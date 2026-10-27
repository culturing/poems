#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

// The rating editor behind the widget in Scripts/review.js. Whatever serves docs/ only reads it,
// so this edits the source files under Poems/ itself. build -r starts it; by hand, run
//
//     dotnet run --file Tools/review-server.cs

using System.Runtime.CompilerServices;
using System.Text.Json;

const int Port = 5501;
string root = Path.GetDirectoryName(ScriptDirectory())!;
string manifestPath = Path.Combine(root, "Output", "review-map.json");
string progressPath = Path.Combine(root, "Tools", "review-progress.json");

Lock gate = new();
Manifest manifest = new(0, []);
Dictionary<string, ManifestPoem> byUrl = [];
DateTime manifestWritten = default;

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

    bool changed = Ratings.Write(Path.Combine(root, poem.Src), rating);
    Dictionary<string, int> progress = LoadProgress();
    progress[poem.Url] = rating;
    SaveProgress(progress);

    Console.WriteLine($"  {Mark(rating),-3} {poem.Title}" + (changed ? "" : "   (unchanged)"));
    return Results.Json(State(poem.Url, changed));
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
    Ratings.Replace(progressPath, JsonSerializer.SerializeToUtf8Bytes(sorted, format));
}

PoemState? State(string url, bool changed = false)
{
    if (!byUrl.TryGetValue(url, out ManifestPoem? poem))
        return null;

    Dictionary<string, int> progress = LoadProgress();
    List<ManifestPoem> order = manifest.Poems;
    // Wraps around, so the end of the collection does not dead-end
    string? next = order.Skip(poem.I + 1).Concat(order).FirstOrDefault(other => !progress.ContainsKey(other.Url))?.Url;

    return new PoemState(url, poem.Title, Ratings.Read(Path.Combine(root, poem.Src)), manifest.Max,
        poem.I + 1, order.Count, progress.ContainsKey(url), progress.Count, next, poem.Src, changed);
}

class MissingManifest() : Exception("Output/review-map.json is missing. Build in review mode first:\n    build -r");

record Manifest(int Max, List<ManifestPoem> Poems);

record ManifestPoem(int I, string Url, string Title, int Rating, string Src);

record RatingRequest(string? Url, int? Rating);

record PoemState(string Url, string Title, int Rating, int Max, int Index, int Total, bool Reviewed,
    int ReviewedCount, string? NextUnreviewed, string Src, bool Changed);

// Works on bytes, so each file's encoding, BOM (or its absence) and line endings survive exactly
static class Ratings
{
    static ReadOnlySpan<byte> Bom => [0xEF, 0xBB, 0xBF];

    public static int Read(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        return Stars(raw, BomLength(raw));
    }

    // Rewrites only the leading asterisks, and says whether that changed anything
    public static bool Write(string path, int rating)
    {
        byte[] raw = File.ReadAllBytes(path);
        int start = BomLength(raw);
        int stars = Stars(raw, start);
        if (stars == rating)
            return false;

        Replace(path, [.. raw[..start], .. Enumerable.Repeat((byte)'*', rating), .. raw[(start + stars)..]]);
        return true;
    }

    public static void Replace(string path, byte[] contents)
    {
        File.WriteAllBytes(path + ".tmp", contents);
        File.Move(path + ".tmp", path, overwrite: true);
    }

    static int BomLength(byte[] raw) => raw.AsSpan().StartsWith(Bom) ? Bom.Length : 0;

    static int Stars(byte[] raw, int start) =>
        raw.AsSpan(start).IndexOfAnyExcept((byte)'*') is int end and >= 0 ? end : raw.Length - start;
}
