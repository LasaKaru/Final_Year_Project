using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BolaLens.Web.Tests;

/// <summary>Boots the real web host in memory (TestServer) against the repository root and a temp runs folder.</summary>
public sealed class UiFactory : WebApplicationFactory<Program>
{
    public static string RepoRoot { get; } = FindRoot();
    public string RunsDir { get; } = Path.Combine(Path.GetTempPath(), $"bolalens-ui-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("root", RepoRoot);
        builder.UseSetting("runs", RunsDir);
        builder.UseSetting("models", Path.Combine(RepoRoot, "models"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(RunsDir)) Directory.Delete(RunsDir, true);
    }

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "BolaLens.sln"))) return d.FullName;
        throw new InvalidOperationException("repo root not found");
    }
}

public class WebApiTests(UiFactory factory) : IClassFixture<UiFactory>
{
    private HttpClient Client()
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-BolaLens", "1");
        return c;
    }

    [Fact]
    public async Task Serves_the_ui_shell_and_assets_without_external_resources()
    {
        var c = factory.CreateClient();
        var html = await c.GetStringAsync("/", TestContext.Current.CancellationToken);
        html.ShouldContain("<main id=\"main\"");
        html.ShouldContain("name=\"viewport\"");
        (await c.GetStringAsync("/app.js", TestContext.Current.CancellationToken)).ShouldContain("textContent");
        (await c.GetStringAsync("/app.css", TestContext.Current.CancellationToken)).ShouldContain("@media (max-width: 720px)");
        var res = await c.GetAsync("/", TestContext.Current.CancellationToken);
        res.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'self'");
        foreach (var asset in new[] { html, await c.GetStringAsync("/app.css", TestContext.Current.CancellationToken) })
            asset.ShouldNotContain("https://");
    }

    [Fact]
    public async Task Info_lists_bolalab_target_and_its_labels()
    {
        using var doc = JsonDocument.Parse(await Client().GetStringAsync("/api/info", TestContext.Current.CancellationToken));
        var targets = doc.RootElement.GetProperty("targets").EnumerateArray().Select(t => t.GetString()).ToList();
        targets[0].ShouldBe("fixtures/BolaLab/BolaLab.sln");
        targets.ShouldNotContain("BolaLens.sln");
        doc.RootElement.GetProperty("defaultLabels").GetProperty("fixtures/BolaLab/BolaLab.sln").GetString().ShouldBe("fixtures/BolaLab/expected.json");
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/scans", new { target = "fixtures/BolaLab/BolaLab.sln", mode = "symbolic" }, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("does/not/exist.sln")]
    [InlineData("README.md")]
    public async Task Rejects_bad_targets(string target)
    {
        var res = await Client().PostAsJsonAsync("/api/scans", new { target, mode = "symbolic" }, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("..%2F..%2Fetc")]
    [InlineData("nope")]
    public async Task Unknown_or_traversing_run_ids_are_404(string id)
    {
        (await Client().GetAsync($"/api/runs/{id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Scan_eval_export_and_delete_end_to_end()
    {
        var c = Client();
        var ct = TestContext.Current.CancellationToken;
        var start = await c.PostAsJsonAsync("/api/scans", new { target = "fixtures/BolaLab/BolaLab.sln", mode = "symbolic" }, ct);
        start.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var jobId = (await start.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("jobId").GetString();

        JsonElement job = default;
        for (var i = 0; i < 240; i++)
        {
            job = await c.GetFromJsonAsync<JsonElement>($"/api/scans/{jobId}", ct);
            if (job.GetProperty("status").GetString() is "done" or "failed") break;
            await Task.Delay(500, ct);
        }
        job.GetProperty("status").GetString().ShouldBe("done", job.ToString());
        var runId = job.GetProperty("runId").GetString()!;
        job.GetProperty("log").EnumerateArray().Select(l => l.GetString()).ShouldContain(l => l!.Contains("Stage 1"));

        var report = await c.GetFromJsonAsync<JsonElement>($"/api/runs/{runId}", ct);
        report.GetProperty("findings").GetArrayLength().ShouldBe(18);
        report.GetProperty("findings")[0].GetProperty("final").ValueKind.ShouldBe(JsonValueKind.String); // enums as strings

        var context = await c.GetFromJsonAsync<JsonElement>($"/api/runs/{runId}/context", ct);
        context.GetProperty("endpoints").GetArrayLength().ShouldBe(16);

        var runs = await c.GetFromJsonAsync<JsonElement>("/api/runs", ct);
        runs.EnumerateArray().ShouldContain(r => r.GetProperty("id").GetString() == runId);

        var eval = await c.PostAsJsonAsync($"/api/runs/{runId}/eval", new { labels = "fixtures/BolaLab/expected.json" }, ct);
        eval.StatusCode.ShouldBe(HttpStatusCode.OK);
        var arm = (await eval.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("arms")[0];
        arm.GetProperty("matrix").GetProperty("tp").GetInt32().ShouldBe(9);
        arm.GetProperty("matrix").GetProperty("fp").GetInt32().ShouldBe(0);

        var html = await c.GetStringAsync($"/api/runs/{runId}/export", ct);
        html.ShouldContain("<script id=\"bolalens-data\" type=\"application/json\">");
        html.ShouldNotContain("src=\"app.js\"");
        html.ShouldNotContain("href=\"app.css\"");
        var data = html[(html.IndexOf("application/json\">", StringComparison.Ordinal) + 18)..];
        data = data[..data.IndexOf("</script>", StringComparison.Ordinal)];
        data.ShouldNotContain("<"); // embedded JSON cannot break out of the script element
        JsonDocument.Parse(data).RootElement.GetProperty("report").GetProperty("findings").GetArrayLength().ShouldBe(18);

        (await c.DeleteAsync($"/api/runs/{runId}", ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await c.GetAsync($"/api/runs/{runId}", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hybrid_without_a_model_folder_is_rejected()
    {
        var res = await Client().PostAsJsonAsync("/api/scans", new { target = "fixtures/BolaLab/BolaLab.sln", mode = "hybrid", model = "models/does-not-exist" }, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
