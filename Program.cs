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

namespace Poems;

class Poem
{
    public string Title { get; set; }
    public string Link { get; set; }
    public bool Bold { get; set; } = false;
    public DateTime PublicationDate { get; set; }
    public static List<string> Months = new List<string> { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    public string FilePath { get; set; }
    public int Page { get; set; }
    public string Style()
    {
        if (string.IsNullOrEmpty(_Style))
        {
            _Style = string.Empty;
            if (Bold)
                _Style += "font-weight: bold;";
        }
        return _Style;
    }

    protected string _Style;
}

class Program
{
    static Markdown md = new Markdown();
    static string IndexTemplate = File.ReadAllText("Templates/index.html");
    static string BestTemplate = File.ReadAllText("Templates/best.html");
    static string ContentTemplate = File.ReadAllText("Templates/content.html");
    static string OtherTemplate = File.ReadAllText("Templates/other.html");
    static string TableOfContentsTemplate = File.ReadAllText("Templates/toc.html");
    static string PdfIndexTemplate = File.ReadAllText("Templates/pdf-index.html");
    static List<Poem> Poems { get; set; } = new List<Poem>();
    static Dictionary<string, List<Poem>> PoemsByDate = new Dictionary<string, List<Poem>>();
    static XFont Font = new XFont("Quattrocento", 12.0);

    static async Task Main(string[] args)
    {            
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Directory.Delete("Output", true);
        Directory.CreateDirectory("Output/Poems");  
        Directory.CreateDirectory("Output/Pdfs");  
        Directory.CreateDirectory("Output/Other");  
    
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
                previousPath = Path.GetFileName(prev.FilePath);
            }
            if (i < OrderedPoems.Count - 1)
            {
                Poem next = OrderedPoems[i+1];
                nextPath = Path.GetFileName(next.FilePath);
            }
            string contents = File.ReadAllText(poem.FilePath);
            contents = contents.Replace("{{previous}}", previousPath);
            contents = contents.Replace("{{next}}", nextPath);
            File.WriteAllText(poem.FilePath, contents);
        }

        string finalIndexHtml = IndexTemplate
            .Replace("{{index}}", indexHtml)
            .Replace("{{chronology}}", chronologyHtml);

        File.WriteAllText("index.html", finalIndexHtml);

        string finalBestIndexHtml = BestTemplate
            .Replace("{{index}}", bestIndexHtml)
            .Replace("{{chronology}}", bestChronologyHtml);

        File.WriteAllText("best.html", finalBestIndexHtml);

        RenderOtherPage("Other/FAQ.md");
        RenderOtherPage("Other/Favorite Poems.md");
        RenderOtherPage("Other/Why Poetry.md");

        //await RenderPdf("Poems.pdf");
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
        
        string content = String.Join("  \n", lines);
        string contentHtml = md.Transform(content);
        string finalPoemHtml = ContentTemplate.Replace("{{content}}", contentHtml);
        string finalPath = $"Output/Poems/{filename}.html";
        File.WriteAllText(finalPath, finalPoemHtml);

        poem.Link = $"<a href=\"{finalPath}\">{poem.Title}</a>";
        poem.FilePath = finalPath;

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
        string html = OtherTemplate.Replace("{{content}}", md.Transform(text));
        string htmlpath = $"Output/Other/{Path.GetFileNameWithoutExtension(filepath)}.html";
        File.WriteAllText(htmlpath, html);   
    }

    static async Task RenderPdf(string outpath)
    {
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
        await page.GotoAsync("file:///" + Path.GetFullPath($"Templates/title.html"));                    
        await page.PdfAsync(pdfRenderOptions);
        using (PdfDocument titlePdf = PdfReader.Open(pdfRenderOptions.Path, PdfDocumentOpenMode.Import))
        {
            MergePdfs(titlePdf, pdf);
        }
        pdf.Outlines.Add("Title", pdf.Pages[pdf.PageCount - 1]);

    // Add copyright
        string copyrightHtml = File.ReadAllText("Templates/copyright.html")
            .Replace("{{year}}", DateTime.Now.ToString("yyyy"));
        string copyrightPath = $"Output/Other/Copyright.html";
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
        await RenderTableOfContents(page);
        using (PdfDocument tableOfContentsPdf = PdfReader.Open("Output/Pdfs/TableOfContents.pdf", PdfDocumentOpenMode.Import))
        {
            tableOfContentsPageCount = tableOfContentsPdf.PageCount;
            MergePdfs(tableOfContentsPdf, pdf);
        }

    // Add poems
        PdfOutline contentsOutline = pdf.Outlines.Add("Contents", pdf.Pages[tableOfContentsStart]);
        PdfOutline poemsOutline = pdf.Outlines.Add("Poems", pdf.Pages[pdf.PageCount - 1]);
        foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByDate.OrderBy(kvp => DateTime.Parse(kvp.Key)))
        {
            Directory.CreateDirectory($"Output/Pdfs/{kvp.Key}");
            foreach(Poem poem in kvp.Value)
            {                    
                pdfRenderOptions.Path = $"Output/Pdfs/{kvp.Key}/{Path.GetFileNameWithoutExtension(poem.FilePath)}.pdf";
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
        string indexPath = await RenderPdfIndex(page);
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

    static async Task<string> RenderTableOfContents(IPage page)
    {
        string toc = string.Empty;
        foreach(KeyValuePair<string, List<Poem>> kvp in PoemsByDate.OrderBy(kvp => DateTime.Parse(kvp.Key)))
        {
            toc += $"<div class='toc-section'>";
            toc += $"  <div class='toc-flex toc-section-header'>";
            toc += $"    <span>{kvp.Key}</span>";
            toc += $"    <span class='toc-page'>{kvp.Value.FirstOrDefault()?.Page}</span>";
            toc += $"  </div>";
            foreach(Poem poem in kvp.Value)
            {
                toc += $"  <div class='toc-flex toc-poem'>";
                toc += $"    <span style='{poem.Style()}'>{poem.Title}</span>";
                toc += $"    <span class='toc-page'>{poem.Page}</span>";
                toc += $"  </div>";
            }
            toc += $"</div>";
        }

        string tocHtml = TableOfContentsTemplate.Replace("{{toc}}", toc);
        string filepath = "Output/Other/TableOfContents.html";
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

    static async Task<string> RenderPdfIndex(IPage page)
    {
        string index = string.Empty;
        foreach(Poem poem in Poems.OrderBy(p => Regex.Replace(p.Title, @"[^\w\s]", "")))
        {
            index += $"<div class='toc-flex toc-poem'>";
            index += $"  <span style='{poem.Style()}'>{poem.Title}</span>";
            index += $"  <span class='toc-page'>{poem.Page}</span>";
            index += $"</div>";
        }

        string indexHtml = PdfIndexTemplate.Replace("{{index}}", index);
        string filepath = "Output/Other/PdfIndex.html";
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
}
