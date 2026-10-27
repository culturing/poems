#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

// Serves docs/ on port 5500 as GitHub Pages would, and reloads open pages once a build has
// finished. serve and build start it; by hand, run
//
//     dotnet run --file Tools/docs-server.cs

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.StaticFiles;

const int Port = 5500;
const string ReloadScript = """
    <script>(function wait() { fetch('/__reload').then(() => location.reload(), () => setTimeout(wait, 1000)); })();</script>
    """;

string root = Path.GetDirectoryName(ScriptDirectory())!;
string docs = Path.Combine(root, "docs") + Path.DirectorySeparatorChar;
var contentTypes = new FileExtensionContentTypeProvider();
var builds = new BuildWatcher(Path.Combine(root, "Output", "build-finished"));

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

WebApplication app = builder.Build();
app.Use((context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return next();
});

app.MapGet("/__reload", async (CancellationToken aborted) =>
{
    await builds.Finished.WaitAsync(aborted);
    return Results.Ok();
});

app.MapGet("/{**path}", (HttpContext context) =>
{
    string path = Path.GetFullPath(Path.Join(docs, context.Request.Path.Value));
    if (Directory.Exists(path))
    {
        if (!context.Request.Path.Value!.EndsWith('/'))
            return Results.Redirect(context.Request.Path + "/" + context.Request.QueryString);
        path = Path.Join(path, "index.html");
    }

    if (path.StartsWith(docs) && File.Exists(path))
        return Serve(path, StatusCodes.Status200OK);

    string notFound = Path.Join(docs, "404.html");
    return File.Exists(notFound) ? Serve(notFound, StatusCodes.Status404NotFound) : Results.NotFound();
});

Console.WriteLine($"serving {docs} on http://localhost:{Port}");
app.Run($"http://localhost:{Port}");

IResult Serve(string path, int status)
{
    if (!path.EndsWith(".html"))
        return Results.File(path, contentTypes.TryGetContentType(path, out string? type) ? type : "application/octet-stream");

    string html = File.ReadAllText(path);
    int end = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
    return Results.Content(end < 0 ? html + ReloadScript : html.Insert(end, ReloadScript), "text/html; charset=utf-8", statusCode: status);
}

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

// The build writes its marker last. Polled, since every build deletes and remakes Output/
class BuildWatcher
{
    readonly Timer poll;
    DateTime seen;
    TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BuildWatcher(string marker)
    {
        seen = File.GetLastWriteTimeUtc(marker);
        poll = new Timer(_ =>
        {
            DateTime written = File.GetLastWriteTimeUtc(marker);
            if (!File.Exists(marker) || written == seen)
                return;
            seen = written;
            Interlocked.Exchange(ref finished, new(TaskCreationOptions.RunContinuationsAsynchronously)).SetResult();
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public Task Finished => finished.Task;
}
