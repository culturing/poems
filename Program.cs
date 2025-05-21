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
using System.Security.Cryptography;

namespace Poems;

class Poem
{
    public string Title { get; set; }
    public string Link { get; set; }
    public bool Bold { get; set; } = false;
    public DateTime PublicationDate { get; set; }
    public static List<string> Months = new List<string> { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    public string FilePath { get; set; }
    public string FileName => Path.GetFileName(Path.GetDirectoryName(FilePath));
    public string UrlPath => $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{FileName}";
    public int Page { get; set; }
    public string Style(bool bestOnly = false)
    {
        string style = string.Empty;
        if (Bold && !bestOnly)
            style += "font-weight: bold;";
        return style;
    }
    public Dictionary<string, string> Variables { get; set; } = new();

    public string GetHash()
    {
        string contents = File.ReadAllText(FilePath);
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(contents));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

class Program
{
    static public string BaseUrl = "https://poems.culturing.net";
    static Markdown md = new Markdown();
    static string IndexTemplate = File.ReadAllText("Templates/index.html");
    static string BestTemplate = File.ReadAllText("Templates/best.html");
    static string ContentTemplate = File.ReadAllText("Templates/content.html");
    static string FaqTemplate = File.ReadAllText("Templates/faq.html");
    static string PdfCopyrightTemplate = File.ReadAllText("Templates/pdf/copyright.html");
    static string PdfTableOfContentsTemplate = File.ReadAllText("Templates/pdf/toc.html");
    static string PdfIndexTemplate = File.ReadAllText("Templates/pdf/index.html");
    static List<Poem> Poems { get; set; } = new List<Poem>();
    static Dictionary<string, List<Poem>> PoemsByDate = new Dictionary<string, List<Poem>>();
    static Dictionary<string, IEnumerable<Poem>> FilteredPoemsByDate;
    static XFont Font = new XFont("Quattrocento", 12.0);

    static async Task Main(string[] args)
    {            
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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

    // Build Index
        string indexHtml = string.Empty;
        string bestIndexHtml = string.Empty;
        foreach(Poem poem in Poems.OrderBy(p => Regex.Replace(p.Title, @"[^\w\s]", "")))
        {
            indexHtml += $"<div style='{poem.Style()}'>{poem.Link}</div>\n";
            if (poem.Bold)
                bestIndexHtml += $"<div>{poem.Link}</div>\n";
        }

    // Build Chronology
        string chronologyHtml = string.Empty;
        string bestChronologyHtml = string.Empty;
        foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByDate.OrderByDescending(kvp => DateTime.Parse(kvp.Key)))
        {
            chronologyHtml += $"<h3>{kvp.Key}</h3>\n";
            if (kvp.Value.Any(poem => poem.Bold))
                bestChronologyHtml += $"<h3>{kvp.Key}</h3>\n";

            foreach(Poem poem in Enumerable.Reverse(kvp.Value))
            {
                chronologyHtml += $"<div style='{poem.Style()}'>{poem.Link}</div>\n";                    
                if (poem.Bold)
                    bestChronologyHtml += $"<div>{poem.Link}</div>\n";
            }
        }

    // Set previous and next links
        List<Poem> OrderedPoems = Poems.OrderBy(poem => poem.PublicationDate).ToList();
        for (int i = 0; i < OrderedPoems.Count; ++i)
        {
            Poem poem = OrderedPoems[i];
            string previousPath = string.Empty;
            string nextPath = string.Empty;
            if (i > 0)
            {
                Poem prev = OrderedPoems[i-1];
                previousPath = prev.UrlPath;
            }
            if (i < OrderedPoems.Count - 1)
            {
                Poem next = OrderedPoems[i+1];
                nextPath = next.UrlPath;
            }
            string contents = File.ReadAllText(poem.FilePath);
            contents = contents.Replace("{{previous}}", previousPath);
            contents = contents.Replace("{{next}}", nextPath);
            File.WriteAllText(poem.FilePath, contents);
        }

        string finalIndexHtml = IndexTemplate
            .Replace("{{index}}", indexHtml)
            .Replace("{{chronology}}", chronologyHtml);

        File.WriteAllText("docs/index.html", finalIndexHtml);

        string finalBestIndexHtml = BestTemplate
            .Replace("{{index}}", bestIndexHtml)
            .Replace("{{chronology}}", bestChronologyHtml);

        Directory.CreateDirectory("docs/best");
        File.WriteAllText("docs/best/index.html", finalBestIndexHtml);

        // RenderOtherPage("Other/FAQ.md");
        // RenderOtherPage("Other/Favorite Poems.md");
        // RenderOtherPage("Other/Why Poetry.md");

        await RenderPdf("docs/culturing.pdf");
        //await RenderPdf("Submission.pdf", true, new DateTime(2021, 02, 01), new DateTime(2022, 10, 31));
        await RenderVideo();
        GenerateSitemap();

        CopyFilesToDocs();
    }

    static void AddPoem(string filepath)
    {
        var poem = new Poem();
        string filename = Path.GetFileNameWithoutExtension(filepath);
        string[] lines = File.ReadAllLines(filepath);
        
        filename = Regex.Replace(filename, "^[0-9][0-9] ", "");
        poem.Title = filename;

        if (lines[0].StartsWith("*"))
        {
            poem.Bold = true;
            lines[0] = lines[0].Substring(1);
        }
        
        try
        {
            poem.PublicationDate = System.DateTime.Parse(lines[1]);
            poem.Title = lines[0];
            lines[0] = $"### {lines[0]}";
            lines[1] = $"<p style='margin:0;'><em><small><small>{lines[1]}</small></small></em></p>";                
        }
        catch(FormatException)
        {
            poem.PublicationDate = System.DateTime.Parse(lines[0]);
            lines[0] = $"<p style='margin:0;'><em><small><small>{lines[0]}</small></small></em></p>";                
        }

        string dirPath = $"docs/{poem.PublicationDate.ToString("yyyy")}/{poem.PublicationDate.ToString("MM")}";
        
        string content = String.Join("  \n", lines);
        string contentHtml = md.Transform(content);
        string finalPoemHtml = ContentTemplate.Replace("{{content}}", contentHtml).Replace("{{title}}", poem.Title);
        string finalFileName = Regex.Replace(filename.ToLower().Replace(" ", "-"), @"[^0-9a-zA-Z\-]", "");

        dirPath += $"/{finalFileName}";
        Directory.CreateDirectory(dirPath);        
        string finalPath = $"{dirPath}/index.html";
        File.WriteAllText(finalPath, finalPoemHtml);

        poem.FilePath = finalPath;
        poem.Link = $"<a href=\"{poem.UrlPath}\">{poem.Title}</a>";

        Poems.Add(poem);

    // Sort for chronology
        string key = $"{Poem.Months[poem.PublicationDate.Month]} {poem.PublicationDate.Year}";
        if (!PoemsByDate.ContainsKey(key))
            PoemsByDate[key] = new List<Poem>();
        PoemsByDate[key].Add(poem);
    }

    static void RenderOtherPage(string filepath)
    {
        string text = File.ReadAllText(filepath);
        string html = FaqTemplate.Replace("{{content}}", md.Transform(text));
        string htmlpath = $"Output/Other/{Path.GetFileNameWithoutExtension(filepath)}.html";
        File.WriteAllText(htmlpath, html);   
    }

    static async Task RenderPdf(string outpath, bool bestOnly = false, DateTime start = default, DateTime end = default)
    {
        if (File.Exists(outpath))
            return; 

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
        await using IBrowser browser = await playwright.Chromium.LaunchAsync();
        IPage page = await browser.NewPageAsync();
        PagePdfOptions pdfRenderOptions = new PagePdfOptions 
        { 
            Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
        };

    // Add title
        pdfRenderOptions.Path = $"Output/Pdfs/Title.pdf";
        await page.GotoAsync("file:///" + Path.GetFullPath($"Templates/pdf/title.html"));                    
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument titlePdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(titlePdf, pdf);
        }
        pdf.Outlines.Add("Title", pdf.Pages[pdf.PageCount - 1]);

    // Add copyright
        string copyrightHtml = PdfCopyrightTemplate.Replace("{{year}}", DateTime.Now.ToString("yyyy"));
        string copyrightPath = $"Output/Pdfs/Copyright.html";
        File.WriteAllText(copyrightPath, copyrightHtml);

        pdfRenderOptions.Path = $"Output/Pdfs/Copyright.pdf";
        await page.GotoAsync("file:///" + Path.GetFullPath(copyrightPath));                    
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument copyrightPdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(copyrightPdf, pdf);
        }
        pdf.Outlines.Add("Copyright", pdf.Pages[pdf.PageCount - 1]);
        
    // Add temporary table of contents
        int tableOfContentsStart = pdf.PageCount;
        int tableOfContentsPageCount = 0;
        await RenderTableOfContents(page, bestOnly);
        using (PdfDocument tableOfContentsPdf = PdfReader.Open("Output/Pdfs/TableOfContents.pdf", PdfDocumentOpenMode.Import))
        {
            tableOfContentsPageCount = tableOfContentsPdf.PageCount;
            MergePdfs(tableOfContentsPdf, pdf);
        }

    // Add poems
        PdfOutline contentsOutline = pdf.Outlines.Add("Contents", pdf.Pages[tableOfContentsStart]);
        PdfOutline poemsOutline = pdf.Outlines.Add("Poems", pdf.Pages[pdf.PageCount - 1]);
        foreach(KeyValuePair<string, IEnumerable<Poem>> kvp in FilteredPoemsByDate.OrderBy(kvp => DateTime.Parse(kvp.Key)))
        {
            IEnumerable<Poem> poems = kvp.Value.Where(poem => poem.PublicationDate > start && poem.PublicationDate < end);
            if (bestOnly)
                poems = poems.Where(poem => poem.Bold);

            Directory.CreateDirectory($"Output/Pdfs/{kvp.Key}");
            foreach(Poem poem in poems)
            {                    
                pdfRenderOptions.Path = $"Output/Pdfs/{kvp.Key}/{poem.FileName}.pdf";
                await page.GotoAsync("file:///" + Path.GetFullPath(poem.FilePath));                    
                await page.PdfAsync(pdfRenderOptions);

                using (PdfDocument poemPdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
                {
                    poem.Page = pdf.PageCount + 1;
                    MergePdfs(poemPdf, pdf);
                    poemsOutline.Outlines.Add(poem.Title, pdf.Pages[pdf.PageCount - poemPdf.PageCount]);
                }
            }
        }

    // Render final table of contents
        string tocPath = await RenderTableOfContents(page, bestOnly);
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

        pdf.Save(outpath);
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

    static async Task<string> RenderTableOfContents(IPage page, bool bestOnly)
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
                toc += $"    <span style='{poem.Style(bestOnly)}'>{poem.Title}</span>";
                toc += $"    <span class='toc-page'>{poem.Page}</span>";
                toc += $"  </div>";
            }
            toc += $"</div>";
        }

        string tocHtml = PdfTableOfContentsTemplate.Replace("{{toc}}", toc);
        string filepath = "Output/Pdfs/TableOfContents.html";
        File.WriteAllText(filepath, tocHtml);

        await page.GotoAsync("file:///" + Path.GetFullPath(filepath));

        string pdfPath = "Output/Pdfs/TableOfContents.pdf";
        PagePdfOptions options = new PagePdfOptions 
        { 
            Path = pdfPath,
            Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
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
                index += $"  <span style='{poem.Style(bestOnly)}'>{poem.Title}</span>";
            index += $"  <span class='toc-page'>{poem.Page}</span>";
            index += $"</div>";
            }
        }

        string indexHtml = PdfIndexTemplate.Replace("{{index}}", index);
        string filepath = "Output/Pdfs/Index.html";
        File.WriteAllText(filepath, indexHtml);

        await page.GotoAsync("file:///" + Path.GetFullPath(filepath));

        string pdfPath = "Output/Pdfs/Index.pdf";
        PagePdfOptions options = new PagePdfOptions 
        { 
            Path = pdfPath,
            Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
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

        string xmlString = SitemapGenerator.GenerateXmlString(Poems);
        File.WriteAllText(filepath, xmlString, Encoding.UTF8);
    } 

    static void CopyFilesToDocs()
    {
        List<string> filesToCopy = new List<string>();
        filesToCopy.Add("robots.txt");
        filesToCopy.Add("google84fcfca997bbbdc0.html");
        filesToCopy.Add("favicon.ico");
        filesToCopy.AddRange(Directory.GetFiles("Styles"));
        filesToCopy.AddRange(Directory.GetFiles("Scripts"));
        
        foreach(string file in filesToCopy)
        {
            File.Copy(file, $"docs/{Path.GetFileName(file)}");
        }
    }   

    static void CleanupPrevious()
    {
        if (Directory.Exists("docs"))
        {                   
            foreach(string dir in Directory.GetDirectories("docs"))
            {
                Directory.Delete(dir, true);
            }

            string[] exclude = ["culturing.pdf", "CNAME"];
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