using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HeyRed.MarkdownSharp;
using Schema.NET;

namespace Poems;

class Program
{
    const string TagLineToken = "<p class='tags'>{{tags}}</p>";

    static readonly Markdown md = new Markdown();

    static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        bool reviewMode = args.Contains("review");
        if (reviewMode)
            Console.WriteLine("review mode: poem pages carry the rating widget, and Output/review-map.json is written");

        Output.CleanupPrevious();

        var poems = new List<Poem>();
        foreach (string dirpath in Directory.EnumerateDirectories("Poems"))
        {
            if (Path.GetFileName(dirpath) == "Purgatory")
                continue;

            foreach (string filepath in Directory.EnumerateFiles(dirpath))
                poems.Add(AddPoem(filepath, reviewMode));
        }

        LoadTags(poems);

        Person author = Markup.BuildAuthor();
        List<Poem> ordered = poems.OrderBy(poem => poem.PublicationDate).ToList();

        FinishPoemPages(ordered, author);
        RenderHome(poems, author);

        // /best/ became the rating filter
        Directory.CreateDirectory("docs/best");
        File.WriteAllText("docs/best/index.html", Templates.Redirect.Replace("{{target}}", Site.BaseUrl + "/"));

        RenderOtherPage("Other/about.md", Templates.About, $"About | poems by {Site.Name}", author);

        ListPages.RenderArchives(poems);
        ListPages.RenderTitleIndex(poems);
        ListPages.RenderThemes(poems);
        Output.RenderRedirects();
        File.WriteAllText("docs/404.html", Templates.NotFound.Replace("{{navbar}}", Templates.Navbar));

        if (reviewMode)
            Output.WriteReviewManifest(ordered);
        Output.CopyFilesToDocs(reviewMode);
        SitemapGenerator.Write(poems, ListPages.Urls);
        Feed.Write(ordered);

        await PdfBook.Render("docs/culturing.pdf", poems);
        await Video.Render();
    }

    static Poem AddPoem(string filepath, bool reviewMode)
    {
        var poem = new Poem();
        string filename = Path.GetFileNameWithoutExtension(filepath);
        List<string> lines = File.ReadAllLines(filepath).ToList();

        filename = Regex.Replace(filename, "^[0-9][0-9] ", "");
        poem.Title = filename;

        int stars = lines[0].Length - lines[0].TrimStart('*').Length;
        if (stars > Poem.MaxRating)
            throw new InvalidOperationException($"{filepath} opens with {stars} asterisks; the scale runs to {Poem.MaxRating}");
        poem.Rating = stars;
        lines[0] = lines[0].Substring(stars);

        bool titled;
        int bodyStart;
        try
        {
            poem.PublicationDate = DateTime.Parse(lines[1]);
            poem.Title = lines[0];
            titled = true;
            bodyStart = 2;
        }
        catch (FormatException) // Untitled: the date is the first line, and the title stays the filename
        {
            poem.PublicationDate = DateTime.Parse(lines[0]);
            titled = false;
            bodyStart = 1;
        }

        poem.Description = Markup.BuildDescription(lines.Skip(bodyStart));
        poem.Opening = Markup.BuildOpening(lines.Skip(bodyStart));

        // Written out rather than left to markdown's "# ", so a title is never read as markup
        string heading = titled
            ? $"<h1>{WebUtility.HtmlEncode(poem.Title)}</h1>"
            : $"<h1 class='visually-hidden'>{WebUtility.HtmlEncode(poem.Title)}</h1>";

        lines.RemoveRange(0, bodyStart);
        lines.InsertRange(0, new List<string>
        {
            heading,
            $"<p class='dateline'>{Markup.BuildDateLine(poem)}</p>",
            "<p class='url'>{{url}}</p>"
        });

        // The blank line keeps markdown reading the tag line as its own block
        lines.Add(string.Empty);
        lines.Add(TagLineToken);

        string content = string.Join("  \n", lines);
        string finalPoemHtml = Templates.Content
            .Replace("{{content}}", md.Transform(content))
            .Replace("{{title}}", $"{WebUtility.HtmlEncode(poem.Title)} | poems by {Site.Name}")
            .Replace("{{navbar}}", Templates.Navbar)
            .Replace("{{review}}", reviewMode ? "<script src=\"/review.js\"></script>" : string.Empty);
        string finalFileName = Markup.Slugify(poem.Title);

        string dirPath = $"docs{poem.DatePath}".TrimEnd('/') + $"/{finalFileName}";
        string finalPath = $"{dirPath}/index.html";
        if (File.Exists(finalPath))
            throw new InvalidOperationException($"Two poems on {poem.PublicationDate:yyyy-MM-dd} share the slug '{finalFileName}'; the second would overwrite the first ({filepath})");

        Directory.CreateDirectory(dirPath);
        File.WriteAllText(finalPath, finalPoemHtml);

        poem.FilePath = finalPath;
        poem.SourcePath = filepath;
        poem.Link = $"<a href=\"{poem.UrlPath}\">{poem.Title}</a>";

        return poem;
    }

    // Keyed by url, most salient tag first
    static void LoadTags(List<Poem> poems)
    {
        string path = "Other/tags.tsv";
        if (!File.Exists(path))
            return;

        Dictionary<string, Poem> byUrl = poems.ToDictionary(poem => poem.UrlPath);

        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                continue;

            string[] parts = trimmed.Split('\t');
            if (parts.Length != 2)
                throw new InvalidOperationException($"Malformed line in {path}: {line}");

            if (!byUrl.TryGetValue(parts[0], out Poem poem))
                throw new InvalidOperationException(
                    $"{path} tags '{parts[0]}', which matches no poem. Did a title change? "
                    + "Update the key here and add a line to Other/redirects.txt.");

            poem.Tags = parts[1].Split(',')
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .ToList();
        }
    }

    // Fills in what a poem's page could not know while it was read alone
    static void FinishPoemPages(List<Poem> ordered, Person author)
    {
        Dictionary<string, PageHash> hashes = SitemapGenerator.GetHashes();
        (string Previous, string Next)[] edges = Edges.ForSequence(ordered);

        for (int i = 0; i < ordered.Count; ++i)
        {
            Poem poem = ordered[i];
            string canonicalUrl = Site.BaseUrl + poem.UrlPath;

            string contents = File.ReadAllText(poem.FilePath)
                .Replace("{{previous}}", edges[i].Previous)
                .Replace("{{next}}", edges[i].Next)
                .Replace("{{url}}", canonicalUrl);

            string chips = string.Join(" &middot; ", poem.Tags.Select(tag =>
                $"<a href=\"/themes/{Markup.Slugify(tag)}/\">{WebUtility.HtmlEncode(Markup.ThemeName(tag))}</a>"));
            contents = poem.Tags.Count > 0
                ? contents.Replace("{{tags}}", chips)
                : contents.Replace(TagLineToken, string.Empty);
            contents = contents.Replace("{{meta}}", Markup.BuildMetaTags(
                canonicalUrl,
                $"{poem.Title} | poems by {Site.Name}",
                poem.Description,
                "article",
                poem.PublicationDate));

            DateTime dateModified = hashes.TryGetValue(poem.UrlPath, out PageHash hash)
                ? hash.LastMod
                : poem.PublicationDate;

            CreativeWork poemSchema = new CreativeWork()
            {
                Url = new Uri(canonicalUrl),
                Name = poem.Title,
                Headline = poem.Title,
                Description = poem.Description,
                AdditionalType = new Uri("https://schema.org/Poem"), // Schema.NET has no Poem class
                Author = author,
                CopyrightHolder = author,
                CopyrightYear = poem.PublicationDate.Year,
                Genre = "Poem",
                Keywords = string.Join(", ", poem.Tags),
                IsAccessibleForFree = true,
                InLanguage = "en-us",
                PublishingPrinciples = new Uri(Site.BaseUrl + "/about/"),
                DatePublished = poem.PublicationDate,
                DateModified = dateModified
            };

            string breadcrumbs = Markup.BuildBreadcrumbJson(
                Markup.BuildCrumbs(poem.PublicationDate, poem.HasDayUrl, poem.Title, poem.UrlPath));
            contents = contents.Replace("{{schema}}", $"[{poemSchema.ToHtmlEscapedString()},{breadcrumbs}]");

            File.WriteAllText(poem.FilePath, contents);
        }
    }

    static void RenderHome(List<Poem> poems, Person author)
    {
        var chronology = new StringBuilder();
        var years = new List<int>();

        foreach (IGrouping<DateTime, Poem> day in poems.GroupBy(poem => poem.PublicationDate.Date).OrderByDescending(day => day.Key))
        {
            string anchor = string.Empty;
            if (years.Count == 0 || years[^1] != day.Key.Year)
            {
                anchor = $" id=\"year-{day.Key.Year}\"";
                years.Add(day.Key.Year);
            }

            chronology.AppendLine($"<div class=\"node\"{anchor}><h3>{Markup.BuildDateLine(day.First())}</h3><div class=\"leaves\">");
            foreach (Poem poem in day.Reverse())
                chronology.AppendLine(Markup.PoemRow(poem, withOpening: true));
            chronology.AppendLine("</div></div>");
        }

        string chronologyHtml = $"<div class=\"railed\">{Markup.YearRail(years)}<div class=\"railed-body\">{chronology}</div></div>";

        int firstYear = poems.Min(poem => poem.PublicationDate.Year);
        string description = $"A living tree of {poems.Count} poems by culturing, published in order since {firstYear}. Free to read in full.";
        string html = Templates.Index
            .Replace("{{chronology}}", chronologyHtml)
            .Replace("{{navbar}}", Templates.Navbar)
            .Replace("{{title}}", $"poems by {Site.Name}")
            .Replace("{{meta}}", Markup.BuildMetaTags($"{Site.BaseUrl}/", $"poems by {Site.Name}", description, "website"))
            .Replace("{{schema}}", new WebSite()
            {
                Url = new Uri(Site.BaseUrl + "/"),
                Name = Site.Name,
                Description = description,
                Author = author,
                CopyrightHolder = author,
                InLanguage = "en-us",
                IsAccessibleForFree = true,
                PublishingPrinciples = new Uri(Site.BaseUrl + "/about/")
            }.ToHtmlEscapedString());

        File.WriteAllText("docs/index.html", html);
    }

    static void RenderOtherPage(string filepath, string template, string title, Person author)
    {
        List<string> lines = File.ReadAllLines(filepath).ToList();
        string name = Path.GetFileNameWithoutExtension(filepath);
        string url = $"{Site.BaseUrl}/{name}/";
        string description = Markup.BuildDescription(lines.Skip(1));

        string html = template
            .Replace("{{content}}", md.Transform(string.Join("\n", lines)))
            .Replace("{{navbar}}", Templates.Navbar)
            .Replace("{{title}}", title)
            .Replace("{{meta}}", Markup.BuildMetaTags(url, title, description, "website"))
            .Replace("{{schema}}", new AboutPage()
            {
                Url = new Uri(url),
                Name = title,
                Description = description,
                Author = author,
                InLanguage = "en-us",
                IsAccessibleForFree = true
            }.ToHtmlEscapedString());

        Directory.CreateDirectory($"docs/{name}");
        File.WriteAllText($"docs/{name}/index.html", html);
    }
}
