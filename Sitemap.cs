using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System.Text.Json;

namespace Poems;

public enum SitemapChangeFrequency { Always, Hourly, Daily, Weekly, Monthly, Yearly, Never}

class SitemapNode
{
    public string Url { get; set; }
    public DateTime? LastModified { get; set; }
    public SitemapChangeFrequency? ChangeFrequency { get; set; }
    public decimal? Priority { get; set; }
    public SitemapNode(){}

    public SitemapNode(Poem poem, DateTime lastMod)
    {
        Url = $"{Program.BaseUrl}/{poem.FilePath}";
        LastModified = lastMod;
        ChangeFrequency = SitemapChangeFrequency.Yearly;
        Priority = 0.9M;
    }
}

class SitemapGenerator
{
    static public string GenerateXmlString(IEnumerable<Poem> poems)
    {
        List<SitemapNode> sitemapNodes = GetSitemapNodes(poems);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var sitemap = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(ns + "urlset",
                sitemapNodes.Select(node =>
                    new XElement(ns + "url",
                        // Required: <loc>
                        new XElement(ns + "loc", node.Url),

                        // Optional: <lastmod>
                        node.LastModified.HasValue ?
                            new XElement(ns + "lastmod", node.LastModified.Value.ToString("yyyy-MM-dd")) :
                            null, // LINQ to XML conveniently skips null elements

                        // Optional: <changefreq>
                        node.ChangeFrequency.HasValue ?
                            new XElement(ns + "changefreq", node.ChangeFrequency.Value.ToString().ToLowerInvariant()) :
                            null,

                        // Optional: <priority>
                        node.Priority.HasValue ?
                            new XElement(ns + "priority", node.Priority.Value.ToString("0.0", CultureInfo.InvariantCulture)) : // Use InvariantCulture for decimal formatting
                            null
                    )
                )
            )
        );

        return sitemap.ToString(); // This includes the XML declaration
    }

    static public List<SitemapNode> GetSitemapNodes(IEnumerable<Poem> poems)
    {
        string hashesJson;
        Dictionary<string, PoemHash> hashes;

        if (File.Exists("hashes.json"))
        {
            hashesJson = File.ReadAllText("hashes.json");
            hashes = JsonSerializer.Deserialize<Dictionary<string, PoemHash>>(hashesJson);
        }
        else 
        {
            hashes = new Dictionary<string, PoemHash>();
        }

        DateTime now = DateTime.UtcNow;

        var nodes = new List<SitemapNode>
        {
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/",
                LastModified = now,
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            },
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/best.html",
                LastModified = now,
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            },            
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/Poems.pdf",
                LastModified = now,
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            }
        };

        foreach (Poem poem in poems)
        {
            string hash = poem.GetHash();

            if (hashes.ContainsKey(poem.FilePath))
            {
                PoemHash prevHash = hashes[poem.FilePath];                
                if (prevHash.Hash == hash)
                {
                    // no-op
                }
                else 
                {
                    hashes[poem.FilePath] = new PoemHash { Hash = hash, LastMod = now };
                }
            }
            else
            {
                hashes[poem.FilePath] = new PoemHash { Hash = hash, LastMod = now };
            }

            DateTime lastMod = hashes[poem.FilePath].LastMod;
            nodes.Add(new SitemapNode(poem, lastMod));
        }

        hashesJson = JsonSerializer.Serialize(hashes);
        File.WriteAllText("hashes.json", hashesJson);

        return nodes;
    }
}

class PoemHash
{
    public string Hash { get; set; }
    public DateTime LastMod { get; set; }
}