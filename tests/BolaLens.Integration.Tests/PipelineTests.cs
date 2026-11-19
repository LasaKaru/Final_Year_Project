using BolaLens.Cli.Commands;
using BolaLens.App;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using BolaLens.Eval;
using BolaLens.Inference;
using BolaLens.Reporting;

namespace BolaLens.Integration.Tests;

/// <summary>Hybrid mode end-to-end with a scripted fake model (no ONNX needed), report round-trip,
/// determinism and CLI exit codes.</summary>
public class PipelineTests(BolaLabFixture fx)
{
    [Fact]
    public async Task Hybrid_mode_invokes_the_validator_only_on_candidates_and_fuses()
    {
        var calls = 0;
        // Fake model: always "protected" with high confidence, citing the last line of the slice.
        var service = new ScanService(BolaLensConfig.Default.WithGate(false), _ => new OnnxSemanticValidator((prompt, _) =>
        {
            calls++;
            var lastLine = prompt.Split('\n').Last(l => l.StartsWith('L')).Split(' ')[0];
            return ($$"""{"status":"protected","confidence":0.95,"reason":"fake","evidence":["{{lastLine}}"]}""", 10, 10);
        }, "fake-model"));

        var (report, _) = await service.ScanAsync(BolaLabFixture.SolutionPath, ScanMode.Hybrid, TestContext.Current.CancellationToken);
        report.Run.ModelGate.ShouldBeNull();

        var candidates = report.Findings.Count(f => FusionPolicy.IsCandidate(f.Symbolic));
        candidates.ShouldBe(9 + 1 + 1); // 9 vulnerable paths + TC10 unknown + TC09 delegated (SYM-05)
        calls.ShouldBe(candidates);
        report.Coverage.SemanticInvocations.ShouldBe(candidates);
        report.Run.Mode.ShouldBe("hybrid");
        report.Run.ModelId.ShouldBe("fake-model");

        // A confident, grounded "protected" suppresses every symbolic Vulnerable (this is what RQ2 measures).
        report.Findings.Where(f => f.Symbolic.Status == VerdictStatus.Vulnerable).ShouldAllBe(f => f.Final == VerdictStatus.Protected);
        report.Findings.Single(f => f.Symbolic.Status == VerdictStatus.Unknown).Final.ShouldBe(VerdictStatus.Unknown);

        var units = UnitMatcher.Match(LabelSet.Load(BolaLabFixture.ExpectedPath), UnitMatcher.Predict(report));
        var m = ConfusionMatrix.From(units);
        (m.TP, m.FN, m.TN).ShouldBe((0, 9, 6));
    }

    [Fact]
    public async Task Calibration_gate_makes_a_constant_answer_model_advisory_only()
    {
        // A "model" that always answers vulnerable with high confidence (what SmolLM2-135M actually did).
        var service = new ScanService(BolaLensConfig.Default, _ => new OnnxSemanticValidator((prompt, _) =>
            ("""{"status":"vulnerable","confidence":0.9,"reason":"always","evidence":["L1","L2"]}""", 10, 10), "constant"));
        var (report, _) = await service.ScanAsync(BolaLabFixture.SolutionPath, ScanMode.Hybrid, TestContext.Current.CancellationToken);

        var gate = report.Run.ModelGate.ShouldNotBeNull();
        (gate.Passed, gate.Correct, gate.Total).ShouldBe((false, 2, 4));
        report.Findings.ShouldAllBe(f => f.Final == f.Symbolic.Status);
        report.Findings.Single(f => f.Path.Endpoint.Id == "PUT api/documents/{id}").FinalReason.ShouldContain("advisory");
        report.Coverage.SemanticInvocations.ShouldBe(11);
    }

    [Fact]
    public async Task Calibration_gate_passes_a_model_that_answers_the_probes_correctly()
    {
        // Oracle on probes: protected iff the slice shows a trusted-identity check/predicate.
        var service = new ScanService(BolaLensConfig.Default, _ => new OnnxSemanticValidator((prompt, _) =>
        {
            var task = prompt[prompt.LastIndexOf("### Task", StringComparison.Ordinal)..];
            var status = task.Contains("trusted identity") || task.Contains("session.UserId") ? "protected" : "vulnerable";
            return ($$"""{"status":"{{status}}","confidence":0.6,"reason":"oracle","evidence":["L1"]}""", 10, 10);
        }, "oracle"));
        var (report, _) = await service.ScanAsync(BolaLabFixture.SolutionPath, ScanMode.Hybrid, TestContext.Current.CancellationToken);
        report.Run.ModelGate.ShouldNotBeNull().Passed.ShouldBeTrue();
    }

    [Fact]
    public async Task Run_store_saves_lists_and_rejects_bad_ids()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bolalens-runs-{Guid.NewGuid():N}");
        try
        {
            var store = new RunStore(root);
            var id = await store.SaveAsync(new ScanResult(fx.Report, fx.Symbolic), TestContext.Current.CancellationToken);
            store.List().ShouldHaveSingleItem().Id.ShouldBe(id);
            store.GetReport(id)!.Findings.Count.ShouldBe(fx.Report.Findings.Count);
            store.GetContext(id)!.Endpoints.Count.ShouldBe(16);
            store.GetReport("../etc").ShouldBeNull();
            RunStore.IsValidId("..").ShouldBeFalse();
            RunStore.IsValidId("a/b").ShouldBeFalse();
            store.Delete(id).ShouldBeTrue();
            store.List().ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Evaluator_gives_symbolic_and_hybrid_arms_and_compare_adds_more()
    {
        var labels = LabelSet.Load(BolaLabFixture.ExpectedPath);
        var single = Evaluator.Evaluate(labels, [("r", fx.Report)]);
        single.Arms.Select(a => a.Name).ShouldBe(["symbolic"]);
        var hybrid = fx.Report with { Run = fx.Report.Run with { Mode = "hybrid", ModelId = "m" } };
        var both = Evaluator.Evaluate(labels, [("a", fx.Report), ("b", hybrid)]);
        both.Arms.Select(a => a.Name).ShouldBe(["symbolic (a)", "hybrid (m, no gate)"]);
        both.ToMarkdown().ShouldContain("| hybrid (m, no gate) |");
    }

    [Fact]
    public async Task Report_round_trips_through_json_and_ids_are_deterministic()
    {
        var file = Path.Combine(Path.GetTempPath(), $"bolalens-{Guid.NewGuid():N}.json");
        try
        {
            await JsonReportWriter.WriteFileAsync(fx.Report, file, TestContext.Current.CancellationToken);
            var back = JsonReportWriter.Read(file);
            back.Findings.Select(f => f.FindingId).ShouldBe(fx.Report.Findings.Select(f => f.FindingId));
            back.Findings.Select(f => f.Symbolic.Status).ShouldBe(fx.Report.Findings.Select(f => f.Symbolic.Status));

            var (again, _) = await new ScanService(BolaLensConfig.Default).ScanAsync(BolaLabFixture.SolutionPath, ScanMode.Symbolic, TestContext.Current.CancellationToken);
            again.Findings.Select(f => (f.FindingId, f.Final)).ShouldBe(fx.Report.Findings.Select(f => (f.FindingId, f.Final)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Inventory_of_missing_path_exits_with_2()
    {
        var code = await InventoryCommand.Create().Parse(["does/not/exist.sln"]).InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);
        code.ShouldBe(Common.LoadError);
    }

    [Fact]
    public async Task Scan_exits_with_1_when_vulnerable_findings_exist()
    {
        var code = await ScanCommand.Create().Parse([BolaLabFixture.SolutionPath, "--quiet"]).InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);
        code.ShouldBe(Common.VulnerableFound);
    }
}
