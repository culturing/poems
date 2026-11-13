#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

// The rating and text editor behind the widget in Scripts/review.js. Whatever serves docs/ only
// reads it, so this edits the source files under Poems/ itself. build -r starts it; by hand, run
//
//     dotnet run --file Tools/review-server.cs

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const int Port = 5501;
string root = Path.GetDirectoryName(ScriptDirectory())!;
string manifestPath = Path.Combine(root, "Output", "review-map.json");
string progressPath = Path.Combine(root, "Tools", "review-progress.json");
string tagsPath = Path.Combine(root, "Other", "tags.tsv");

Lock gate = new();
Manifest manifest = new(0, false, []);
Dictionary<string, ManifestPoem> byUrl = [];
DateTime manifestWritten = default;
Dictionary<string, DateTime> textSavedAt = [];
Rebuild rebuild = new(root);

try
{
    LoadManifest();
}
catch (MissingManifest e)
{
    Console.WriteLine(e.Message);
    return 1;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

WebApplication app = builder.Build();
app.UseCors();
app.Use((context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return next();
});

app.MapGet("/api/poem", (string? url) => Guarded(() =>
    State(url ?? "") is PoemState state ? Results.Json(state) : Error(404, $"no poem at {url}")));

app.MapPost("/api/rating", (RatingRequest request) => Guarded(() =>
{
    if (request.Url is null || !byUrl.TryGetValue(request.Url, out ManifestPoem? poem))
        return Error(404, $"no poem at {request.Url}");
    if (request.Rating is not int rating || rating < 0 || rating > manifest.Max)
        return Error(400, $"rating must be 0..{manifest.Max}");

    bool changed = PoemFile.SetRating(Path.Combine(root, poem.Src), rating);
    Dictionary<string, int> progress = LoadProgress();
    progress[poem.Url] = rating;
    SaveProgress(progress);

    Console.WriteLine($"  {Mark(rating),-3} {poem.Title}" + (changed ? "" : "   (unchanged)"));
    return Results.Json(State(poem.Url, changed));
}));

app.MapGet("/api/text", (string? url) => Guarded(() =>
    byUrl.TryGetValue(url ?? "", out ManifestPoem? poem)
        ? Results.Json(new { text = PoemFile.Text(Path.Combine(root, poem.Src)) })
        : Error(404, $"no poem at {url}")));

app.MapPost("/api/text", (TextRequest request) => Guarded(() =>
{
    if (!manifest.Edit)
        return Error(403, "text is editable only after build -re");
    if (request.Url is null || !byUrl.TryGetValue(request.Url, out ManifestPoem? poem))
        return Error(404, $"no poem at {request.Url}");

    string path = Path.Combine(root, poem.Src);
    string[] before = PoemFile.Text(path).Split('\n');
    string[] after = (request.Text ?? "").ReplaceLineEndings("\n").Split('\n');
    if (after.Length < 2 || after[0].Trim().Length == 0)
        return Error(400, "the first line must be the title");
    if (after[0].StartsWith('*'))
        return Error(400, "a title cannot open with *; the rating buttons set those");
    if (after[1] != before[1])
        return Error(400, "the date line cannot change here"); // The date fixes the url's directory, which UrlFor keeps

    bool changed = PoemFile.SetText(path, request.Text!);
    string url = UrlFor(poem.Url, after[0]);
    if (changed)
    {
        string old = UrlFor(poem.Url, before[0]);
        if (url != old)
            Tags.Rekey(tagsPath, old, url);
        Console.WriteLine($"  edit {after[0]}" + (url != old ? $"   ({old} -> {url})" : ""));
        textSavedAt[poem.Url] = DateTime.UtcNow;
    }

    return Results.Json(new { changed, url, unbuilt = Unbuilt(poem.Url) });
}));

app.MapGet("/api/build", () => Results.Json(new { running = rebuild.Running, failed = rebuild.Failed }));

app.MapPost("/api/build", () => Guarded(() =>
{
    rebuild.Request(manifest.Edit);
    return Results.Json(new { running = true });
}));

string spread = string.Join("  ", manifest.Poems
    .GroupBy(poem => poem.Rating)
    .OrderBy(group => group.Key)
    .Select(group => $"{Mark(group.Key)}: {group.Count()}"));
Console.WriteLine($"review server on http://127.0.0.1:{Port}");
Console.WriteLine($"  {spread}");
Console.WriteLine($"  {LoadProgress().Count} already reviewed this pass\n");

app.Run($"http://127.0.0.1:{Port}");
return 0;

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static string Mark(int rating) => rating == 0 ? "-" : new string('*', rating);

static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

// The url a title would take beside the poem now at url
static string UrlFor(string url, string title)
{
    string directory = url.TrimEnd('/');
    return directory[..(directory.LastIndexOf('/') + 1)] + Slugify(title) + "/";
}

// When you change Markup.Slugify in Source/Markup.cs, change this to match
static string Slugify(string title)
{
    var unaccented = new StringBuilder();
    foreach (char c in title.Normalize(NormalizationForm.FormD))
    {
        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            unaccented.Append(c);
    }

    string slug = unaccented.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    slug = Regex.Replace(slug, @"[\s/\\_]+", "-");
    slug = Regex.Replace(slug, @"[^0-9a-z\-]", "");
    slug = Regex.Replace(slug, "-{2,}", "-");
    slug = slug.Trim('-');

    if (slug.Length == 0)
        slug = "untitled";
    else if (!slug.Any(char.IsLetter))
        slug = $"poem-{slug}";

    return slug;
}

// One request at a time, so the progress file is never read while another request writes it
IResult Guarded(Func<IResult> handle)
{
    lock (gate)
    {
        try
        {
            LoadManifest();
            return handle();
        }
        catch (MissingManifest e)
        {
            return Error(503, e.Message);
        }
    }
}

// Rereads the manifest whenever a rebuild has rewritten it
void LoadManifest()
{
    if (!File.Exists(manifestPath))
        throw new MissingManifest();

    DateTime written = File.GetLastWriteTimeUtc(manifestPath);
    if (written == manifestWritten)
        return;

    manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(manifestPath), JsonSerializerOptions.Web)!;
    byUrl = manifest.Poems.ToDictionary(poem => poem.Url);
    manifestWritten = written;
    Console.WriteLine($"  manifest: {byUrl.Count} poems");
}

Dictionary<string, int> LoadProgress()
{
    try
    {
        return JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllBytes(progressPath)) ?? [];
    }
    catch (Exception e) when (e is IOException or JsonException)
    {
        return [];
    }
}

void SaveProgress(Dictionary<string, int> progress)
{
    var sorted = new SortedDictionary<string, int>(progress, StringComparer.Ordinal);
    var format = new JsonSerializerOptions { WriteIndented = true, IndentSize = 0, NewLine = "\n" };
    PoemFile.Replace(progressPath, JsonSerializer.SerializeToUtf8Bytes(sorted, format));
}

bool Unbuilt(string url) => textSavedAt.TryGetValue(url, out DateTime saved) && saved > manifestWritten;

PoemState? State(string url, bool changed = false)
{
    if (!byUrl.TryGetValue(url, out ManifestPoem? poem))
        return null;

    Dictionary<string, int> progress = LoadProgress();
    List<ManifestPoem> order = manifest.Poems;
    // Wraps around, so the end of the collection does not dead-end
    string? next = order.Skip(poem.I + 1).Concat(order).FirstOrDefault(other => !progress.ContainsKey(other.Url))?.Url;

    return new PoemState(url, poem.Title, PoemFile.Rating(Path.Combine(root, poem.Src)), manifest.Max,
        poem.I + 1, order.Count, progress.ContainsKey(url), progress.Count, next, poem.Src, changed, Unbuilt(url));
}

class MissingManifest() : Exception("Output/review-map.json is missing. Build in review mode first:\n    build -r");

record Manifest(int Max, bool Edit, List<ManifestPoem> Poems);

record ManifestPoem(int I, string Url, string Title, int Rating, string Src);

record RatingRequest(string? Url, int? Rating);

record TextRequest(string? Url, string? Text);

record PoemState(string Url, string Title, int Rating, int Max, int Index, int Total, bool Reviewed,
    int ReviewedCount, string? NextUnreviewed, string Src, bool Changed, bool Unbuilt);

// Works on bytes, so each file's encoding, BOM (or its absence) and line endings survive exactly
static class PoemFile
{
    static ReadOnlySpan<byte> Bom => [0xEF, 0xBB, 0xBF];

    public static int Rating(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        return Stars(raw, BomLength(raw));
    }

    // Rewrites only the leading asterisks, and says whether that changed anything
    public static bool SetRating(string path, int rating)
    {
        byte[] raw = File.ReadAllBytes(path);
        int start = BomLength(raw);
        int stars = Stars(raw, start);
        if (stars == rating)
            return false;

        Replace(path, [.. raw[..start], .. Enumerable.Repeat((byte)'*', rating), .. raw[(start + stars)..]]);
        return true;
    }

    // Everything after the rating, with \n line endings
    public static string Text(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        return Encoding.UTF8.GetString(raw[Head(raw)..]).ReplaceLineEndings("\n");
    }

    // Rewrites everything after the rating in the file's own line endings, and says whether that changed anything
    public static bool SetText(string path, string text)
    {
        byte[] raw = File.ReadAllBytes(path);
        int head = Head(raw);
        string newline = raw.AsSpan().IndexOf("\r\n"u8) >= 0 ? "\r\n" : "\n";
        byte[] body = Encoding.UTF8.GetBytes(text.ReplaceLineEndings(newline));
        if (raw.AsSpan(head).SequenceEqual(body))
            return false;

        Replace(path, [.. raw[..head], .. body]);
        return true;
    }

    public static void Replace(string path, byte[] contents)
    {
        File.WriteAllBytes(path + ".tmp", contents);
        File.Move(path + ".tmp", path, overwrite: true);
    }

    static int Head(byte[] raw) => BomLength(raw) + Stars(raw, BomLength(raw));

    static int BomLength(byte[] raw) => raw.AsSpan().StartsWith(Bom) ? Bom.Length : 0;

    static int Stars(byte[] raw, int start) =>
        raw.AsSpan(start).IndexOfAnyExcept((byte)'*') is int end and >= 0 ? end : raw.Length - start;
}

// A changed title moves the url, and the build fails on a tag row that matches no poem
static class Tags
{
    public static void Rekey(string path, string from, string to)
    {
        string text = File.ReadAllText(path);
        string moved = Regex.Replace(text, "^" + Regex.Escape(from) + "\t", to + "\t", RegexOptions.Multiline);
        if (moved != text)
            PoemFile.Replace(path, Encoding.UTF8.GetBytes(moved));
    }
}

// Runs build -r, or -re when editing; a request made while it runs gets one more build when it ends
sealed class Rebuild(string root)
{
    readonly Lock gate = new();
    Process? running;
    bool again;
    string mode = "-r";

    public bool Failed { get; private set; }

    public bool Running
    {
        get { lock (gate) return running is not null; }
    }

    public void Request(bool edit)
    {
        lock (gate)
        {
            mode = edit ? "-re" : "-r";
            if (running is null)
                Start();
            else
                again = true;
        }
    }

    void Start()
    {
        var info = new ProcessStartInfo("pwsh") { WorkingDirectory = root, CreateNoWindow = true };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add($"& ./Tools/commands/build.ps1 {mode} *> Logs/review-build.log; exit $LASTEXITCODE");

        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.Exited += (_, _) =>
        {
            lock (gate)
            {
                Failed = process.ExitCode != 0;
                Console.WriteLine(Failed ? "  build failed; see Logs/review-build.log" : "  build finished");
                process.Dispose();
                running = null;
                if (again)
                {
                    again = false;
                    Start();
                }
            }
        };

        running = process;
        process.Start();
        Console.WriteLine("  building");
    }
}
