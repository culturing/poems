using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Poems;

static class PdfBook
{
    static readonly XFont Font = new XFont("Alegreya", 12.0);

    public static async Task Render(string outpath, List<Poem> poems, bool bestOnly = false, DateTime start = default, DateTime end = default)
    {
        if (File.Exists(outpath))
            File.Delete(outpath);

        Directory.CreateDirectory("docs/pdf");
        foreach (string file in Directory.EnumerateFiles("Templates/pdf"))
            File.Copy(file, $"docs/pdf/{Path.GetFileName(file)}", true);

        string year = DateTime.Now.ToString("yyyy");
        File.WriteAllText("docs/pdf/copyright.html", Templates.PdfCopyright.Replace("{{year}}", year));
        File.WriteAllText("docs/pdf/epigraph.html", Templates.PdfEpigraph.Replace("{{year}}", year));

        if (start == default)
            start = DateTime.MinValue;
        if (end == default)
            end = DateTime.MaxValue;

        List<Poem> included = poems
            .Where(poem => poem.PublicationDate > start && poem.PublicationDate < end && (!bestOnly || poem.Bold))
            .ToList();
        List<IGrouping<DateTime, Poem>> days = included
            .GroupBy(poem => poem.PublicationDate.Date)
            .OrderBy(day => day.Key)
            .ToList();
        List<Poem> flatPoems = days.SelectMany(day => day).ToList();

        using PdfDocument pdf = new PdfDocument();

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Args = new List<string> { "--disable-export-tagged-pdf" }
        });
        IPage page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = "http://127.0.0.1:5500" });

        await AppendPage(page, pdf, "/pdf/title.html", "Title");
        await AppendPage(page, pdf, "/pdf/copyright.html", "Copyright");
        await AppendPage(page, pdf, "/pdf/epigraph.html", "Epigraph");
        await AppendPage(page, pdf, "/about/index.html", "About");

        // A placeholder, replaced once the poems' page numbers are known
        int tableOfContentsStart = pdf.PageCount;
        using (PdfDocument tableOfContentsPdf = PdfReader.Open(await RenderTableOfContents(page, days), PdfDocumentOpenMode.Import))
            MergePdfs(tableOfContentsPdf, pdf);

        PdfOutline contentsOutline = pdf.Outlines.Add("Contents", pdf.Pages[tableOfContentsStart]);
        PdfOutline poemsOutline = pdf.Outlines.Add("Poems", pdf.Pages[pdf.PageCount - 1]);

        File.WriteAllText("docs/pdf/all_poems.html", AllPoemsHtml(flatPoems));

        // Set before the navigation, so the book is laid out once and in print media only
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });

        await page.GotoAsync("/pdf/all_poems.html");

        // Removed, not hidden: Chromium repeats position: fixed elements on every printed page
        await page.EvaluateAsync(@"() => {
            const elements = document.querySelectorAll('.navbar, .edge, img[src*=\'cc.png\'], a[href*=\'creativecommons\']');
            elements.forEach(el => el.remove());
        }");

        int[] pageSpans = await page.EvaluateAsync<int[]>(@"() => {
            let counts =[];
            let elements = document.querySelectorAll('.poem-page');
            for (let el of elements) {
                let style = window.getComputedStyle(el);
                let mt = parseFloat(style.marginTop) || 0;
                let mb = parseFloat(style.marginBottom) || 0;
                let height = el.getBoundingClientRect().height + mt + mb;

                let pages = Math.ceil(height / 864); // 9in of printable height at 96px an inch
                if (pages === 0) pages = 1;
                counts.push(pages);
            }
            return counts;
        }");

        int currentPoemPage = pdf.PageCount + 1;
        for (int i = 0; i < flatPoems.Count; i++)
        {
            flatPoems[i].Page = currentPoemPage;
            currentPoemPage += pageSpans[i];
        }

        await page.PdfAsync(PdfOptions("Output/Pdfs/AllPoems.pdf"));

        using (PdfDocument poemsPdf = PdfReader.Open("Output/Pdfs/AllPoems.pdf", PdfDocumentOpenMode.Import))
        {
            MergePdfs(poemsPdf, pdf);
            foreach (Poem poem in flatPoems)
                poemsOutline.Outlines.Add(poem.Title, pdf.Pages[poem.Page - 1]);
        }

        using (PdfDocument tableOfContentsPdf = PdfReader.Open(await RenderTableOfContents(page, days), PdfDocumentOpenMode.Import))
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

        using (PdfDocument indexPdf = PdfReader.Open(await RenderIndex(page, included), PdfDocumentOpenMode.Import))
        {
            MergePdfs(indexPdf, pdf);
            pdf.Outlines.Add("Index", pdf.Pages[pdf.PageCount - indexPdf.PageCount]);
        }

        pdf.Save("Output/Pdfs/culturing.pdf");

        List<string> args = new List<string>
        {
            "-sDEVICE=pdfwrite",
            "-dCompatibilityLevel=1.5",
            "-dEmbedAllFonts=false",
            "-dSubsetFonts=false",
            "-dQUIET",
            $"-o {outpath}",
            "Output/Pdfs/culturing.pdf"
        };

        using (Process process = Process.Start("gswin64c", string.Join(" ", args)))
        {
            process.WaitForExit();
        }

        if (Directory.Exists("docs/pdf"))
            Directory.Delete("docs/pdf", true);
    }

    static PagePdfOptions PdfOptions(string path) => new PagePdfOptions
    {
        Path = path,
        Margin = new Margin { Left = "1in", Top = "1in", Right = "1in", Bottom = "1in" },
        Tagged = false
    };

    static async Task<string> Print(IPage page, string url, string pdfName)
    {
        string pdfPath = $"Output/Pdfs/{pdfName}.pdf";
        await page.GotoAsync(url);
        await page.PdfAsync(PdfOptions(pdfPath));
        return pdfPath;
    }

    static async Task AppendPage(IPage page, PdfDocument pdf, string url, string name)
    {
        int start = pdf.PageCount;
        using (PdfDocument section = PdfReader.Open(await Print(page, url, name), PdfDocumentOpenMode.Import))
            MergePdfs(section, pdf);
        pdf.Outlines.Add(name, pdf.Pages[start]);
    }

    static string AllPoemsHtml(List<Poem> poems)
    {
        StringBuilder html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>\n<html>\n<head>\n<meta charset='utf-8'/>");

        int headStart = Templates.Content.IndexOf("<head>", StringComparison.OrdinalIgnoreCase) + 6;
        int headEnd = Templates.Content.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headStart >= 6 && headEnd > headStart)
        {
            // Unsubstituted tokens would land in <head> as bare text, which ends the head early
            string head = Templates.Content.Substring(headStart, headEnd - headStart);
            html.AppendLine(Regex.Replace(head, @"\{\{\w+\}\}", ""));
        }
        html.AppendLine("<style>.poem-page { display: flex; flex-direction: column; justify-content: center; min-height: 100vh; page-break-after: always; break-after: page; box-sizing: border-box; } body { margin: 0; padding: 0; }</style>");
        html.AppendLine("</head>\n<body>");

        for (int i = 0; i < poems.Count; i++)
        {
            string poemHtml = File.ReadAllText(poems[i].FilePath);
            int bodyStart = poemHtml.IndexOf("<body>", StringComparison.OrdinalIgnoreCase) + 6;
            int bodyEnd = poemHtml.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);

            if (bodyStart >= 6 && bodyEnd > bodyStart)
            {
                html.AppendLine($"<div class='poem-page' id='poem_{i}'>");
                html.AppendLine(poemHtml.Substring(bodyStart, bodyEnd - bodyStart));
                html.AppendLine("</div>");
            }
        }
        html.AppendLine("</body>\n</html>");
        return html.ToString();
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

    static async Task<string> RenderTableOfContents(IPage page, List<IGrouping<DateTime, Poem>> days)
    {
        var toc = new StringBuilder();
        foreach (IGrouping<DateTime, Poem> day in days)
        {
            toc.Append("<div class='toc-section'>");
            toc.Append("  <div class='toc-flex toc-section-header'>");
            toc.Append($"    <span>{day.Key:dd} {Site.Months[day.Key.Month]} {day.Key.Year}</span>");
            toc.Append($"    <span class='toc-page'>{day.First().Page}</span>");
            toc.Append("  </div>");
            foreach (Poem poem in day)
            {
                toc.Append("  <div class='toc-flex toc-poem'>");
                toc.Append($"    <span class='{poem.RatingClass}'>{poem.Title}</span>");
                toc.Append($"    <span class='toc-page'>{poem.Page}</span>");
                toc.Append("  </div>");
            }
            toc.Append("</div>");
        }

        File.WriteAllText("docs/pdf/TableOfContents.html", Templates.PdfTableOfContents.Replace("{{toc}}", toc.ToString()));
        return await Print(page, "/pdf/TableOfContents.html", "TableOfContents");
    }

    static async Task<string> RenderIndex(IPage page, List<Poem> poems)
    {
        var index = new StringBuilder();
        foreach (Poem poem in poems.OrderBy(poem => Markup.SortKey(poem.Title)))
        {
            index.Append("<div class='toc-flex toc-poem'>");
            index.Append($"  <span class='{poem.RatingClass}'>{poem.Title}</span>");
            index.Append($"  <span class='toc-page'>{poem.Page}</span>");
            index.Append("</div>");
        }

        File.WriteAllText("docs/pdf/index.html", Templates.PdfIndex.Replace("{{index}}", index.ToString()));
        return await Print(page, "/pdf/index.html", "Index");
    }
}
