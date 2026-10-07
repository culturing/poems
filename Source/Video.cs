using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Poems;

static class Video
{
    public static async Task Render()
    {
        Directory.CreateDirectory("Video");
        Directory.CreateDirectory("Output/Video");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync();
        IPage page = await browser.NewPageAsync();
        await page.GotoAsync("file:///" + Path.GetFullPath("Templates/video.html"));

        foreach (string dirpath in Directory.GetDirectories("Audio"))
        {
            string dir = Path.GetFileName(dirpath);
            Directory.CreateDirectory($"Video/{dir}");
            foreach (string filepath in Directory.GetFiles(dirpath))
            {
                string audioWav = Path.GetFullPath(filepath);
                string title = Path.GetFileNameWithoutExtension(filepath);
                string outpath = Path.GetFullPath($"Video/{dir}/{title}.mov");
                if (File.Exists(outpath))
                    continue;

                string titlePng = await RenderSplash(page, dir, title);

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

    static async Task<string> RenderSplash(IPage page, string dir, string title)
    {
        await page.EvaluateAsync($"document.querySelector('.title').innerHTML = '{title}'");

        var options = new PageScreenshotOptions
        {
            FullPage = true,
            Path = Path.GetFullPath($"Output/Video/{dir}/{title}.png")
        };
        await page.ScreenshotAsync(options);

        return options.Path;
    }
}
