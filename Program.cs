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
        public static List<string> Months = new List<string> { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "Novermber", "December" };
    }

    class Program
    {
        static void Main(string[] args)
        {
            var md = new Markdown();

            List<Poem> poems = new List<Poem>();

            // Generate html
            string poemTemplate = File.ReadAllText("Templates/poem.html");
            string indexHtml = string.Empty;
            foreach (var file in Directory.EnumerateFiles("Poems"))
            {
                string content = File.ReadAllText(file);
                string contentHtml = md.Transform(content);
                string finalPoemHtml = poemTemplate.Replace("{{poem}}", contentHtml);

                string filename = Path.GetFileNameWithoutExtension(file);
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
        }
    }
}
