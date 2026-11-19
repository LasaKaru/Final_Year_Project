using BolaLens.Core.Model;
using BolaLens.Eval;

namespace BolaLens.Inference.Tests;

public class MetricsTests
{
    private static MatchedUnit U(VerdictStatus label, VerdictStatus? predicted) =>
        new(new LabelEntry("T", "PUT", "api/x", null, label, null), predicted is null ? null : new UnitPrediction("PUT api/x", predicted.Value, [], null));

    [Fact]
    public void Confusion_matrix_keeps_unknowns_separate()
    {
        var m = ConfusionMatrix.From([
            U(VerdictStatus.Vulnerable, VerdictStatus.Vulnerable), U(VerdictStatus.Vulnerable, VerdictStatus.Protected),
            U(VerdictStatus.Vulnerable, VerdictStatus.Unknown), U(VerdictStatus.Protected, VerdictStatus.Vulnerable),
            U(VerdictStatus.Protected, VerdictStatus.OutOfScope), U(VerdictStatus.Protected, VerdictStatus.Unknown),
            U(VerdictStatus.Unknown, VerdictStatus.Unknown), U(VerdictStatus.Protected, null),
        ]);
        (m.TP, m.FN, m.FP, m.TN, m.UnknownOnVulnerable, m.UnknownOnProtected, m.ExcludedLabels, m.CorrectAbstentions, m.Missing)
            .ShouldBe((1, 1, 1, 2, 1, 1, 1, 1, 1));

        var x = Metrics.From(m);
        x.Precision.ShouldBe(0.5);
        x.Recall.ShouldBe(0.5);
        x.OperationalRecall.ShouldBe(1.0 / 3);
        x.F1.ShouldBe(0.5);
        x.Fpr.ShouldBe(1.0 / 3);
        x.Fdr.ShouldBe(0.5);
        x.Coverage.ShouldBe(5.0 / 7);
    }

    [Fact]
    public void Undefined_ratios_are_null_and_print_as_na()
    {
        var x = Metrics.From(ConfusionMatrix.From([U(VerdictStatus.Vulnerable, VerdictStatus.Vulnerable)]));
        x.Fpr.ShouldBeNull();
        MarkdownTable.Fmt(x.Fpr).ShouldBe("n/a");
        MarkdownTable.Fmt(x.Precision).ShouldBe("1.000");
    }

    [Theory]
    [InlineData("PUT", "/api/Orders/{id:int}", "PUT api/orders/{id}")]
    [InlineData("delete", "api/x/{targetId}/", "DELETE api/x/{targetid}")]
    public void Unit_keys_are_normalised(string verb, string route, string key) =>
        new LabelEntry(null, verb, route, null, VerdictStatus.Protected, null).UnitKey.ShouldBe(key);
}
