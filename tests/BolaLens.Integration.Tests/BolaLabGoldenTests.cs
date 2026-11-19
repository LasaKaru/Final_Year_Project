using BolaLens.Core.Model;
using BolaLens.Eval;

namespace BolaLens.Integration.Tests;

/// <summary>Golden tests: the symbolic verdict AND rule ID of every BolaLab test case must match
/// expected.json (labels live only there, ADR-006).</summary>
public class BolaLabGoldenTests(BolaLabFixture fx)
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var e in LabelSet.Load(BolaLabFixture.ExpectedPath).Entries) data.Add(e.Tc!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Test_case_matches_expected_label_and_rule(string tc)
    {
        var label = LabelSet.Load(BolaLabFixture.ExpectedPath).Entries.Single(e => e.Tc == tc);
        var predictions = UnitMatcher.Predict(fx.Report, useSymbolic: true);
        predictions.Keys.ShouldContain(label.UnitKey);
        var p = predictions[label.UnitKey];
        p.Status.ShouldBe(label.Label, $"{tc} {label.UnitKey}");
        p.RuleIds.ShouldContain(label.ExpectedRule!, $"{tc} {label.UnitKey}");
    }

    [Fact]
    public void Expected_json_has_16_unique_units_and_one_endpoint_per_case()
    {
        var labels = LabelSet.Load(BolaLabFixture.ExpectedPath).Entries;
        labels.Count.ShouldBe(16);
        labels.Select(l => l.UnitKey).Distinct().Count().ShouldBe(16);
        fx.Symbolic.Endpoints.Select(e => e.Id).ShouldBe(labels.Select(l => l.UnitKey), ignoreOrder: true);
        labels.Count(l => l.Label == VerdictStatus.Vulnerable).ShouldBe(9);
        labels.Count(l => l.Label == VerdictStatus.Protected).ShouldBe(6);
    }

    [Fact]
    public void Symbolic_baseline_metrics_on_development_set()
    {
        var units = UnitMatcher.Match(LabelSet.Load(BolaLabFixture.ExpectedPath), UnitMatcher.Predict(fx.Report, useSymbolic: true));
        var m = ConfusionMatrix.From(units);
        (m.TP, m.FP, m.FN, m.TN).ShouldBe((9, 0, 0, 6));
        m.CorrectAbstentions.ShouldBe(1);
    }

    [Theory]
    [InlineData("DELETE api/orders/{id}", "comparand is client-controlled")]
    [InlineData("POST api/orders/{id}/copy-status-to/{targetid}", "guard checks a different object")]
    [InlineData("PUT api/orders/{id}/total", "guard does not precede the state change")]
    [InlineData("PUT api/notes/{id}", "tenant-level check but object has a user owner")]
    [InlineData("DELETE api/projects/{id}", "query filter bypassed with IgnoreQueryFilters()")]
    public void Invalid_guards_carry_the_reason(string unit, string reason)
    {
        fx.Report.Findings.Where(f => f.Path.Endpoint.Id == unit)
            .SelectMany(f => f.Path.Guards).ShouldContain(g => !g.IsValid && g.InvalidReason == reason);
    }

    [Fact]
    public void Cross_layer_sink_is_located_in_the_repository()
    {
        var tc13 = fx.Report.Findings.Single(f => f.Path.Endpoint.Id == "DELETE api/invoices/{id}");
        tc13.Path.Sinks.ShouldContain(s => s.Location.FilePath.EndsWith("Services/Invoices.cs") && s.Kind == SinkKind.SaveChanges);
        tc13.Path.Steps.ShouldContain(s => s.Kind == StepKind.Call && s.Depth == 0);
        tc13.Path.Steps.ShouldContain(s => s.Kind == StepKind.Load && s.Depth == 2);
    }

    [Fact]
    public void Coverage_and_run_info_are_recorded()
    {
        var c = fx.Report.Coverage;
        c.ProjectsLoaded.ShouldBe(1);
        c.ProjectsFailed.ShouldBe(0);
        c.Endpoints.ShouldBe(16);
        c.PathsExtracted.ShouldBe(18); // TC03 (id + body DTO with UserId) and TC04 (id, targetId) have two paths
        c.SymbolicUnknown.ShouldBe(1);
        fx.Report.Run.ConfigHash.ShouldNotBeNullOrWhiteSpace();
        fx.Report.Run.PeakWorkingSetBytes.ShouldBeGreaterThan(0);
        fx.Report.Run.Mode.ShouldBe("symbolic");
    }

    [Fact]
    public void Candidate_slices_never_contain_labels_rule_ids_or_comments()
    {
        foreach (var f in fx.Report.Findings.Where(f => f.Slice is not null))
        {
            var slice = f.Slice!;
            slice.ShouldNotContain("SYM-");
            slice.ShouldNotContain("TC0");
            slice.ShouldNotContain("//");
            slice.ShouldNotContain("Vulnerable", Case.Insensitive);
        }
        fx.Report.Findings.Single(f => f.Path.Endpoint.Id == "PUT api/orders/{id}/status").Slice.ShouldNotBeNull();
    }
}
