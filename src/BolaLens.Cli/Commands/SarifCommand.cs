using System.CommandLine;
using BolaLens.Analysis.Verdict;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens sarif --report r.json --out r.sarif [--root dir] [--uri-prefix src/api]` — converts a report to
/// SARIF 2.1.0 for GitHub code scanning (FR14). `scan --sarif` does the same in one step.</summary>
public static class SarifCommand
{
    public static Option<bool> NoUnknownOption() =>
        new("--sarif-no-unknown") { Description = "Leave Unknown findings out of the SARIF file (by default they are 'note' results)" };

    public static Option<string?> UriPrefixOption() =>
        new("--uri-prefix") { Description = "Prefix for file paths in SARIF, i.e. the solution folder relative to the repository root" };

    public static Command Create()
    {
        var report = new Option<string>("--report", "-r") { Description = "report.json produced by `scan`", Required = true };
        var output = new Option<string>("--out", "-o") { Description = "SARIF file to write", Required = true };
        var root = new Option<string?>("--root") { Description = "Folder the report's relative paths start from (SRCROOT); default: the scanned solution's folder" };
        var noUnknown = NoUnknownOption();
        var prefix = UriPrefixOption();
        var cmd = new Command("sarif", "Convert a report to SARIF 2.1.0 (GitHub code scanning)") { report, output, root, noUnknown, prefix };
        cmd.SetAction(async (r, ct) =>
        {
            try
            {
                var rep = JsonReportWriter.Read(r.GetValue(report)!);
                var srcRoot = r.GetValue(root) ?? Path.GetDirectoryName(Path.GetFullPath(rep.Run.TargetPath));
                await SarifReportWriter.WriteFileAsync(rep, SymbolicRules.Descriptions, r.GetValue(output)!, !r.GetValue(noUnknown), srcRoot, r.GetValue(prefix), ct);
                Console.Error.WriteLine($"wrote {r.GetValue(output)}");
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
