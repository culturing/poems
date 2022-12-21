using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HeyRed.MarkdownSharp;
using IronPdf;
using IronPdf.Bookmarks;

namespace Poems
{
    class Poem
    {
        public string Title { get; set; }
        public int Index { get; set; }
        public string Link { get; set; }
        public DateTime PublicationDate { get; set; }
        public static List<string> Months = new List<string> { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        public string FilePath { get; set; }
    }

    class Program
    {
        static Markdown md = new Markdown();
        static string ContentTemplate = File.ReadAllText("Templates/content.html");
        static List<Poem> Poems { get; set; } = new List<Poem>();
        static Dictionary<string, List<Poem>> PoemsByDate = new Dictionary<string, List<Poem>>();
        static string RootUrl = "https://raw.githubusercontent.com/culturing/Poems/main/";
        static void Main(string[] args)
        {            
            Directory.Delete("Pages", true);
            Directory.CreateDirectory("Pages");
        
        // Parse Poems
            foreach (string dirpath in Directory.EnumerateDirectories("Poems"))
            foreach (string filepath in Directory.EnumerateFiles(dirpath))
            {
                AddPoem(filepath);
            }
            foreach (string filepath in Directory.EnumerateFiles("Poems"))
            {
                AddPoem(filepath);
            }

        // Build Index
            string indexHtml = string.Empty;
            foreach(Poem poem in Poems.OrderBy(p => p.Title))
            {
                indexHtml += $"<div>{poem.Link}</div>\n";
            }

        // Build Chronology
            string chronologyHtml = string.Empty;
            foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByDate.OrderByDescending(kvp => kvp.Key))
            {
                chronologyHtml += $"<h3>{kvp.Key}</h3>\n";
                foreach(Poem poem in kvp.Value)
                {
                    chronologyHtml += $"<div>{poem.Link}</div>\n";
                }
            }

            string finalIndexHtml = File.ReadAllText("Templates/index.html")
                .Replace("{{index}}", indexHtml)
                .Replace("{{chronology}}", chronologyHtml);

            File.WriteAllText("index.html", finalIndexHtml);

            RenderOtherPage("Other/Why Poetry.md");
            RenderOtherPage("Other/Favorite Poems.md");
            RenderOtherPage("Other/FAQ.md");

        // Render Pdf
            // ChromePdfRenderer renderer = GetPdfRenderer();
            // PdfDocument pdf = renderer.RenderHtmlFileAsPdf("index.html");
            // pdf.Bookmarks.AddBookMarkAtStart("Contents", 0);

            // IPdfBookMark currentMonthBookmark = null;
            // foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByMonth)
            // {
            //     currentMonthBookmark = pdf.Bookmarks.AddBookMarkAtEnd(kvp.Key, pdf.PageCount);
            //     foreach(Poem poem in kvp.Value)
            //     {
            //         PdfDocument poemPdf = renderer.RenderHtmlFileAsPdf(poem.FilePath);
            //         string bookmarkName = Path.GetFileNameWithoutExtension(poem.FilePath);
            //         currentMonthBookmark.Children.AddBookMarkAtEnd(bookmarkName, pdf.PageCount);
            //         pdf.AppendPdf(poemPdf);            
            //     }
            // }
    
            // pdf.SaveAs("Poems.pdf");
        }

        static void AddPoem(string filepath)
        {
            var poem = new Poem();
            string filename = Path.GetFileNameWithoutExtension(filepath);
            string[] lines = File.ReadAllLines(filepath);
            
            filename = Regex.Replace(filename, "^[0-9][0-9] ", "");
            poem.Title = filename;
            
            try
            {
                poem.PublicationDate = System.DateTime.Parse(lines[1]);
                poem.Title = lines[0];
                lines[0] = $"### {lines[0]}";
                lines[1] = $"<p style='margin:0; margin-top: -1.25rem'><em><small><small>{lines[1]}</small></small></em></p>";                
            }
            catch(FormatException)
            {
                poem.PublicationDate = System.DateTime.Parse(lines[0]);
                lines[0] = $"<p style='margin:0; margin-top: -1.25rem'><em><small><small>{lines[0]}</small></small></em></p>";                
            }
            
            string content = String.Join("  \n", lines);
            string contentHtml = md.Transform(content);

            // if (File.Exists($"Audio/{filename}.m4a"))
            // {
            //     contentHtml += $"<audio controls src='{rootUrl}/Audio/{filename}.m4a' style='width:100%'></audio>";
            // }

            string finalPoemHtml = ContentTemplate.Replace("{{content}}", contentHtml);

            string finalPath = $"Pages/{filename}.html";
            File.WriteAllText(finalPath, finalPoemHtml);

            poem.Link = $"<a href=\"{finalPath}\">{filename}</a>";
            poem.FilePath = finalPath;

            Poems.Add(poem);

            string key = poem.PublicationDate.ToString("yyyy-MM-dd");
            if (!PoemsByDate.ContainsKey(key))
                PoemsByDate[key] = new List<Poem>();
            PoemsByDate[key].Add(poem);
        }

        static void RenderOtherPage(string filepath)
        {
            string text = File.ReadAllText(filepath);
            string html = ContentTemplate.Replace("{{content}}", md.Transform(text));
            string htmlpath = filepath.Replace(Path.GetExtension(filepath), ".html");
            File.WriteAllText(htmlpath, html);   
        }

        static ChromePdfRenderer GetPdfRenderer()
        {
            ChromePdfRenderer renderer = new ChromePdfRenderer();
            renderer.RenderingOptions.CssMediaType = IronPdf.Rendering.PdfCssMediaType.Print;
            renderer.RenderingOptions.MarginTop = 12;
            renderer.RenderingOptions.MarginBottom = 12;
            renderer.RenderingOptions.MarginRight = 12;
            renderer.RenderingOptions.MarginLeft = 12;
            return renderer;
        }
    }
}
