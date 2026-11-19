using BolaLens.Core.Model;
using Spectre.Console;

namespace BolaLens.Reporting;

/// <summary>Human-readable console summary: run info, one row per endpoint (worst path), coverage and
/// the explanation of every Vulnerable/Unknown finding. Readable by a developer with basic security knowledge.</summary>
public sealed class ConsoleSummaryRenderer(IAnsiConsole console)
{
    public void Render(ScanReport report, bool details = true)
    {
        var run = report.Run;
        console.MarkupLine($"[bold]BolaLens {Markup.Escape(run.ToolVersion)}[/] · mode [aqua]{run.Mode}[/] · {Markup.Escape(run.TargetPath)}");
        if (run.ModelId is not null) console.MarkupLine($"model {Markup.Escape(run.ModelId)} · prompt {Markup.Escape(run.PromptVersion ?? "")}");
        if (run.ModelGate is { } gate)
            console.MarkupLine(gate.Passed
                ? $"calibration gate [green]passed[/] {gate.Correct}/{gate.Total}"
                : $"calibration gate [red]failed[/] {gate.Correct}/{gate.Total} (needs {gate.MinCorrect}) — model verdicts are [yellow]advisory only[/]");

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Endpoint").AddColumn("Final").AddColumn("Symbolic").AddColumn("Rules").AddColumn("Semantic");
        foreach (var group in report.Findings.GroupBy(f => f.Path.Endpoint.Id))
        {
            var worst = group.MaxBy(f => VerdictOrdering.Rank(f.Final))!;
            var sem = worst.Semantic is null ? "—" : $"{worst.Semantic.Status} ({worst.Semantic.Confidence?.ToString("0.00") ?? "?"})";
            table.AddRow(Markup.Escape(group.Key), Colour(worst.Final), worst.Symbolic.Status.ToString(),
                Markup.Escape(string.Join(",", worst.Symbolic.RuleIds)), Markup.Escape(sem));
        }
        console.Write(table);

        var c = report.Coverage;
        console.MarkupLine($"projects {c.ProjectsLoaded} (with errors {c.ProjectsFailed}) · endpoints {c.Endpoints} (with ids {c.EndpointsWithIdParams}) · paths {c.PathsExtracted} (truncated {c.PathsTruncated}) · unresolved calls {c.UnresolvedCalls}");
        console.MarkupLine($"symbolic: [red]{c.SymbolicVulnerable} vulnerable[/] · [green]{c.SymbolicProtected} protected[/] · [yellow]{c.SymbolicUnknown} unknown[/] · {c.SymbolicOutOfScope} out of scope" +
                           (c.SemanticInvocations > 0 ? $" · SLM calls {c.SemanticInvocations} (parse failures {c.SemanticParseFailures})" : ""));
        if (c.UnsupportedEndpointStyles > 0)
            console.MarkupLine($"[yellow]{c.UnsupportedEndpointStyles} Minimal-API endpoints were not analysed (unsupported endpoint style).[/]");
        console.MarkupLine($"time {run.TotalSeconds:0.0}s · peak memory {run.PeakWorkingSetBytes / 1024 / 1024} MB");

        if (!details) return;
        foreach (var f in report.Findings.Where(f => f.Final is VerdictStatus.Vulnerable or VerdictStatus.Unknown))
        {
            console.WriteLine();
            console.MarkupLine($"{Colour(f.Final)} [bold]{Markup.Escape(f.Path.Endpoint.Id)}[/] via '{Markup.Escape(f.Path.TaintSource.Name)}' [grey]({f.FindingId})[/]");
            console.MarkupLine($"  {Markup.Escape(f.Symbolic.Rationale)}");
            if (f.Semantic is not null) console.MarkupLine($"  SLM: {Markup.Escape(f.Semantic.Reason)}");
            console.MarkupLine($"  [grey]{Markup.Escape(f.FinalReason)}[/]");
        }
    }

    private static string Colour(VerdictStatus s) => s switch
    {
        VerdictStatus.Vulnerable => "[red]Vulnerable[/]",
        VerdictStatus.Protected => "[green]Protected[/]",
        VerdictStatus.Unknown => "[yellow]Unknown[/]",
        _ => "[grey]OutOfScope[/]",
    };
}
