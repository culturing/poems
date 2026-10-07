using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Poems;

static class Feed
{
    const int Length = 50;

    public static void Write(List<Poem> orderedPoems)
    {
        XNamespace atom = "http://www.w3.org/2005/Atom";
        string description = $"Poems by culturing, newest first. {Site.BaseUrl}/";

        var items = orderedPoems
            .OrderByDescending(poem => poem.PublicationDate)
            .Take(Length)
            .Select(poem => new XElement("item",
                new XElement("title", poem.Title),
                new XElement("link", Site.BaseUrl + poem.UrlPath),
                new XElement("guid", new XAttribute("isPermaLink", "true"), Site.BaseUrl + poem.UrlPath),
                new XElement("pubDate", RfcDate(poem.PublicationDate)),
                new XElement("description", poem.Description)));

        var feed = new XDocument(
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "atom", atom),
                new XElement("channel",
                    new XElement("title", $"poems by {Site.Name}"),
                    new XElement("link", Site.BaseUrl + "/"),
                    new XElement("description", description),
                    new XElement("language", "en-us"),
                    new XElement("lastBuildDate", RfcDate(DateTime.UtcNow)),
                    new XElement(atom + "link",
                        new XAttribute("href", $"{Site.BaseUrl}/feed.xml"),
                        new XAttribute("rel", "self"),
                        new XAttribute("type", "application/rss+xml")),
                    items)));

        File.WriteAllText("docs/feed.xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + feed.ToString(), new UTF8Encoding(false));
    }

    static string RfcDate(DateTime date) => date.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
}
