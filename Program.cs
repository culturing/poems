using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HeyRed.MarkdownSharp;

namespace Poems
{
    class Poem
    {
        public string Link { get; set; }
        public DateTime CompositionDate { get; set; }
        public static List<string> Months = new List<string> { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    }

    class Program
    {
        static void Main(string[] args)
        {
            string rootUrl = "https://raw.githubusercontent.com/culturing/Poems/main/";

            var md = new Markdown();

            List<Poem> poems = new List<Poem>();

            string contentTemplate = File.ReadAllText("Templates/content.html");
            string indexHtml = string.Empty;
            foreach (var file in Directory.EnumerateFiles("Poems"))
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string content = File.ReadAllText(file);
                string contentHtml = md.Transform(content);

                // if (File.Exists($"Audio/{filename}.m4a"))
                // {
                //     contentHtml += $"<audio controls src='{rootUrl}/Audio/{filename}.m4a' style='width:100%'></audio>";
                // }

                string finalPoemHtml = contentTemplate.Replace("{{content}}", contentHtml);

                string finalPath = $"Pages/{filename}.html";
                File.WriteAllText(finalPath, finalPoemHtml);

                string link = $"<a href=\"{finalPath}\">{filename}</a>";
                indexHtml += $"<div>{link}</div>\n";

                var match = Regex.Match(content, @"<small><small>(.*)<\/small><\/small>");
                if (match.Success)
                {
                    string date = match.Groups[1].Value;
                    var poem = new Poem
                    {
                        Link = link,
                        CompositionDate = System.DateTime.Parse(date)
                    };
                    poems.Add(poem);
                }
            }

            string chronologyHtml = string.Empty;
            int month = 0;
            int year = 0;

            foreach (Poem poem in poems.OrderByDescending(poems => poems.CompositionDate))
            {
                if ((month != poem.CompositionDate.Month) || (year != poem.CompositionDate.Year))
                {
                    month = poem.CompositionDate.Month;
                    year = poem.CompositionDate.Year;
                    chronologyHtml += $"<h3>{Poem.Months[month]} {year}</h3>\n";
                }
                chronologyHtml += $"<div>{poem.Link}</div>\n";
            }

            string finalIndexHtml = File.ReadAllText("Templates/index.html")
                .Replace("{{index}}", indexHtml)
                .Replace("{{chronology}}", chronologyHtml);

            File.WriteAllText("index.html", finalIndexHtml);

            string whyPoetryText = File.ReadAllText("Other/Why Poetry.md");
            string whyPoetryHtml = md.Transform(whyPoetryText);
            whyPoetryHtml = contentTemplate.Replace("{{content}}", whyPoetryHtml);
            File.WriteAllText("Other/Why Poetry.html", whyPoetryHtml);

            string favoritePoemsText = File.ReadAllText("Other/Favorite Poems.md");
            string favoritePoemsHtml = md.Transform(favoritePoemsText);
            favoritePoemsHtml = contentTemplate.Replace("{{content}}", favoritePoemsHtml);
            File.WriteAllText("Other/Favorite Poems.html", favoritePoemsHtml);

            string faqText = File.ReadAllText("Other/FAQ.md");
            string faqHtml = md.Transform(faqText);
            faqHtml = contentTemplate.Replace("{{content}}", faqHtml);
            File.WriteAllText("Other/FAQ.html", faqHtml);
        }
    }
}
