using BolaLens.Analysis.Robustness;
using BolaLens.App;
using BolaLens.Core.Config;

namespace BolaLens.Integration.Tests;

/// <summary>FR21 robustness on BolaLab: semantics-preserving perturbations (renamed locals, misleading comments, renamed
/// private methods) must not change any verdict or introduce compile errors, and comments must never reach the slices.</summary>
public class RobustnessTests
{
    [Fact]
    public async Task Semantics_preserving_perturbations_keep_every_verdict_and_comments_never_reach_slices()
    {
        var selected = new[] { "P1", "P2", "P3" }.Select(Perturbations.Find).ToList();
        var result = await new RobustnessService(BolaLensConfig.Default)
            .RunAsync(BolaLabFixture.SolutionPath, selected, TestContext.Current.CancellationToken);

        result.Rows.Count.ShouldBe(3);
        foreach (var row in result.Rows)
        {
            row.Units.ShouldBe(16);
            row.Changes.ShouldBeEmpty($"{row.PerturbationId} changed verdicts");
            row.CompileErrorsAfter.ShouldBe(row.CompileErrorsBefore);
        }
        var comments = result.Rows.Single(r => r.PerturbationId.StartsWith("P2"));
        comments.SlicesIdentical.ShouldBe(comments.SlicesCompared);
        result.ToMarkdown().ShouldContain("| P1-rename-locals");
    }
}
