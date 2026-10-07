using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Poems;

static class Templates
{
    // Declared ahead of the templates, so it is initialised before VersionAssets runs over them
    static readonly Regex AssetReference =
        new Regex(@"(?<=(?:href|src)=[""'])/(?<file>[\w.-]+\.(?:css|js))(?=[""'])", RegexOptions.Compiled);

    public static readonly string Index = VersionAssets(File.ReadAllText("Templates/index.html"));
    public static readonly string About = VersionAssets(File.ReadAllText("Templates/about.html"));
    public static readonly string Content = VersionAssets(File.ReadAllText("Templates/content.html"));
    public static readonly string Archive = VersionAssets(File.ReadAllText("Templates/archive.html"));
    public static readonly string Redirect = VersionAssets(File.ReadAllText("Templates/redirect.html"));
    public static readonly string NotFound = VersionAssets(File.ReadAllText("Templates/404.html"));
    public static readonly string Navbar = VersionAssets(File.ReadAllText("Templates/navbar.html"));
    public static readonly string PdfCopyright = File.ReadAllText("Templates/pdf/copyright.html");
    public static readonly string PdfEpigraph = File.ReadAllText("Templates/pdf/epigraph.html");
    public static readonly string PdfTableOfContents = File.ReadAllText("Templates/pdf/toc.html");
    public static readonly string PdfIndex = File.ReadAllText("Templates/pdf/index.html");

    // Fonts are left out on purpose; see DESIGN.md
    static string VersionAssets(string html) => AssetReference.Replace(html, match =>
    {
        string name = match.Groups["file"].Value;
        string source = File.Exists($"Styles/{name}") ? $"Styles/{name}" : $"Scripts/{name}";
        // review.js is absent outside the review pass, and the fingerprint buys it nothing there
        return File.Exists(source) ? $"/{name}?v={AssetHash(source)}" : match.Value;
    });

    static string AssetHash(string path)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path))).Substring(0, 8).ToLowerInvariant();
    }
}
