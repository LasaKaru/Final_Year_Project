using System.CommandLine;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens html --report r.json [--context context.json] --out report.html` — a single, self-contained
/// HTML report (CSS, JS and data inlined; works offline, e.g. as an IPD/supervisor hand-out).</summary>
public static class HtmlCommand
{
    public static Command Create()
    {
        var report = new Option<string>("--report", "-r") { Description = "report.json produced by `scan`", Required = true };
        var context = new Option<string?>("--context") { Description = "context.json from a saved run (adds the endpoints/policy/schema/DI tabs)" };
        var output = new Option<string>("--out", "-o") { Description = "HTML file to write", Required = true };
        var cmd = new Command("html", "Export a report as one offline HTML file") { report, context, output };
        cmd.SetAction(async (r, ct) =>
        {
            try
            {
                var rep = JsonReportWriter.Read(r.GetValue(report)!);
                object? ctx = r.GetValue(context) is { } c ? System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(c, ct)).RootElement.Clone() : null;
                var id = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(r.GetValue(report)!))) ?? "report";
                var outPath = r.GetValue(output)!;
                var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(outPath, HtmlReportExporter.Export(rep, ctx, id), ct);
                Console.Error.WriteLine($"wrote {outPath}");
                return Common.Ok;
            }
            catch (Exception ex) when (ex is FileNotFoundException or System.Text.Json.JsonException or IOException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }
        });
        return cmd;
    }
}
