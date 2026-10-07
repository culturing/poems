using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Poems;

// Housekeeping for docs/ and Output/
static class Output
{
    public static void CleanupPrevious()
    {
        if (Directory.Exists("docs"))
        {
            foreach (string dir in Directory.GetDirectories("docs"))
                Directory.Delete(dir, true);

            string[] exclude = ["culturing.pdf", "CNAME", "google84fcfca997bbbdc0.html", "robots.txt", "favicon.ico", "cc.png"];
            foreach (string file in Directory.GetFiles("docs").Where(path => !exclude.Contains(Path.GetFileName(path))))
                File.Delete(file);
        }
        else
        {
            Directory.CreateDirectory("docs");
        }

        if (Directory.Exists("Output"))
            Directory.Delete("Output", true);
        Directory.CreateDirectory("Output/Pdfs");
        Directory.CreateDirectory("Output/Other");
    }

    public static void CopyFilesToDocs(bool reviewMode)
    {
        List<string> filesToCopy = new List<string>();
        // toc.css ships too: PdfBook loads it as /toc.css over the local server
        filesToCopy.AddRange(Directory.GetFiles("Styles"));
        filesToCopy.AddRange(Directory.GetFiles("Scripts")
            .Where(file => reviewMode || Path.GetFileName(file) != "review.js"));

        foreach (string file in filesToCopy)
            File.Copy(file, $"docs/{Path.GetFileName(file)}");

        // 1920x1080 is near enough the 1.91:1 Open Graph ratio
        File.Copy("culturing.png", "docs/og-image.png", true);

        // Stops GitHub Pages running the content through Jekyll, which drops _-prefixed paths
        File.WriteAllText("docs/.nojekyll", string.Empty);
    }

    // GitHub Pages cannot serve a 301, so each old url gets a meta-refresh stub
    public static void RenderRedirects()
    {
        string listPath = "Other/redirects.txt";
        if (!File.Exists(listPath))
            return;

        foreach (string line in File.ReadAllLines(listPath))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                continue;

            string[] parts = Regex.Split(trimmed, @"\s+");
            if (parts.Length != 2)
                throw new InvalidOperationException($"Malformed redirect in {listPath}: {line}");

            string dirpath = "docs" + parts[0].TrimEnd('/');
            string filepath = $"{dirpath}/index.html";

            // A later poem may legitimately have claimed the old url back
            if (File.Exists(filepath))
                continue;

            Directory.CreateDirectory(dirpath);
            File.WriteAllText(filepath, Templates.Redirect.Replace("{{target}}", Site.BaseUrl + parts[1]));
        }
    }

    // Maps each poem url back to its source file, for the review server
    public static void WriteReviewManifest(List<Poem> ordered)
    {
        var poems = ordered.Select((poem, i) => new Dictionary<string, object>
        {
            ["i"] = i,
            ["url"] = poem.UrlPath,
            ["title"] = poem.Title,
            ["rating"] = poem.Rating,
            ["src"] = poem.SourcePath.Replace('\\', '/')
        }).ToList();

        Directory.CreateDirectory("Output");
        File.WriteAllText("Output/review-map.json", JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["max"] = Poem.MaxRating,
            ["poems"] = poems
        }, new JsonSerializerOptions { WriteIndented = false }));
        Console.WriteLine($"review manifest: {poems.Count} poems -> Output/review-map.json");
    }
}
