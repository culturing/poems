using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HeyRed.MarkdownSharp;
using Microsoft.Playwright;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Xml.Linq;
using System.Globalization;
using Schema.NET;

namespace Poems;

class Program
{
    static public string BaseUrl = "https://poems.culturing.net";
    static public string SiteName = "culturing";
    static public string OgImageUrl = "https://poems.culturing.net/og-image.png";

    // Poems after this date live at /yyyy/MM/dd/slug/, earlier ones at /yyyy/MM/slug/
    static public readonly DateTime DayUrlCutoff = new DateTime(2026, 03, 04);

    // Set by "dotnet run -- review"
    static bool ReviewMode = false;

    static Markdown md = new Markdown();
    static string IndexTemplate = File.ReadAllText("Templates/index.html");
    static string AboutTemplate = File.ReadAllText("Templates/about.html");
    static string ContentTemplate = File.ReadAllText("Templates/content.html");
    static string ArchiveTemplate = File.ReadAllText("Templates/archive.html");
    static string RedirectTemplate = File.ReadAllText("Templates/redirect.html");
    static string NotFoundTemplate = File.ReadAllText("Templates/404.html");
    static string FaqTemplate = File.ReadAllText("Templates/faq.html");
    static string PdfCopyrightTemplate = File.ReadAllText("Templates/pdf/copyright.html");
    static string PdfEpigraphTemplate = File.ReadAllText("Templates/pdf/epigraph.html");
    static string PdfTableOfContentsTemplate = File.ReadAllText("Templates/pdf/toc.html");
    static string PdfIndexTemplate = File.ReadAllText("Templates/pdf/index.html");
    static string NavbarTemplate = File.ReadAllText("Templates/navbar.html");
    static string SimpleNavbarTemplate = File.ReadAllText("Templates/navbar-simple.html");
    static List<Poem> Poems { get; set; } = new List<Poem>();
    // Archive and theme urls collected while rendering, for the sitemap
    static List<string> ListPageUrls = new List<string>();

    const int MinimumThemePoems = 8;

    const string TagLineToken = "<p class='tags'><em><small><small>{{tags}}</small></small></em></p>";
    static Dictionary<string, List<Poem>> PoemsByDate = new Dictionary<string, List<Poem>>();
    static Dictionary<string, IEnumerable<Poem>> FilteredPoemsByDate;
    static List<Analysis> Analyses { get; set; } = new List<Analysis>();
    static Dictionary<string, List<Analysis>> AnalysesByDate = new Dictionary<string, List<Analysis>>();
    static Dictionary<string, IEnumerable<Analysis>> FilteredAnalysesByDate;
    static XFont Font = new XFont("Quattrocento", 12.0);
    public static List<string> Months = new List<string> { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

    static async Task Main(string[] args)
    {            
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        ReviewMode = args.Contains("review");
        if (ReviewMode)
            Console.WriteLine("review mode: poem pages carry the rating widget, and Output/review-map.json is written");

        CleanupPrevious();
    
    // Parse Poems
        foreach (string dirpath in Directory.EnumerateDirectories("Poems"))
        {
            if (Path.GetFileName(dirpath) == "Purgatory")
                continue;
            
            foreach (string filepath in Directory.EnumerateFiles(dirpath))
            {
                AddPoem(filepath);
            }
        }

        LoadTags();

    // Parse Analyses
        // foreach (string dirpath in Directory.EnumerateDirectories("Analyses"))
        // {            
        //     foreach (string filepath in Directory.EnumerateFiles(dirpath))
        //     {
        //         AddAnalysis(filepath);
        //     }
        // }

    // Build Chronology
        var chronology = new StringBuilder();
        Dictionary<int, int> poemsPerYear = Poems.GroupBy(poem => poem.PublicationDate.Year)
            .ToDictionary(group => group.Key, group => group.Count());
        var yearsSeen = new List<KeyValuePair<int, int>>();
        int lastYear = 0;

        foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByDate.OrderByDescending(kvp => DateTime.Parse(kvp.Key)))
        {
            // The first node of each year carries that year's anchor
            int year = kvp.Value.First().PublicationDate.Year;
            string anchor = string.Empty;
            if (year != lastYear)
            {
                anchor = $" id=\"year-{year}\"";
                yearsSeen.Add(new KeyValuePair<int, int>(year, poemsPerYear[year]));
                lastYear = year;
            }

            chronology.AppendLine($"<div class=\"node\"{anchor}><h3>{BuildDateLine(kvp.Value.First())}</h3><div class=\"leaves\">");
            foreach(Poem poem in Enumerable.Reverse(kvp.Value))
                chronology.AppendLine(PoemRow(poem, withOpening: true));
            chronology.AppendLine("</div></div>");
        }

        string chronologyHtml = $"<div class=\"railed\">{YearRail(yearsSeen)}<div class=\"railed-body\">{chronology}</div></div>";

        Dictionary<string, PageHash> hashes = SitemapGenerator.GetHashes();

    // Set previous and next links
        Person author = BuildAuthor();
        List<Poem> OrderedPoems = Poems.OrderBy(poem => poem.PublicationDate).ToList();

        var previousAt = new int[3][];
        var nextAt = new int[3][];
        for (int tier = 0; tier < 3; tier++)
            (previousAt[tier], nextAt[tier]) = NeighbourChain(OrderedPoems, tier);

        for (int i = 0; i < OrderedPoems.Count; ++i)
        {
            Poem poem = OrderedPoems[i];
            string canonicalUrl = BaseUrl + poem.UrlPath;

            // One pair per rating tier; css shows the reader's
            var previous = new StringBuilder();
            var next = new StringBuilder();
            for (int tier = 0; tier < 3; tier++)
            {
                previous.Append(previousAt[tier][i] >= 0
                    ? EdgeLink("prev", tier, OrderedPoems[previousAt[tier][i]])
                    : EdgeDisabled("prev", tier));
                next.Append(nextAt[tier][i] >= 0
                    ? EdgeLink("next", tier, OrderedPoems[nextAt[tier][i]])
                    : EdgeDisabled("next", tier));
            }
            string previousLink = previous.ToString();
            string nextLink = next.ToString();

            string contents = File.ReadAllText(poem.FilePath);
            contents = contents.Replace("{{previous}}", previousLink);
            contents = contents.Replace("{{next}}", nextLink);
            contents = contents.Replace("{{url}}", canonicalUrl);

            string chips = string.Join(" &middot; ", poem.Tags.Select(tag =>
                $"<a href=\"/themes/{Slugify(tag)}/\">{WebUtility.HtmlEncode(ThemeName(tag))}</a>"));
            contents = poem.Tags.Count > 0
                ? contents.Replace("{{tags}}", chips)
                : contents.Replace(TagLineToken, string.Empty);
            contents = contents.Replace("{{meta}}", BuildMetaTags(
                canonicalUrl,
                $"{poem.Title} | poems by {SiteName}",
                poem.Description,
                "article",
                poem.PublicationDate));

            DateTime dateModified = poem.PublicationDate;
            if (hashes.ContainsKey(poem.UrlPath))
                dateModified = hashes[poem.UrlPath].LastMod;

            CreativeWork poemSchema = new CreativeWork()
            {
                Url = new Uri(canonicalUrl),
                Name = poem.Title,
                Headline = poem.Title,
                Description = poem.Description,
                // Schema.NET has no Poem class
                AdditionalType = new Uri("https://schema.org/Poem"),
                Author = author,
                CopyrightHolder = author,
                CopyrightYear = poem.PublicationDate.Year,
                Genre = "Poem",
                Keywords = string.Join(", ", poem.Tags),
                IsAccessibleForFree = true,
                InLanguage = "en-us",
                PublishingPrinciples = new Uri(BaseUrl + "/about/"),
                DatePublished = poem.PublicationDate,
                DateModified = dateModified
            };

            string breadcrumbs = BuildBreadcrumbJson(
                BuildCrumbs(poem.PublicationDate, poem.HasDayUrl, poem.Title, poem.UrlPath));
            contents = contents.Replace("{{schema}}", $"[{poemSchema.ToHtmlEscapedString()},{breadcrumbs}]");

            File.WriteAllText(poem.FilePath, contents);
        }

        int firstYear = OrderedPoems.First().PublicationDate.Year;

        string homeDescription = $"A living tree of {Poems.Count} poems by culturing, published in order since {firstYear}. Free to read in full.";
        string finalIndexHtml = IndexTemplate
            .Replace("{{chronology}}", chronologyHtml)
            .Replace("{{navbar}}", SimpleNavbarTemplate)
            .Replace("{{title}}", $"poems by {SiteName}")
            .Replace("{{meta}}", BuildMetaTags($"{BaseUrl}/", $"poems by {SiteName}", homeDescription, "website"))
            .Replace("{{schema}}", new WebSite()
            {
                Url = new Uri(BaseUrl + "/"),
                Name = SiteName,
                Description = homeDescription,
                Author = author,
                CopyrightHolder = author,
                InLanguage = "en-us",
                IsAccessibleForFree = true,
                PublishingPrinciples = new Uri(BaseUrl + "/about/")
            }.ToHtmlEscapedString());

        File.WriteAllText("docs/index.html", finalIndexHtml);

    // Redirect /best/, which the rating filter replaced
        Directory.CreateDirectory("docs/best");
        File.WriteAllText("docs/best/index.html", RedirectTemplate.Replace("{{target}}", BaseUrl + "/"));

        RenderOtherPage("Other/about.md", AboutTemplate, $"About | poems by {SiteName}", $"About | poems by {SiteName}", author);
        // RenderOtherPage("Other/FAQ.md");
        // RenderOtherPage("Other/Favorite Poems.md");
        // RenderOtherPage("Other/Why Poetry.md");

        RenderArchives();
        RenderTitleIndex();
        RenderThemes();
        RenderRedirects();
        RenderNotFoundPage();

        WriteReviewManifest(OrderedPoems);
        CopyFilesToDocs();
        GenerateSitemap();
        GenerateFeed(OrderedPoems);

        await RenderPdf("docs/culturing.pdf");
        //await RenderPdf("Submission.pdf", true, new DateTime(2021, 02, 01), new DateTime(2022, 10, 31));
        await RenderVideo();
    }

    static void AddPoem(string filepath)
    {
        var poem = new Poem();
        string filename = Path.GetFileNameWithoutExtension(filepath);
        List<string> lines = File.ReadAllLines(filepath).ToList();
        
        filename = Regex.Replace(filename, "^[0-9][0-9] ", "");
        poem.Title = filename;

        // One leading asterisk per level
        int stars = lines[0].Length - lines[0].TrimStart('*').Length;
        if (stars > Poem.MaxRating)
            throw new InvalidOperationException($"{filepath} opens with {stars} asterisks; the scale runs to {Poem.MaxRating}");
        poem.Rating = stars;
        lines[0] = lines[0].Substring(stars);
        
        bool titled;
        int bodyStart;
        try
        {
            poem.PublicationDate = System.DateTime.Parse(lines[1]);
            poem.Title = lines[0];
            titled = true;
            bodyStart = 2;
        }
        catch(FormatException)
        {
            // Untitled poem: the date is the first line and the title comes from the filename
            poem.PublicationDate = System.DateTime.Parse(lines[0]);
            titled = false;
            bodyStart = 1;
        }

        poem.Description = BuildDescription(lines.Skip(bodyStart));
        poem.Opening = BuildOpening(lines.Skip(bodyStart));

        // Written out rather than left to markdown's "# ", so a title is never read as markup
        string heading = titled
            ? $"<h1>{WebUtility.HtmlEncode(poem.Title)}</h1>"
            : $"<h1 class='visually-hidden'>{WebUtility.HtmlEncode(poem.Title)}</h1>";

        lines.RemoveRange(0, bodyStart);
        lines.InsertRange(0, new List<string>
        {
            heading,
            $"<p style='margin:0;'><em><small><small>{BuildDateLine(poem)}</small></small></em></p>",
            "<p class='url' style='margin:0;'><em><small><small>{{url}}</small></small></em></p>"
        });

        // The blank line keeps markdown reading the tag line as its own block
        lines.Add(string.Empty);
        lines.Add(TagLineToken);

        string dirPath = $"docs{poem.DatePath}".TrimEnd('/');

        string content = String.Join("  \n", lines);
        string contentHtml = md.Transform(content);
        string finalPoemHtml = ContentTemplate
            .Replace("{{content}}", contentHtml)
            .Replace("{{title}}", $"{WebUtility.HtmlEncode(poem.Title)} | poems by {SiteName}")
            .Replace("{{navbar}}", NavbarTemplate)
            .Replace("{{review}}", ReviewMode ? "<script src=\"/review.js\"></script>" : string.Empty);
        string finalFileName = Slugify(poem.Title);

        dirPath += $"/{finalFileName}";
        string finalPath = $"{dirPath}/index.html";
        if (File.Exists(finalPath))
            throw new InvalidOperationException($"Two poems on {poem.PublicationDate:yyyy-MM-dd} share the slug '{finalFileName}'; the second would overwrite the first ({filepath})");

        Directory.CreateDirectory(dirPath);
        File.WriteAllText(finalPath, finalPoemHtml);

        poem.FilePath = finalPath;
        poem.SourcePath = filepath;
        poem.Link = $"<a href=\"{poem.UrlPath}\">{poem.Title}</a>";

        Poems.Add(poem);

    // Sort for chronology
        DateTime pub = poem.PublicationDate;
        string key = $"{pub.Day.ToString("D2")} {Months[pub.Month]} {pub.Year}";
        if (!PoemsByDate.ContainsKey(key))
            PoemsByDate[key] = new List<Poem>();
        PoemsByDate[key].Add(poem);
    }

    static string BuildDescription(IEnumerable<string> bodyLines)
    {
        string text = string.Join(" ", bodyLines);
        text = Regex.Replace(text, "<[^>]+>", " ");                     // inline html some poems carry
        text = Regex.Replace(text, @"!?\[([^\]]*)\]\([^)]*\)", "$1");   // markdown links and images
        text = Regex.Replace(text, @"[*_`#>]", "");                     // emphasis, headings, quotes
        text = Regex.Replace(text, @"\s+", " ").Trim();

        const int limit = 155;
        if (text.Length > limit)
        {
            int cut = text.LastIndexOf(' ', limit);
            if (cut < limit / 2)
                cut = limit;
            text = text.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '-', '—') + "…";
        }

        return text;
    }

    // The first line of verse, shown beside a title in a listing
    static string BuildOpening(IEnumerable<string> bodyLines)
    {
        string line = bodyLines
            .Select(raw => Regex.Replace(raw, "<[^>]+>", " "))
            .Select(raw => Regex.Replace(raw, @"!?\[([^\]]*)\]\([^)]*\)", "$1"))
            .Select(raw => Regex.Replace(raw, @"[*_`#>]", ""))
            .Select(raw => Regex.Replace(raw, @"\s+", " ").Trim())
            .FirstOrDefault(raw => raw.Length > 0) ?? string.Empty;

        const int limit = 52;
        if (line.Length > limit)
        {
            int cut = line.LastIndexOf(' ', limit);
            if (cut < limit / 2)
                cut = limit;
            line = line.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '-', '—') + "…";
        }

        return line;
    }

    // The date under a title, linking its archives. linkDay is false on the day archive itself
    static string BuildDateLine(Poem poem, bool linkDay = true)
    {
        DateTime pub = poem.PublicationDate;
        string day = poem.HasDayUrl && linkDay
            ? $"<a href=\"{poem.DatePath}\">{pub.ToString("dd")}</a>"
            : pub.ToString("dd");

        return $"<time datetime=\"{pub.ToString("yyyy-MM-dd")}\">{day} "
             + $"<a href=\"/{pub.ToString("yyyy")}/{pub.ToString("MM")}/\">{Months[pub.Month]}</a> "
             + $"<a href=\"/{pub.ToString("yyyy")}/\">{pub.ToString("yyyy")}</a></time>";
    }

    static string BuildMetaTags(string canonicalUrl, string title, string description, string ogType, DateTime? published = null)
    {
        string encodedTitle = WebUtility.HtmlEncode(title);
        string encodedDescription = WebUtility.HtmlEncode(description);

        var meta = new List<string>
        {
            $"<link rel=\"canonical\" href=\"{canonicalUrl}\" />",
            $"<meta name=\"description\" content=\"{encodedDescription}\" />",
            $"<meta name=\"author\" content=\"{SiteName}\" />",
            $"<meta property=\"og:site_name\" content=\"{SiteName}\" />",
            $"<meta property=\"og:type\" content=\"{ogType}\" />",
            $"<meta property=\"og:title\" content=\"{encodedTitle}\" />",
            $"<meta property=\"og:description\" content=\"{encodedDescription}\" />",
            $"<meta property=\"og:url\" content=\"{canonicalUrl}\" />",
            $"<meta property=\"og:image\" content=\"{OgImageUrl}\" />",
            $"<meta name=\"twitter:card\" content=\"summary_large_image\" />",
            $"<meta name=\"twitter:title\" content=\"{encodedTitle}\" />",
            $"<meta name=\"twitter:description\" content=\"{encodedDescription}\" />",
            $"<meta name=\"twitter:image\" content=\"{OgImageUrl}\" />"
        };

        if (published.HasValue)
            meta.Add($"<meta property=\"article:published_time\" content=\"{published.Value.ToString("yyyy-MM-dd")}\" />");

        return string.Join("\n    ", meta);
    }

    // JsonSerializer escapes < > &, so the result is safe to embed in a <script>
    static string BuildBreadcrumbJson(List<KeyValuePair<string, string>> crumbs)
    {
        var items = new List<Dictionary<string, object>>();
        for (int i = 0; i < crumbs.Count; ++i)
        {
            items.Add(new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["name"] = crumbs[i].Key,
                ["item"] = BaseUrl + crumbs[i].Value
            });
        }

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        });
    }

    static List<KeyValuePair<string, string>> BuildCrumbs(DateTime pub, bool includeDay, string leafName, string leafUrl)
    {
        var crumbs = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(SiteName, "/"),
            new KeyValuePair<string, string>(pub.ToString("yyyy"), $"/{pub.ToString("yyyy")}/"),
            new KeyValuePair<string, string>(Months[pub.Month], $"/{pub.ToString("yyyy")}/{pub.ToString("MM")}/")
        };

        if (includeDay)
            crumbs.Add(new KeyValuePair<string, string>(pub.ToString("dd"), $"/{pub.ToString("yyyy")}/{pub.ToString("MM")}/{pub.ToString("dd")}/"));

        if (leafName != null)
            crumbs.Add(new KeyValuePair<string, string>(leafName, leafUrl));

        return crumbs;
    }

    static Person BuildAuthor()
    {
        return new Person()
        {
            Name = SiteName,
            Url = new Uri(BaseUrl),
            SameAs = new List<Uri>
            {
                new Uri("https://bsky.app/profile/culturing.bsky.social"),
                new Uri("https://www.youtube.com/channel/UCqOgJLPDUhKz9DZQivo99PQ"),
                new Uri("https://github.com/culturing"),
            },
            PublishingPrinciples = new Uri(BaseUrl + "/about/")
        };
    }

    static string Slugify(string title)
    {
        StringBuilder unaccented = new StringBuilder();
        foreach (char c in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                unaccented.Append(c);
        }

        string slug = unaccented.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        slug = Regex.Replace(slug, @"[\s/\\_]+", "-");
        slug = Regex.Replace(slug, @"[^0-9a-z\-]", "");
        slug = Regex.Replace(slug, "-{2,}", "-");
        slug = slug.Trim('-');

        // An all-numeric slug could shadow a day directory under /yyyy/MM/
        if (slug.Length == 0)
            slug = "untitled";
        else if (!slug.Any(char.IsLetter))
            slug = $"poem-{slug}";

        return slug;
    }

    static void AddAnalysis(string filepath)
    {
        var analysis = new Analysis();
        string filename = Path.GetFileNameWithoutExtension(filepath);
        List<string> lines = File.ReadAllLines(filepath).ToList();
        
        filename = Regex.Replace(filename, "^[0-9][0-9] ", "");
        analysis.Title = filename;
        
        analysis.PublicationDate = System.DateTime.Parse(lines[2]);
        analysis.Title = lines[0];
        lines[0] = $"### {lines[0]}";
        lines[2] = $"<p style='margin:0;'><em><small><small>{lines[2]}</small></small></em></p>";

        string dirPath = $"docs/analysis/{analysis.PublicationDate.ToString("yyyy")}/{analysis.PublicationDate.ToString("MM")}/{analysis.PublicationDate.ToString("dd")}";
        
        string content = String.Join("  \n", lines);
        string contentHtml = md.Transform(content);
        string finalPoemHtml = ContentTemplate.Replace("{{content}}", contentHtml).Replace("{{title}}", analysis.Title).Replace("{{navbar}}", NavbarTemplate);
        string finalFileName = Regex.Replace(filename.ToLower().Replace(" ", "-"), @"[^0-9a-zA-Z\-]", "");

        dirPath += $"/{finalFileName}";
        Directory.CreateDirectory(dirPath);        
        string finalPath = $"{dirPath}/index.html";
        File.WriteAllText(finalPath, finalPoemHtml);

        analysis.FilePath = finalPath;
        analysis.Link = $"<a href=\"{analysis.UrlPath}\">{analysis.Title}</a>";

        Analyses.Add(analysis);

    // Sort for chronology
        string key = $"{Months[analysis.PublicationDate.Month]} {analysis.PublicationDate.Year}";
        if (!AnalysesByDate.ContainsKey(key))
            AnalysesByDate[key] = new List<Analysis>();
        AnalysesByDate[key].Add(analysis);
    }

    static void RenderOtherPage(string filepath, string template, string titleHtml, string titleText, Person author)
    {
        List<string> lines = File.ReadAllLines(filepath).ToList();
        string name = Path.GetFileNameWithoutExtension(filepath);
        string url = $"{BaseUrl}/{name}/";
        string description = BuildDescription(lines.Skip(1));

        string html = template
            .Replace("{{content}}", md.Transform(string.Join("\n", lines)))
            .Replace("{{navbar}}", SimpleNavbarTemplate)
            .Replace("{{title}}", titleHtml)
            .Replace("{{meta}}", BuildMetaTags(url, titleText, description, "website"))
            .Replace("{{schema}}", new AboutPage()
            {
                Url = new Uri(url),
                Name = titleText,
                Description = description,
                Author = author,
                InLanguage = "en-us",
                IsAccessibleForFree = true
            }.ToHtmlEscapedString());

        string dirpath = $"docs/{name}";
        string htmlpath = $"{dirpath}/index.html";

        Directory.CreateDirectory(dirpath);
        File.WriteAllText(htmlpath, html);
    }

    // Other/tags.tsv, keyed by url, most salient tag first
    static void LoadTags()
    {
        string path = "Other/tags.tsv";
        if (!File.Exists(path))
            return;

        Dictionary<string, Poem> byUrl = Poems.ToDictionary(poem => poem.UrlPath);

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

    // Theme hubs at /themes/<tag>/, and the field of themes at /themes/
    static void RenderThemes()
    {
        Dictionary<string, List<Poem>> byTag = new Dictionary<string, List<Poem>>();
        foreach (Poem poem in Poems.OrderBy(poem => poem.PublicationDate))
        {
            foreach (string tag in poem.Tags)
            {
                if (!byTag.ContainsKey(tag))
                    byTag[tag] = new List<Poem>();
                byTag[tag].Add(poem);
            }
        }

        List<string> published = byTag.Keys
            .Where(tag => byTag[tag].Count >= MinimumThemePoems)
            .OrderBy(tag => tag)
            .ToList();

        foreach (string tag in byTag.Keys.Except(published).OrderBy(tag => tag))
            Console.WriteLine($"theme '{tag}' held back: {byTag[tag].Count} poems, needs {MinimumThemePoems}");

        int fewest = published.Min(tag => byTag[tag].Count);
        int most = published.Max(tag => byTag[tag].Count);

        var index = new StringBuilder();
        foreach (string tag in published)
        {
            string name = ThemeName(tag);
            string themePath = $"/themes/{Slugify(tag)}/";
            int count = byTag[tag].Count;

            WriteListPage(
                themePath,
                $"Poems about {name}",
                $"{count} poems about {name} by {SiteName}, written between "
                    + $"{byTag[tag].First().PublicationDate.Year} and {byTag[tag].Last().PublicationDate.Year}. Free to read in full.",
                ThemeArchiveBody(byTag[tag]),
                BuildThemeCrumbs(name, themePath),
                titleText: char.ToUpperInvariant(name[0]) + name.Substring(1),
                showTrail: false);

            index.AppendLine($"<div class=\"theme w{Weight(count, fewest, most)}\"><a href=\"{themePath}\">{WebUtility.HtmlEncode(name)}</a>"
                + $"<small class=\"count\">{count}</small></div>");
        }

        WriteListPage(
            "/themes/",
            "Themes",
            $"Every theme in the collection, from love to war. {Poems.Count} poems by {SiteName}, "
                + $"grouped by what they are about.",
            $"<div class=\"theme-list\">{index}</div>",
            BuildThemeCrumbs(null, null));
    }

    static string ThemeName(string tag) => tag.Replace('-', ' ');

    // Nine steps, on a log scale
    static int Weight(int count, int fewest, int most)
    {
        if (most <= fewest)
            return 5;
        double t = (Math.Log(count) - Math.Log(fewest)) / (Math.Log(most) - Math.Log(fewest));
        return Math.Clamp((int)Math.Round(1 + t * 8), 1, 9);
    }

    // A rail of year anchors, newest first, weighted by how many poems each year holds
    static string YearRail(List<KeyValuePair<int, int>> yearsNewestFirst)
    {
        int fewest = yearsNewestFirst.Min(year => year.Value);
        int most = yearsNewestFirst.Max(year => year.Value);

        var rail = new StringBuilder("<nav class=\"year-rail\" aria-label=\"Jump to a year\">");
        foreach (KeyValuePair<int, int> year in yearsNewestFirst)
            rail.Append($"<a class=\"w{Weight(year.Value, fewest, most)}\" href=\"#year-{year.Key}\">{year.Key}</a>");
        rail.Append("</nav>");
        return rail.ToString();
    }

    // A theme's poems, grouped under year headings beside a year rail
    static string ThemeArchiveBody(List<Poem> poems)
    {
        List<IGrouping<int, Poem>> byYear = poems
            .GroupBy(poem => poem.PublicationDate.Year)
            .OrderByDescending(group => group.Key)
            .ToList();

        // One year needs no rail
        if (byYear.Count < 2)
            return $"<div class=\"theme-columns\">{ArchivePoemLinks(poems, withOpening: false)}</div>";

        string rail = YearRail(byYear
            .Select(group => new KeyValuePair<int, int>(group.Key, group.Count()))
            .ToList());

        var list = new StringBuilder("<div class=\"railed-body\">");
        foreach (IGrouping<int, Poem> group in byYear)
        {
            list.Append($"<section class=\"year\" id=\"year-{group.Key}\"><h3 class=\"year-mark\">{group.Key}</h3><div class=\"theme-columns\">");
            list.Append(ArchivePoemLinks(group, withOpening: false));
            list.Append("</div></section>");
        }
        list.Append("</div>");

        return $"<div class=\"railed\">{rail}{list}</div>";
    }

    static List<KeyValuePair<string, string>> BuildThemeCrumbs(string name, string themePath)
    {
        var crumbs = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(SiteName, "/"),
            new KeyValuePair<string, string>("themes", "/themes/")
        };
        if (name != null)
            crumbs.Add(new KeyValuePair<string, string>(name, themePath));
        return crumbs;
    }

    // Year, month and day index pages
    static void RenderArchives()
    {
        foreach (IGrouping<int, Poem> yearGroup in Poems.GroupBy(poem => poem.PublicationDate.Year).OrderBy(group => group.Key))
        {
            int year = yearGroup.Key;
            string yearPath = $"/{year}/";
            var yearBody = new StringBuilder();

            foreach (IGrouping<int, Poem> monthGroup in yearGroup.GroupBy(poem => poem.PublicationDate.Month).OrderByDescending(group => group.Key))
            {
                int month = monthGroup.Key;
                string monthPath = $"/{year}/{month.ToString("D2")}/";
                yearBody.AppendLine($"<div class=\"node\"><h3><a href=\"{monthPath}\">{Months[month]}</a></h3><div class=\"leaves\">");
                yearBody.Append(ArchivePoemLinks(monthGroup, withOpening: true));
                yearBody.AppendLine("</div></div>");

                var monthBody = new StringBuilder();

                // A month can straddle the day-url cutoff, so each date decides for itself
                foreach (IGrouping<DateTime, Poem> dayGroup in monthGroup.GroupBy(poem => poem.PublicationDate.Date).OrderByDescending(group => group.Key))
                {
                    DateTime day = dayGroup.Key;
                    string dayLabel = $"{day.ToString("dd")} {Months[month]} {year}";
                    bool hasDayArchive = dayGroup.First().HasDayUrl;
                    string dayPath = $"/{year}/{month.ToString("D2")}/{day.ToString("dd")}/";

                    monthBody.AppendLine($"<div class=\"node\"><h3>{BuildDateLine(dayGroup.First())}</h3><div class=\"leaves\">");
                    monthBody.Append(ArchivePoemLinks(dayGroup, withOpening: true));
                    monthBody.AppendLine("</div></div>");

                    if (hasDayArchive)
                    {
                        // Built as a single chronology node, its <h1> hidden behind the date
                        WriteListPage(
                            dayPath,
                            dayLabel,
                            $"The {dayGroup.Count()} poems culturing published on {dayLabel}.",
                            $"<div class=\"node\"><h3>{BuildDateLine(dayGroup.First(), linkDay: false)}</h3>"
                                + $"<div class=\"leaves\">{ArchivePoemLinks(dayGroup, withOpening: true)}</div></div>",
                            BuildCrumbs(day, false, dayLabel, dayPath),
                            hideHeading: true);
                    }
                }

                WriteListPage(
                    monthPath,
                    $"{Months[month]} {year}",
                    $"The {monthGroup.Count()} poems culturing published in {Months[month]} {year}.",
                    monthBody.ToString(),
                    BuildCrumbs(monthGroup.First().PublicationDate, false, null, null));
            }

            WriteListPage(
                yearPath,
                $"Poems from {year}",
                $"The {yearGroup.Count()} poems culturing published in {year}.",
                yearBody.ToString(),
                new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(SiteName, "/"),
                    new KeyValuePair<string, string>(year.ToString(), yearPath)
                },
                year.ToString());
        }
    }

    // Reversed first so that, the sort being stable, poems sharing a date keep chronology order
    static string ArchivePoemLinks(IEnumerable<Poem> poems, bool withOpening)
    {
        var html = new StringBuilder();
        foreach (Poem poem in poems.Reverse().OrderByDescending(poem => poem.PublicationDate))
            html.AppendLine(PoemRow(poem, withOpening));
        return html.ToString();
    }

    // One row of a listing
    static string PoemRow(Poem poem, bool withOpening)
    {
        string opening = withOpening && poem.Opening.Length > 0
            ? $"<span class=\"opening\">{WebUtility.HtmlEncode(poem.Opening)}</span>"
            : string.Empty;
        return $"<div class=\"{poem.RatingClass}\">{poem.Link}{opening}</div>";
    }

    static string EdgeChevron(string kind) =>
        $"<svg class=\"edge-mark\" width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">"
        + $"<path d=\"{(kind == "prev" ? "M14.5 5 8 12l6.5 7" : "M9.5 5 16 12l-6.5 7")}\"/></svg>";

    // For every position in the sequence, the nearest poem on each side that meets the tier
    static (int[] previous, int[] next) NeighbourChain(List<Poem> ordered, int tier)
    {
        var previous = new int[ordered.Count];
        var next = new int[ordered.Count];

        int seen = -1;
        for (int i = 0; i < ordered.Count; i++)
        {
            previous[i] = seen;
            if (ordered[i].Rating >= tier)
                seen = i;
        }

        seen = -1;
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            next[i] = seen;
            if (ordered[i].Rating >= tier)
                seen = i;
        }

        return (previous, next);
    }

    // Only the unfiltered pair carries an id; the others are found by tier
    static string EdgeId(string kind, int tier) => tier == 0
        ? $" id=\"{(kind == "prev" ? "previous" : "next")}\""
        : string.Empty;

    static string EdgeLink(string kind, int tier, Poem neighbour)
    {
        string label = $"<span class=\"edge-label\">{WebUtility.HtmlEncode(neighbour.Title)}</span>";
        string inner = kind == "prev" ? EdgeChevron(kind) + label : label + EdgeChevron(kind);
        return $"<a{EdgeId(kind, tier)} class=\"edge edge-{kind}\" data-edge=\"{kind}\" data-tier=\"{tier}\""
            + $" rel=\"{kind}\" href=\"{neighbour.UrlPath}\">{inner}</a>";
    }

    // The ends of a chain: the slot stays occupied, but emits no link
    static string EdgeDisabled(string kind, int tier) =>
        $"<span{EdgeId(kind, tier)} class=\"edge edge-{kind} edge-disabled\" data-edge=\"{kind}\""
        + $" data-tier=\"{tier}\" aria-hidden=\"true\">{EdgeChevron(kind)}</span>";

    // Punctuation dropped, so "Am I Right?" sorts under its own letter rather than after it
    static string SortKey(string title) => Regex.Replace(title, @"[^\w\s]", "");

    // The alphabetical index at /index/
    static void RenderTitleIndex()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var byLetter = new SortedDictionary<string, List<Poem>>(StringComparer.Ordinal);

        foreach (Poem poem in Poems.OrderBy(poem => SortKey(poem.Title), StringComparer.OrdinalIgnoreCase))
        {
            char first = SortKey(poem.Title).TrimStart().FirstOrDefault();
            // Titles opening on a digit collect under the tilde, which sorts last
            string letter = char.IsLetter(first) ? char.ToUpperInvariant(first).ToString() : "~";
            if (!byLetter.ContainsKey(letter))
                byLetter[letter] = new List<Poem>();
            byLetter[letter].Add(poem);
        }

        // Every letter appears, whether or not it has poems
        var rail = new StringBuilder("<nav class=\"rail\" aria-label=\"Jump to a letter\">");
        foreach (char letter in alphabet)
            rail.Append(byLetter.ContainsKey(letter.ToString())
                ? $"<a href=\"#letter-{letter}\">{letter}</a>"
                : $"<span class=\"rail-empty\" aria-hidden=\"true\">{letter}</span>");
        rail.Append("</nav>");

        var body = new StringBuilder(rail.ToString());
        body.Append("<div class=\"letters\">");
        foreach (KeyValuePair<string, List<Poem>> group in byLetter)
        {
            string id = group.Key == "~" ? "letter-other" : $"letter-{group.Key}";
            string mark = group.Key == "~" ? "&amp;c" : group.Key;
            body.Append($"<section class=\"letter\" id=\"{id}\"><h2 class=\"letter-mark\">{mark}</h2><div class=\"letter-list\">");
            foreach (Poem poem in group.Value)
                body.AppendLine(PoemRow(poem, withOpening: false));
            body.Append("</div></section>");
        }
        body.Append("</div>");

        WriteListPage(
            "/index/",
            "Index",
            $"Every one of the {Poems.Count} poems by {SiteName}, listed by title. Free to read in full.",
            body.ToString(),
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(SiteName, "/"),
                new KeyValuePair<string, string>("index", "/index/")
            });
    }

    // titleText names a page in <title> where its visible heading would read wrong there
    static void WriteListPage(string urlPath, string heading, string description, string body, List<KeyValuePair<string, string>> crumbs, string titleText = null, bool showTrail = true, bool hideHeading = false)
    {
        titleText = titleText ?? heading;
        string fullTitle = $"{titleText} | poems by {SiteName}";
        // The site name and the page itself are dropped from the visible trail; the
        // BreadcrumbList below keeps the whole chain
        List<KeyValuePair<string, string>> visible = crumbs.Skip(1).ToList();
        if (visible.Count > 0 && visible[visible.Count - 1].Value == urlPath)
            visible.RemoveAt(visible.Count - 1);

        string trail = visible.Count == 0
            ? string.Empty
            : string.Join(" &rsaquo; ", visible.Select(crumb =>
                $"<a href=\"{crumb.Value}\">{WebUtility.HtmlEncode(crumb.Key)}</a>"));

        string html = ArchiveTemplate
            .Replace("{{navbar}}", SimpleNavbarTemplate)
            .Replace("{{title}}", WebUtility.HtmlEncode(fullTitle))
            .Replace("{{headingClass}}", hideHeading ? "visually-hidden" : string.Empty)
            .Replace("{{heading}}", WebUtility.HtmlEncode(heading))
            .Replace("{{breadcrumb}}", !showTrail || trail.Length == 0 ? string.Empty : $"<p class=\"breadcrumb\"><small>{trail}</small></p>")
            .Replace("{{content}}", body)
            .Replace("{{meta}}", BuildMetaTags(BaseUrl + urlPath, fullTitle, description, "website"))
            .Replace("{{schema}}", "[" + new CollectionPage()
            {
                Url = new Uri(BaseUrl + urlPath),
                Name = heading,
                Description = description,
                InLanguage = "en-us",
                IsAccessibleForFree = true
            }.ToHtmlEscapedString() + "," + BuildBreadcrumbJson(crumbs) + "]");

        string dirpath = "docs" + urlPath.TrimEnd('/');
        Directory.CreateDirectory(dirpath);
        File.WriteAllText($"{dirpath}/index.html", html);
        ListPageUrls.Add(urlPath);
    }

    // A renamed url gets a stub carrying a meta refresh. See Other/redirects.txt
    static void RenderRedirects()
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
            File.WriteAllText(filepath, RedirectTemplate.Replace("{{target}}", BaseUrl + parts[1]));
        }
    }

    static void RenderNotFoundPage()
    {
        File.WriteAllText("docs/404.html", NotFoundTemplate.Replace("{{navbar}}", SimpleNavbarTemplate));
    }

    static void GenerateFeed(List<Poem> orderedPoems)
    {
        const int feedLength = 50;
        XNamespace atom = "http://www.w3.org/2005/Atom";
        string description = $"Poems by culturing, newest first. {BaseUrl}/";

        var items = orderedPoems
            .OrderByDescending(poem => poem.PublicationDate)
            .Take(feedLength)
            .Select(poem => new XElement("item",
                new XElement("title", poem.Title),
                new XElement("link", BaseUrl + poem.UrlPath),
                new XElement("guid", new XAttribute("isPermaLink", "true"), BaseUrl + poem.UrlPath),
                new XElement("pubDate", poem.PublicationDate.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)),
                new XElement("description", poem.Description)));

        var feed = new XDocument(
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "atom", atom),
                new XElement("channel",
                    new XElement("title", $"poems by {SiteName}"),
                    new XElement("link", BaseUrl + "/"),
                    new XElement("description", description),
                    new XElement("language", "en-us"),
                    new XElement("lastBuildDate", DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)),
                    new XElement(atom + "link",
                        new XAttribute("href", $"{BaseUrl}/feed.xml"),
                        new XAttribute("rel", "self"),
                        new XAttribute("type", "application/rss+xml")),
                    items)));

        File.WriteAllText("docs/feed.xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + feed.ToString(), new UTF8Encoding(false));
    }

    static async Task RenderPdf(string outpath, bool bestOnly = false, DateTime start = default, DateTime end = default)
    {
        if (File.Exists(outpath))
            File.Delete(outpath);

        Directory.CreateDirectory("docs/pdf");
        foreach (string file in Directory.EnumerateFiles("Templates/pdf"))
        {
            File.Copy(file, $"docs/pdf/{Path.GetFileName(file)}", true);
        }

        if (start == default)
            start = DateTime.MinValue;
        if (end == default)
            end = DateTime.MaxValue;

        FilteredPoemsByDate = new();
        foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByDate)
        {
            IEnumerable<Poem> filteredPoems = kvp.Value.Where(poem => poem.PublicationDate > start && poem.PublicationDate < end);
            if (bestOnly)
                filteredPoems = filteredPoems.Where(poem => poem.Bold);

            if (filteredPoems.Count() > 0)
                FilteredPoemsByDate[kvp.Key] = filteredPoems;                
        }
            
        using PdfDocument pdf = new PdfDocument();

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync( new BrowserTypeLaunchOptions
        {
            Args = new List<string>
            {
                "--disable-export-tagged-pdf"
            }
        });        
        BrowserNewPageOptions newPageOptions = new BrowserNewPageOptions { BaseURL = "http://127.0.0.1:5500" };
        IPage page = await browser.NewPageAsync(newPageOptions);

        PagePdfOptions pdfRenderOptions = new PagePdfOptions 
        { 
            Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
            Tagged = false
        };

    // Add title
        pdfRenderOptions.Path = $"Output/Pdfs/Title.pdf";
        await page.GotoAsync("/pdf/title.html");
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument titlePdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(titlePdf, pdf);
        }
        pdf.Outlines.Add("Title", pdf.Pages[pdf.PageCount - 1]);

    // Add copyright
        string copyrightHtml = PdfCopyrightTemplate.Replace("{{year}}", DateTime.Now.ToString("yyyy"));
        string copyrightPath = $"docs/pdf/copyright.html";
        File.WriteAllText(copyrightPath, copyrightHtml);

        pdfRenderOptions.Path = $"Output/Pdfs/Copyright.pdf";
        await page.GotoAsync("/pdf/copyright.html");
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument copyrightPdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(copyrightPdf, pdf);
        }
        pdf.Outlines.Add("Copyright", pdf.Pages[pdf.PageCount - 1]);

    // Add epigraph
        string epigraphHtml = PdfEpigraphTemplate.Replace("{{year}}", DateTime.Now.ToString("yyyy"));
        string epigraphPath = $"docs/pdf/epigraph.html";
        File.WriteAllText(epigraphPath, epigraphHtml);

        pdfRenderOptions.Path = $"Output/Pdfs/Epigraph.pdf";
        await page.GotoAsync("/pdf/epigraph.html");
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument epigraphPdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(epigraphPdf, pdf);
        }
        pdf.Outlines.Add("Epigraph", pdf.Pages[pdf.PageCount - 1]);

    // Add about
        pdfRenderOptions.Path = $"Output/Pdfs/About.pdf";
        await page.GotoAsync("/about/index.html");
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument aboutPdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(aboutPdf, pdf);
        }
        pdf.Outlines.Add("About", pdf.Pages[pdf.PageCount - 1]);
        
    // Add temporary table of contents
        int tableOfContentsStart = pdf.PageCount;
        int tableOfContentsPageCount = 0;
        await RenderTableOfContents(page);
        using (PdfDocument tableOfContentsPdf = PdfReader.Open("Output/Pdfs/TableOfContents.pdf", PdfDocumentOpenMode.Import))
        {
            tableOfContentsPageCount = tableOfContentsPdf.PageCount;
            MergePdfs(tableOfContentsPdf, pdf);
        }

    // Add poems
        PdfOutline contentsOutline = pdf.Outlines.Add("Contents", pdf.Pages[tableOfContentsStart]);
        PdfOutline poemsOutline = pdf.Outlines.Add("Poems", pdf.Pages[pdf.PageCount - 1]);

        List<Poem> flatPoems = new List<Poem>();
        foreach(KeyValuePair<string, IEnumerable<Poem>> kvp in FilteredPoemsByDate.OrderBy(kvp => DateTime.Parse(kvp.Key)))
        {
            IEnumerable<Poem> poems = kvp.Value.Where(poem => poem.PublicationDate > start && poem.PublicationDate < end);
            if (bestOnly)
                poems = poems.Where(poem => poem.Bold);

            flatPoems.AddRange(poems);
        }

        StringBuilder allPoemsHtml = new StringBuilder();
        allPoemsHtml.AppendLine("<!DOCTYPE html>\n<html>\n<head>\n<meta charset='utf-8'/>");
        
        int headStart = ContentTemplate.IndexOf("<head>", StringComparison.OrdinalIgnoreCase) + 6;
        int headEnd = ContentTemplate.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if(headStart >= 6 && headEnd > headStart)
        {
            // Unsubstituted tokens would land in <head> as bare text, which ends the head early
            string head = ContentTemplate.Substring(headStart, headEnd - headStart);
            allPoemsHtml.AppendLine(Regex.Replace(head, @"\{\{\w+\}\}", ""));
        }
        allPoemsHtml.AppendLine("<style>.poem-page { display: flex; flex-direction: column; justify-content: center; min-height: 100vh; page-break-after: always; break-after: page; box-sizing: border-box; } body { margin: 0; padding: 0; }</style>");
        allPoemsHtml.AppendLine("</head>\n<body>");

        for (int i = 0; i < flatPoems.Count; i++)
        {
            Poem poem = flatPoems[i];
            string poemHtml = File.ReadAllText(poem.FilePath);
            int bodyStart = poemHtml.IndexOf("<body>", StringComparison.OrdinalIgnoreCase) + 6;
            int bodyEnd = poemHtml.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            
            if(bodyStart >= 6 && bodyEnd > bodyStart)
            {
                string bodyContent = poemHtml.Substring(bodyStart, bodyEnd - bodyStart);

                allPoemsHtml.AppendLine($"<div class='poem-page' id='poem_{i}'>");
                allPoemsHtml.AppendLine(bodyContent);
                allPoemsHtml.AppendLine("</div>");
            }
        }
        allPoemsHtml.AppendLine("</body>\n</html>");
        File.WriteAllText("docs/pdf/all_poems.html", allPoemsHtml.ToString());

        // Set before the navigation, so the book is laid out once and in print media only
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });

        await page.GotoAsync("/pdf/all_poems.html");

        // Take the navbar, pagination and Creative Commons links out of the tree
        await page.EvaluateAsync(@"() => {
            const elements = document.querySelectorAll('.navbar, .edge, img[src*=\'cc.png\'], a[href*=\'creativecommons\']');
            elements.forEach(el => el.remove());
        }");

        // Instantly measure physical DOM height to determine accurate PDF page spanning
        var jsCode = @"() => {
            let counts =[];
            let elements = document.querySelectorAll('.poem-page');
            for (let el of elements) {
                let style = window.getComputedStyle(el);
                let mt = parseFloat(style.marginTop) || 0;
                let mb = parseFloat(style.marginBottom) || 0;
                let height = el.getBoundingClientRect().height + mt + mb;
                
                // 11in page - 2in margins = 9in printable height. 9in * 96 DPI = 864 pixels.
                let pages = Math.ceil(height / 864);
                if (pages === 0) pages = 1;
                counts.push(pages);
            }
            return counts;
        }";

        int[] pageSpans = await page.EvaluateAsync<int[]>(jsCode);

        int currentPoemPage = pdf.PageCount + 1;
        for (int i = 0; i < flatPoems.Count; i++)
        {
            flatPoems[i].Page = currentPoemPage;
            currentPoemPage += pageSpans[i];
        }
        
        pdfRenderOptions.Path = "Output/Pdfs/AllPoems.pdf";
        await page.PdfAsync(pdfRenderOptions);

        using (PdfDocument poemsPdf = PdfReader.Open("Output/Pdfs/AllPoems.pdf", PdfDocumentOpenMode.Import))
        {
            MergePdfs(poemsPdf, pdf);
            foreach (Poem poem in flatPoems)
            {
                poemsOutline.Outlines.Add(poem.Title, pdf.Pages[poem.Page - 1]);
            }
        }

    // Render final table of contents
        string tocPath = await RenderTableOfContents(page);
        using (PdfDocument tableOfContentsPdf = PdfReader.Open(tocPath, PdfDocumentOpenMode.Import))
        {
            int i = tableOfContentsStart;
            foreach (PdfPage pdfPage in tableOfContentsPdf.Pages)
            {
                pdf.Pages.RemoveAt(i);
                PdfPage insertedPage = pdf.Pages.Insert(i, pdfPage);
                AddPageNumber(insertedPage, i + 1);
                ++i;
            }
            contentsOutline.DestinationPage = pdf.Pages[tableOfContentsStart];
            poemsOutline.DestinationPage = pdf.Pages[tableOfContentsStart + tableOfContentsPdf.PageCount];
        }

    // Add index
        string indexPath = await RenderPdfIndex(page, bestOnly, start, end);
        using (PdfDocument indexPdf = PdfReader.Open(indexPath, PdfDocumentOpenMode.Import))
        {
            MergePdfs(indexPdf, pdf);
            pdf.Outlines.Add("Index", pdf.Pages[pdf.PageCount - indexPdf.PageCount]);
        }

        pdf.Save("Output/Pdfs/culturing.pdf");

        List<string> args = new List<string>
        {
            $"-sDEVICE=pdfwrite",
            $"-dCompatibilityLevel=1.5",
            $"-dEmbedAllFonts=false",
            $"-dSubsetFonts=false",
            $"-dQUIET",
            $"-o {outpath}",
            $"Output/Pdfs/culturing.pdf"
        };

        using (Process process = Process.Start("gswin64c", string.Join(" ", args)))
        {
            process.WaitForExit();
        }

        if (Directory.Exists("docs/pdf"))
            Directory.Delete("docs/pdf", true);
    }

    static void MergePdfs(PdfDocument source, PdfDocument destination)
    {
        foreach (PdfPage pdfPage in source.Pages)
        {
            PdfPage addedPage = destination.AddPage(pdfPage);
            AddPageNumber(addedPage, destination.PageCount);
        }
    }

    static void AddPageNumber(PdfPage page, int pageNumber)
    {
        using (XGraphics gfx = XGraphics.FromPdfPage(page))
        {
            double x = 0;
            double y = page.Height - Font.Height - new XUnit(0.5, XGraphicsUnit.Inch);
            double width = page.Width - new XUnit(0.5, XGraphicsUnit.Inch);
            double height = Font.Height;
            gfx.DrawString($"{pageNumber}", Font, XBrushes.Black, new XRect(x, y, width, height), XStringFormats.CenterRight);
        }
    }

    static async Task<string> RenderTableOfContents(IPage page)
    {
        string toc = string.Empty;
        foreach(KeyValuePair<string, IEnumerable<Poem>> kvp in FilteredPoemsByDate.OrderBy(kvp => DateTime.Parse(kvp.Key)))
        {
            toc += $"<div class='toc-section'>";
            toc += $"  <div class='toc-flex toc-section-header'>";
            toc += $"    <span>{kvp.Key}</span>";
            toc += $"    <span class='toc-page'>{kvp.Value.FirstOrDefault()?.Page}</span>";
            toc += $"  </div>";
            foreach(Poem poem in kvp.Value)
            {
                toc += $"  <div class='toc-flex toc-poem'>";
                toc += $"    <span class='{poem.RatingClass}'>{poem.Title}</span>";
                toc += $"    <span class='toc-page'>{poem.Page}</span>";
                toc += $"  </div>";
            }
            toc += $"</div>";
        }

        string tocHtml = PdfTableOfContentsTemplate.Replace("{{toc}}", toc);
        string filepath = "docs/pdf/TableOfContents.html";
        File.WriteAllText(filepath, tocHtml);

        await page.GotoAsync("/pdf/TableOfContents.html");

        string pdfPath = "Output/Pdfs/TableOfContents.pdf";
        PagePdfOptions options = new PagePdfOptions 
        { 
            Path = pdfPath,
            Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
            Tagged = false
        };         
        await page.PdfAsync(options);
        
        return pdfPath;
    }

    static async Task<string> RenderPdfIndex(IPage page, bool bestOnly, DateTime start, DateTime end)
    {
        string index = string.Empty;
        foreach(Poem poem in Poems.OrderBy(p => Regex.Replace(p.Title, @"[^\w\s]", "")))
        {
            if (bestOnly && !poem.Bold)
                continue; 

            if (poem.PublicationDate > start && poem.PublicationDate < end)
        {
            index += $"<div class='toc-flex toc-poem'>";
            index += $"  <span class='{poem.RatingClass}'>{poem.Title}</span>";
            index += $"  <span class='toc-page'>{poem.Page}</span>";
            index += $"</div>";
            }
        }

        string indexHtml = PdfIndexTemplate.Replace("{{index}}", index);
        string filepath = "docs/pdf/index.html";
        File.WriteAllText(filepath, indexHtml);

        await page.GotoAsync("/pdf/index.html");

        string pdfPath = "Output/Pdfs/Index.pdf";
        PagePdfOptions options = new PagePdfOptions 
        { 
            Path = pdfPath,
            Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
            Tagged = false
        };         
        await page.PdfAsync(options);

        return pdfPath;
    }

    static async Task RenderVideo()
    {
        if (!Directory.Exists("Video"))
            Directory.CreateDirectory("Video");            
        if (!Directory.Exists("Output/Video"))
            Directory.CreateDirectory("Output/Video");            

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync();
        IPage page = await browser.NewPageAsync();
        await page.GotoAsync("file:///" + Path.GetFullPath("Templates/video.html"));

        foreach(string dirpath in Directory.GetDirectories("Audio"))
        {
            string dir = Path.GetFileName(dirpath);
            Directory.CreateDirectory($"Video/{dir}");
            foreach(string filepath in Directory.GetFiles(dirpath))
            {
                string audioWav = Path.GetFullPath(filepath);
                string title = Path.GetFileNameWithoutExtension(filepath);
                string outpath = Path.GetFullPath($"Video/{dir}/{title}.mov");
                if (!File.Exists(outpath))
                {
                    string titlePng = await RenderVideoSplash(page, dir, title);

                    string args = string.Empty;
                    args += $" -loop 1 -i \"{titlePng}\""; // add title
                    args += $" -loop 1 -i black.png"; // add background
                    args += $" -itsoffset 2.5s -i \"{audioWav}\""; // add audio after small delay
                    args += $" -filter_complex";
                    args += $" \"";
                    args += $"  [0:v]fade=t=in:st=0s:d=0.5s,fade=t=out:st=4.5s:d=0.5s,scale=1920:1080[v0];"; // fade title in and out
                    args += $"  [v0][1:v]concat=n=2:v=1:a=0,scale=1920:1080[outv];"; // concat title and background into one video stream
                    args += $" \"";
                    args += $" -map \"[outv]\" -map 2:a -shortest \"{outpath}\""; // map video to audio

                    Process.Start("ffmpeg", args).WaitForExit();
                }
            }
        }
    }

    static async Task<string> RenderVideoSplash(IPage page, string dir, string title)
    {
        await page.EvaluateAsync($"document.querySelector('.title').innerHTML = '{title}'");
        
        PageScreenshotOptions options = new PageScreenshotOptions();
        options.FullPage = true;
        options.Path = Path.GetFullPath($"Output/Video/{dir}/{title}.png");
        await page.ScreenshotAsync(options);           
        
        return options.Path;
    }

    static void GenerateSitemap()
    {
        string filepath = "docs/sitemap.xml";
        if (File.Exists(filepath))
            File.Delete(filepath);

        string xmlString = SitemapGenerator.GenerateXmlString(Poems, ListPageUrls);
        File.WriteAllText(filepath, xmlString, new UTF8Encoding(false));
    }

    // Maps each poem url back to its source file, for the review server
    static void WriteReviewManifest(List<Poem> ordered)
    {
        if (!ReviewMode)
            return;

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

    static void CopyFilesToDocs()
    {
        List<string> filesToCopy = new List<string>();
        // toc.css ships too: RenderPdf loads it as /toc.css over the local server
        filesToCopy.AddRange(Directory.GetFiles("Styles"));
        // review.js belongs to the review pass only
        filesToCopy.AddRange(Directory.GetFiles("Scripts")
            .Where(file => ReviewMode || Path.GetFileName(file) != "review.js"));

        foreach(string file in filesToCopy)
        {
            File.Copy(file, $"docs/{Path.GetFileName(file)}");
        }

        // The card link previews use; 1920x1080 is near enough the 1.91:1 Open Graph ratio
        File.Copy("culturing.png", "docs/og-image.png", true);

        // Stops GitHub Pages running the content through Jekyll, which drops _-prefixed paths
        File.WriteAllText("docs/.nojekyll", string.Empty);
    }

    static void CleanupPrevious()
    {
        if (Directory.Exists("docs"))
        {                   
            foreach(string dir in Directory.GetDirectories("docs"))
            {
                Directory.Delete(dir, true);
            }

            string[] exclude =["culturing.pdf", "CNAME", "google84fcfca997bbbdc0.html", "robots.txt", "favicon.ico", "cc.png"];
            foreach(string file in Directory.GetFiles("docs").Where(path => !exclude.Contains(Path.GetFileName(path))))
            {
                File.Delete(file);
            }
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
}