using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;

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
        Url = Program.BaseUrl + poem.UrlPath;
        LastModified = lastMod;
        ChangeFrequency = SitemapChangeFrequency.Yearly;
        Priority = 0.9M;
    }
}

class SitemapGenerator
{
    static public JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
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
                            new XElement(ns + "lastmod", node.LastModified.Value.ToString("o")) :
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

    static public Dictionary<string, PageHash> GetHashes()
    {
        string hashesJson;
        Dictionary<string, PageHash> hashes;

        if (File.Exists("hashes.json"))
        {
            hashesJson = File.ReadAllText("hashes.json");
            hashes = JsonSerializer.Deserialize<Dictionary<string, PageHash>>(hashesJson);
        }
        else 
        {
            hashes = new Dictionary<string, PageHash>();
        }

        return hashes;
    }

    static public List<SitemapNode> GetSitemapNodes(IEnumerable<Poem> poems)
    {
        Dictionary<string, PageHash> hashes = GetHashes();        

        DateTime now = DateTime.UtcNow;

        List<SitemapNode> nodes = new List<SitemapNode>
        {
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/",
                LastModified = UpdateHash(hashes, "docs/index.html", "/", now),
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            },
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/best/",
                LastModified = UpdateHash(hashes, "docs/best/index.html", "/best/", now),
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            },            
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/about/",
                LastModified = UpdateHash(hashes, "docs/about/index.html", "/about/", now),
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            },            
            new SitemapNode
            {
                Url = $"{Program.BaseUrl}/culturing.pdf",
                LastModified = UpdateHash(hashes, "docs/culturing.pdf", "/culturing.pdf", now),
                ChangeFrequency = SitemapChangeFrequency.Monthly,
                Priority = 1.0M
            }
        };

        foreach (Poem poem in poems)
        {            
            DateTime lastMod = UpdateHash(hashes, poem.FilePath, poem.UrlPath, now);
            nodes.Add(new SitemapNode(poem, lastMod));
        }

        string hashesJson = JsonSerializer.Serialize(hashes, JsonOptions);
        File.WriteAllText("hashes.json", hashesJson);

        return nodes;
    }

    static public DateTime UpdateHash(Dictionary<string, PageHash> hashes, string filePath, string urlPath, DateTime now)
    {
        string hash = GetHash(filePath);
        if (hashes.ContainsKey(urlPath))
        {
            PageHash prevHash = hashes[urlPath];                
            if (prevHash.Hash == hash)
            {
                return prevHash.LastMod;
            }
            else 
            {
                hashes[urlPath] = new PageHash { Hash = hash, LastMod = now };
                return now;
            }
        }
        else
        {
            hashes[urlPath] = new PageHash { Hash = hash, LastMod = now };
            return now;
        }
    }

    static public string GetHash(string filePath)
    {
        string contents = File.ReadAllText(filePath);
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(contents));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

class PageHash
{
    public string Hash { get; set; }
    public DateTime LastMod { get; set; }
}