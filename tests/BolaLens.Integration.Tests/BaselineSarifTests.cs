using BolaLens.Analysis.Verdict;
using BolaLens.Core.Model;
using BolaLens.Eval;
using BolaLens.Reporting;

namespace BolaLens.Integration.Tests;

/// <summary>Baseline arm adapter: SARIF results are attributed to the endpoint whose action method contains them.</summary>
public class BaselineSarifTests(BolaLabFixture fx)
{
    [Fact]
    public void Results_inside_an_action_flag_that_endpoint_only()
    {
        var update = fx.Report.Findings.Select(f => f.Path.Endpoint).First(e => e.Action == "Update" && e.Controller == "DocumentsController");
        var results = new List<BaselineResult>
        {
            // repository-root-relative path, as CodeQL writes it
            new("cs/insecure-direct-object-reference", "fixtures/BolaLab/" + update.Location.FilePath, update.Location.StartLine + 1),
            new("cs/other", "fixtures/BolaLab/src/BolaLab.Api/Program.cs", 1),
        };
        var predictions = BaselineSarif.Predict(fx.Report, results);
        predictions[Routes.UnitKey(update.Verb, update.RouteTemplate)].Status.ShouldBe(VerdictStatus.Vulnerable);
        predictions.Values.Count(p => p.Status == VerdictStatus.Vulnerable).ShouldBe(1);
    }

    [Fact]
    public async Task Bolalens_own_sarif_as_a_baseline_reproduces_the_symbolic_arm_on_bolalab()
    {
        // sanity check of the adapter: feeding BolaLens's SARIF back must give the same TP/FN on the vulnerable units
        // whose sink lies in the action itself; it can only lose units whose sink is in a service/repository
        var path = Path.Combine(Path.GetTempPath(), $"bl-{Guid.NewGuid():N}.sarif");
        await SarifReportWriter.WriteFileAsync(fx.Report, SymbolicRules.Descriptions, path, includeUnknown: false, null, null, TestContext.Current.CancellationToken);
        var labels = LabelSet.Load(BolaLabFixture.ExpectedPath);
        var arm = BaselineSarif.Arm("self", labels, fx.Report, BaselineSarif.Load(path));
        File.Delete(path);
        arm.Matrix.FP.ShouldBe(0);
        arm.Matrix.TP.ShouldBeGreaterThan(0);
        (arm.Matrix.TP + arm.Matrix.FN).ShouldBe(labels.Entries.Count(e => e.Label == VerdictStatus.Vulnerable));
    }
}
