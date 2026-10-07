using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Schema.NET;

namespace Poems;

// The archives, the themes and the title index
static class ListPages
{
    const int MinimumThemePoems = 8;

    // For the sitemap
    public static readonly List<string> Urls = new List<string>();

    public static void RenderArchives(List<Poem> poems)
    {
        foreach (IGrouping<int, Poem> yearGroup in poems.GroupBy(poem => poem.PublicationDate.Year).OrderBy(group => group.Key))
        {
            int year = yearGroup.Key;
            string yearPath = $"/{year}/";
            var yearBody = new StringBuilder();

            foreach (IGrouping<int, Poem> monthGroup in yearGroup.GroupBy(poem => poem.PublicationDate.Month).OrderByDescending(group => group.Key))
            {
                int month = monthGroup.Key;
                string monthPath = $"/{year}/{month.ToString("D2")}/";
                yearBody.AppendLine($"<div class=\"node\"><h3><a href=\"{monthPath}\">{Site.Months[month]}</a></h3><div class=\"leaves\">");
                yearBody.Append(Markup.ArchivePoemLinks(monthGroup, withOpening: true));
                yearBody.AppendLine("</div></div>");

                var monthBody = new StringBuilder();

                // A month can straddle the day-url cutoff, so each date decides for itself
                foreach (IGrouping<DateTime, Poem> dayGroup in monthGroup.GroupBy(poem => poem.PublicationDate.Date).OrderByDescending(group => group.Key))
                {
                    DateTime day = dayGroup.Key;
                    string dayLabel = $"{day.ToString("dd")} {Site.Months[month]} {year}";
                    string dayPath = $"/{year}/{month.ToString("D2")}/{day.ToString("dd")}/";

                    monthBody.AppendLine($"<div class=\"node\"><h3>{Markup.BuildDateLine(dayGroup.First())}</h3><div class=\"leaves\">");
                    monthBody.Append(Markup.ArchivePoemLinks(dayGroup, withOpening: true));
                    monthBody.AppendLine("</div></div>");

                    if (dayGroup.First().HasDayUrl)
                    {
                        Write(
                            dayPath,
                            dayLabel,
                            $"The {dayGroup.Count()} poems culturing published on {dayLabel}.",
                            $"<div class=\"node\"><h3>{Markup.BuildDateLine(dayGroup.First(), linkDay: false)}</h3>"
                                + $"<div class=\"leaves\">{Markup.ArchivePoemLinks(dayGroup, withOpening: true)}</div></div>",
                            Markup.BuildCrumbs(day, false, dayLabel, dayPath),
                            hideHeading: true); // The node's date says the same words
                    }
                }

                Write(
                    monthPath,
                    $"{Site.Months[month]} {year}",
                    $"The {monthGroup.Count()} poems culturing published in {Site.Months[month]} {year}.",
                    monthBody.ToString(),
                    Markup.BuildCrumbs(monthGroup.First().PublicationDate, false, null, null));
            }

            Write(
                yearPath,
                $"Poems from {year}",
                $"The {yearGroup.Count()} poems culturing published in {year}.",
                yearBody.ToString(),
                new List<Crumb> { new Crumb(Site.Name, "/"), new Crumb(year.ToString(), yearPath) },
                year.ToString());
        }
    }

    public static void RenderThemes(List<Poem> poems)
    {
        var byTag = new Dictionary<string, List<Poem>>();
        foreach (Poem poem in poems.OrderBy(poem => poem.PublicationDate))
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
            string name = Markup.ThemeName(tag);
            string themePath = $"/themes/{Markup.Slugify(tag)}/";
            int count = byTag[tag].Count;

            Write(
                themePath,
                $"Poems about {name}",
                $"{count} poems about {name} by {Site.Name}, written between "
                    + $"{byTag[tag].First().PublicationDate.Year} and {byTag[tag].Last().PublicationDate.Year}. Free to read in full.",
                ThemeArchiveBody(byTag[tag]),
                ThemeCrumbs(name, themePath),
                titleText: char.ToUpperInvariant(name[0]) + name.Substring(1),
                showTrail: false);

            index.AppendLine($"<div class=\"theme w{Weight(count, fewest, most)}\"><a href=\"{themePath}\">{WebUtility.HtmlEncode(name)}</a>"
                + $"<small class=\"count\">{count}</small></div>");
        }

        Write(
            "/themes/",
            "Themes",
            $"Every theme in the collection, from love to war. {poems.Count} poems by {Site.Name}, "
                + $"grouped by what they are about.",
            $"<div class=\"theme-list\">{index}</div>",
            ThemeCrumbs(null, null));
    }

    static int Weight(int count, int fewest, int most)
    {
        if (most <= fewest)
            return 5;
        double t = (Math.Log(count) - Math.Log(fewest)) / (Math.Log(most) - Math.Log(fewest));
        return Math.Clamp((int)Math.Round(1 + t * 8), 1, 9);
    }

    static string ThemeArchiveBody(List<Poem> poems)
    {
        List<IGrouping<int, Poem>> byYear = poems
            .GroupBy(poem => poem.PublicationDate.Year)
            .OrderByDescending(group => group.Key)
            .ToList();

        if (byYear.Count < 2)
            return $"<div class=\"theme-columns\">{Markup.ArchivePoemLinks(poems, withOpening: false)}</div>";

        string rail = Markup.YearRail(byYear.Select(group => group.Key));

        // Focusable for the script in archive.html, and out of the tab order: the list is
        // links, and tabbing through them scrolls it anyway
        var list = new StringBuilder("<div class=\"railed-body\" tabindex=\"-1\">");
        foreach (IGrouping<int, Poem> group in byYear)
        {
            list.Append($"<section class=\"year\" id=\"year-{group.Key}\"><h3 class=\"year-mark\">{group.Key}</h3><div class=\"theme-columns\">");
            list.Append(Markup.ArchivePoemLinks(group, withOpening: false));
            list.Append("</div></section>");
        }
        list.Append("</div>");

        return $"<div class=\"railed\">{rail}{list}</div>";
    }

    static List<Crumb> ThemeCrumbs(string name, string themePath)
    {
        var crumbs = new List<Crumb> { new Crumb(Site.Name, "/"), new Crumb("themes", "/themes/") };
        if (name != null)
            crumbs.Add(new Crumb(name, themePath));
        return crumbs;
    }

    public static void RenderTitleIndex(List<Poem> poems)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var byLetter = new SortedDictionary<string, List<Poem>>(StringComparer.Ordinal);

        foreach (Poem poem in poems.OrderBy(poem => Markup.SortKey(poem.Title), StringComparer.OrdinalIgnoreCase))
        {
            char first = Markup.SortKey(poem.Title).TrimStart().FirstOrDefault();
            // Titles opening on a digit collect under the tilde, which sorts last
            string letter = char.IsLetter(first) ? char.ToUpperInvariant(first).ToString() : "~";
            if (!byLetter.ContainsKey(letter))
                byLetter[letter] = new List<Poem>();
            byLetter[letter].Add(poem);
        }

        // Empty letters stay, so the rail never changes shape
        var rail = new StringBuilder("<nav class=\"rail\" aria-label=\"Jump to a letter\">");
        foreach (char letter in alphabet)
            rail.Append(byLetter.ContainsKey(letter.ToString())
                ? $"<a href=\"#letter-{letter}\">{letter}</a>"
                : $"<span class=\"rail-empty\" aria-hidden=\"true\">{letter}</span>");
        rail.Append("</nav>");

        var body = new StringBuilder(rail.ToString());
        // tabindex, so the script in archive.html can hand it the page keys once it scrolls
        body.Append("<div class=\"letters\" tabindex=\"-1\">");
        foreach (KeyValuePair<string, List<Poem>> group in byLetter)
        {
            string id = group.Key == "~" ? "letter-other" : $"letter-{group.Key}";
            string mark = group.Key == "~" ? "&amp;c" : group.Key;
            body.Append($"<section class=\"letter\" id=\"{id}\"><h2 class=\"letter-mark\">{mark}</h2><div class=\"letter-list\">");
            foreach (Poem poem in group.Value)
                body.AppendLine(Markup.PoemRow(poem, withOpening: false));
            body.Append("</div></section>");
        }
        body.Append("</div>");

        Write(
            "/index/",
            "Index",
            $"Every one of the {poems.Count} poems by {Site.Name}, listed by title. Free to read in full.",
            body.ToString(),
            new List<Crumb> { new Crumb(Site.Name, "/"), new Crumb("index", "/index/") });
    }

    // titleText names a page in <title> where its visible heading would read wrong there
    static void Write(string urlPath, string heading, string description, string body, List<Crumb> crumbs, string titleText = null, bool showTrail = true, bool hideHeading = false)
    {
        titleText = titleText ?? heading;
        string fullTitle = $"{titleText} | poems by {Site.Name}";
        // The site name and the page itself are dropped from the visible trail; the
        // BreadcrumbList below keeps the whole chain
        List<Crumb> visible = crumbs.Skip(1).ToList();
        if (visible.Count > 0 && visible[visible.Count - 1].Url == urlPath)
            visible.RemoveAt(visible.Count - 1);

        string trail = visible.Count == 0
            ? string.Empty
            : string.Join(" &rsaquo; ", visible.Select(crumb =>
                $"<a href=\"{crumb.Url}\">{WebUtility.HtmlEncode(crumb.Name)}</a>"));

        string html = Templates.Archive
            .Replace("{{navbar}}", Templates.Navbar)
            .Replace("{{title}}", WebUtility.HtmlEncode(fullTitle))
            .Replace("{{headingClass}}", hideHeading ? "visually-hidden" : string.Empty)
            .Replace("{{heading}}", WebUtility.HtmlEncode(heading))
            .Replace("{{breadcrumb}}", !showTrail || trail.Length == 0 ? string.Empty : $"<p class=\"breadcrumb\"><small>{trail}</small></p>")
            .Replace("{{content}}", body)
            .Replace("{{meta}}", Markup.BuildMetaTags(Site.BaseUrl + urlPath, fullTitle, description, "website"))
            .Replace("{{schema}}", "[" + new CollectionPage()
            {
                Url = new Uri(Site.BaseUrl + urlPath),
                Name = heading,
                Description = description,
                InLanguage = "en-us",
                IsAccessibleForFree = true
            }.ToHtmlEscapedString() + "," + Markup.BuildBreadcrumbJson(crumbs) + "]");

        string dirpath = "docs" + urlPath.TrimEnd('/');
        Directory.CreateDirectory(dirpath);
        File.WriteAllText($"{dirpath}/index.html", html);
        Urls.Add(urlPath);
    }
}
