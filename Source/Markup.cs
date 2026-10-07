using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Schema.NET;

namespace Poems;

record Crumb(string Name, string Url);

static class Markup
{
    public static string BuildDescription(IEnumerable<string> bodyLines) =>
        Truncate(StripMarkup(string.Join(" ", bodyLines)), 155);

    public static string BuildOpening(IEnumerable<string> bodyLines) =>
        Truncate(bodyLines.Select(StripMarkup).FirstOrDefault(line => line.Length > 0) ?? string.Empty, 52);

    static string StripMarkup(string text)
    {
        text = Regex.Replace(text, "<[^>]+>", " ");                     // inline html some poems carry
        text = Regex.Replace(text, @"!?\[([^\]]*)\]\([^)]*\)", "$1");   // markdown links and images
        text = Regex.Replace(text, @"[*_`#>]", "");                     // emphasis, headings, quotes
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    // Cuts at a word, unless the last word break falls in the first half
    static string Truncate(string text, int limit)
    {
        if (text.Length <= limit)
            return text;

        int cut = text.LastIndexOf(' ', limit);
        if (cut < limit / 2)
            cut = limit;
        return text.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '-', '—') + "…";
    }

    // linkDay is false on the day archive, so the page does not link to itself
    public static string BuildDateLine(Poem poem, bool linkDay = true)
    {
        DateTime pub = poem.PublicationDate;
        string day = poem.HasDayUrl && linkDay
            ? $"<a href=\"{poem.DatePath}\">{pub.ToString("dd")}</a>"
            : pub.ToString("dd");

        return $"<time datetime=\"{pub.ToString("yyyy-MM-dd")}\">{day} "
             + $"<a href=\"/{pub.ToString("yyyy")}/{pub.ToString("MM")}/\">{Site.Months[pub.Month]}</a> "
             + $"<a href=\"/{pub.ToString("yyyy")}/\">{pub.ToString("yyyy")}</a></time>";
    }

    public static string BuildMetaTags(string canonicalUrl, string title, string description, string ogType, DateTime? published = null)
    {
        string encodedTitle = WebUtility.HtmlEncode(title);
        string encodedDescription = WebUtility.HtmlEncode(description);

        var meta = new List<string>
        {
            $"<link rel=\"canonical\" href=\"{canonicalUrl}\" />",
            $"<meta name=\"description\" content=\"{encodedDescription}\" />",
            $"<meta name=\"author\" content=\"{Site.Name}\" />",
            $"<meta property=\"og:site_name\" content=\"{Site.Name}\" />",
            $"<meta property=\"og:type\" content=\"{ogType}\" />",
            $"<meta property=\"og:title\" content=\"{encodedTitle}\" />",
            $"<meta property=\"og:description\" content=\"{encodedDescription}\" />",
            $"<meta property=\"og:url\" content=\"{canonicalUrl}\" />",
            $"<meta property=\"og:image\" content=\"{Site.OgImageUrl}\" />",
            $"<meta name=\"twitter:card\" content=\"summary_large_image\" />",
            $"<meta name=\"twitter:title\" content=\"{encodedTitle}\" />",
            $"<meta name=\"twitter:description\" content=\"{encodedDescription}\" />",
            $"<meta name=\"twitter:image\" content=\"{Site.OgImageUrl}\" />"
        };

        if (published.HasValue)
            meta.Add($"<meta property=\"article:published_time\" content=\"{published.Value.ToString("yyyy-MM-dd")}\" />");

        return string.Join("\n    ", meta);
    }

    // JsonSerializer escapes < > &, so the result is safe to embed in a <script>
    public static string BuildBreadcrumbJson(List<Crumb> crumbs)
    {
        var items = crumbs.Select((crumb, i) => new Dictionary<string, object>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = crumb.Name,
            ["item"] = Site.BaseUrl + crumb.Url
        }).ToList();

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        });
    }

    public static List<Crumb> BuildCrumbs(DateTime pub, bool includeDay, string leafName, string leafUrl)
    {
        var crumbs = new List<Crumb>
        {
            new Crumb(Site.Name, "/"),
            new Crumb(pub.ToString("yyyy"), $"/{pub.ToString("yyyy")}/"),
            new Crumb(Site.Months[pub.Month], $"/{pub.ToString("yyyy")}/{pub.ToString("MM")}/")
        };

        if (includeDay)
            crumbs.Add(new Crumb(pub.ToString("dd"), $"/{pub.ToString("yyyy")}/{pub.ToString("MM")}/{pub.ToString("dd")}/"));

        if (leafName != null)
            crumbs.Add(new Crumb(leafName, leafUrl));

        return crumbs;
    }

    public static Person BuildAuthor()
    {
        return new Person()
        {
            Name = Site.Name,
            Url = new Uri(Site.BaseUrl),
            SameAs = new List<Uri>
            {
                new Uri("https://bsky.app/profile/culturing.bsky.social"),
                new Uri("https://www.youtube.com/channel/UCqOgJLPDUhKz9DZQivo99PQ"),
                new Uri("https://github.com/culturing"),
            },
            PublishingPrinciples = new Uri(Site.BaseUrl + "/about/")
        };
    }

    public static string Slugify(string title)
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

    public static string ThemeName(string tag) => tag.Replace('-', ' ');

    // Punctuation dropped, so "Am I Right?" sorts under its own letter rather than after it
    public static string SortKey(string title) => Regex.Replace(title, @"[^\w\s]", "");

    public static string YearRail(IEnumerable<int> yearsNewestFirst)
    {
        var rail = new StringBuilder("<nav class=\"year-rail\" aria-label=\"Jump to a year\">");
        foreach (int year in yearsNewestFirst)
            rail.Append($"<a href=\"#year-{year}\">{year}</a>");
        rail.Append("</nav>");
        return rail.ToString();
    }

    public static string ArchivePoemLinks(IEnumerable<Poem> poems, bool withOpening)
    {
        var html = new StringBuilder();
        // Reversed first so that, the sort being stable, poems sharing a date keep chronology order
        foreach (Poem poem in poems.Reverse().OrderByDescending(poem => poem.PublicationDate))
            html.AppendLine(PoemRow(poem, withOpening));
        return html.ToString();
    }

    public static string PoemRow(Poem poem, bool withOpening)
    {
        string opening = withOpening && poem.Opening.Length > 0
            ? $"<span class=\"opening\">{WebUtility.HtmlEncode(poem.Opening)}</span>"
            : string.Empty;
        return $"<div class=\"{poem.RatingClass}\">{poem.Link}{opening}</div>";
    }
}
