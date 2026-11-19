using System.Text.Json;
using BolaLens.Analysis.Loading;
using BolaLens.App;
using BolaLens.Core.Config;
using BolaLens.Core.Json;
using BolaLens.Eval;
using BolaLens.Inference;
using BolaLens.Reporting;
using BolaLens.Web;

// FIRST: register the SDK's MSBuild before any MSBuild/MSBuildWorkspace type is loaded.
MsBuildBootstrap.EnsureRegistered();

var builder = WebApplication.CreateBuilder(args);
// Local-only by default: the UI can read source folders and run scans on this machine.
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5057");
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonDefaults.Options.PropertyNamingPolicy;
    o.SerializerOptions.DefaultIgnoreCondition = JsonDefaults.Options.DefaultIgnoreCondition;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    foreach (var c in JsonDefaults.Options.Converters) o.SerializerOptions.Converters.Add(c);
});

var cwd = Path.GetFullPath(builder.Configuration["root"] ?? UiOptions.FindRepositoryRoot(Environment.CurrentDirectory));
Directory.SetCurrentDirectory(cwd); // reports store target paths relative to the working folder
var runsDir = Path.GetFullPath(builder.Configuration["runs"] ?? Path.Combine(cwd, ".bolalens", "runs"));
var recorded = Path.Combine(cwd, "eval", "runs");
var modelsDir = Path.GetFullPath(builder.Configuration["models"] ?? Path.Combine(cwd, "models"));
var configPath = builder.Configuration["config"];
builder.Services.AddSingleton(new RunStore(runsDir, Directory.Exists(recorded) ? [recorded] : []));
builder.Services.AddSingleton<ScanJobs>();

var app = builder.Build();
var ui = new UiOptions(cwd, runsDir, modelsDir, configPath);

// CSRF defence for a localhost tool: state-changing requests must carry a custom header, which a
// cross-site page cannot send without a CORS preflight (and no CORS policy is configured).
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsDelete(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method))
    {
        if (ctx.Request.Headers["X-BolaLens"] != "1")
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "missing X-BolaLens header" });
            return;
        }
    }
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";
    await next();
});

app.MapGet("/", () => Results.Content(HtmlReportExporter.Asset("index.html"), "text/html; charset=utf-8"));
app.MapGet("/app.css", () => Results.Content(HtmlReportExporter.Asset("app.css"), "text/css; charset=utf-8"));
app.MapGet("/app.js", () => Results.Content(HtmlReportExporter.Asset("app.js"), "text/javascript; charset=utf-8"));

app.MapGet("/api/info", () => Results.Ok(new
{
    version = ScanService.ToolVersion,
    cwd = ui.Root,
    runsDir = ui.RunsDir,
    targets = ui.DiscoverTargets(),
    models = ModelInfo.Discover(ui.ModelsDir).Select(m => new { m.Id, path = Path.GetRelativePath(ui.Root, m.Path), sizeMb = m.SizeBytes / 1024.0 / 1024.0, m.ChatFormat, m.ModelType }),
    defaultLabels = ui.DefaultLabels(),
}));

app.MapGet("/api/runs", (RunStore store) => Results.Ok(store.List().Select(r => r with { Folder = Path.GetRelativePath(ui.Root, r.Folder) })));

app.MapGet("/api/runs/{id}", (string id, RunStore store) =>
    store.GetReport(id) is { } report ? Results.Ok(report) : Results.NotFound(new { error = "run not found" }));

app.MapGet("/api/runs/{id}/context", (string id, RunStore store) =>
    store.GetContext(id) is { } c ? Results.Ok(c)
    : store.FolderOf(id) is not null ? Results.NoContent() // recorded runs may have no context.json
    : Results.NotFound(new { error = "run not found" }));

app.MapGet("/api/runs/{id}/export", (string id, RunStore store) =>
{
    var report = store.GetReport(id);
    if (report is null) return Results.NotFound(new { error = "run not found" });
    var html = HtmlReportExporter.Export(report, store.GetContext(id), id);
    return Results.File(System.Text.Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8", $"bolalens-{id}.html");
});

app.MapDelete("/api/runs/{id}", (string id, RunStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound(new { error = "run not found or read-only" }));

app.MapPost("/api/scans", (ScanRequest req, ScanJobs jobs) =>
{
    var target = ui.Resolve(req.Target);
    if (target is null || !File.Exists(target) || Path.GetExtension(target).ToLowerInvariant() is not (".sln" or ".slnx" or ".csproj"))
        return Results.BadRequest(new { error = "target must be an existing .sln, .slnx or .csproj file" });
    BolaLensConfig config;
    try { config = BolaLensConfig.Load(ui.ConfigPath); }
    catch (Exception ex) when (ex is IOException or JsonException) { return Results.BadRequest(new { error = $"config: {ex.Message}" }); }

    var mode = string.Equals(req.Mode, "hybrid", StringComparison.OrdinalIgnoreCase) ? ScanMode.Hybrid : ScanMode.Symbolic;
    if (mode == ScanMode.Hybrid)
    {
        var model = ui.Resolve(req.Model ?? config.Inference.ModelPath);
        if (model is null || !ModelInfo.IsModelFolder(model)) return Results.BadRequest(new { error = "model must be a folder containing genai_config.json" });
        config = config.WithModel(model);
        if (req.Gate == false) config = config.WithGate(false);
    }
    var job = jobs.Start(target, mode, config);
    return Results.Accepted($"/api/scans/{job.Id}", new { jobId = job.Id });
});

app.MapGet("/api/scans/{jobId}", (string jobId, ScanJobs jobs) =>
    jobs.Get(jobId) is { } j ? Results.Ok(j.Snapshot()) : Results.NotFound(new { error = "job not found" }));

app.MapPost("/api/runs/{id}/eval", (string id, EvalRequest req, RunStore store) =>
{
    var report = store.GetReport(id);
    if (report is null) return Results.NotFound(new { error = "run not found" });
    var labels = ui.Resolve(req.Labels);
    if (labels is null || !File.Exists(labels) || !labels.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "labels must be an existing .json file" });
    try
    {
        return Results.Ok(Evaluator.Evaluate(LabelSet.Load(labels), [(id, report)]));
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = $"labels: {ex.Message}" });
    }
});

Console.WriteLine($"BolaLens UI on {string.Join(", ", app.Urls.DefaultIfEmpty(builder.Configuration["urls"] ?? "http://127.0.0.1:5057"))} — runs in {runsDir}");
app.Run();

public sealed record ScanRequest(string Target, string? Mode, string? Model, bool? Gate);
public sealed record EvalRequest(string Labels);

/// <summary>Entry point type, public for WebApplicationFactory in tests.</summary>
public partial class Program;
