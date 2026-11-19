using BolaLens.Core.Model;
using BolaLens.Inference;

namespace BolaLens.Inference.Tests;

/// <summary>The fusion table of guide 05 §16, row by row.</summary>
public class FusionPolicyTests
{
    private static readonly FusionPolicy Fusion = new(0.80);

    private static SymbolicVerdict Sym(VerdictStatus s, string rule = "SYM-08") => new(s, [rule], "r");

    private static SemanticVerdict Sem(VerdictStatus s, double conf, bool ok = true, int evidence = 1) =>
        new(s, conf, "because", Enumerable.Range(1, evidence).Select(i => $"L{i}").ToList(), ok, "m", 1, null);

    [Theory]
    [InlineData(VerdictStatus.Vulnerable, VerdictStatus.Vulnerable, 0.9, VerdictStatus.Vulnerable)]
    [InlineData(VerdictStatus.Vulnerable, VerdictStatus.Protected, 0.9, VerdictStatus.Protected)]   // suppressed (RQ2)
    [InlineData(VerdictStatus.Vulnerable, VerdictStatus.Protected, 0.5, VerdictStatus.Vulnerable)]  // not confident
    [InlineData(VerdictStatus.Vulnerable, VerdictStatus.Unknown, 0.9, VerdictStatus.Vulnerable)]
    [InlineData(VerdictStatus.Unknown, VerdictStatus.Vulnerable, 0.9, VerdictStatus.Vulnerable)]    // escalated
    [InlineData(VerdictStatus.Unknown, VerdictStatus.Vulnerable, 0.6, VerdictStatus.Unknown)]
    [InlineData(VerdictStatus.Unknown, VerdictStatus.Protected, 0.99, VerdictStatus.Unknown)]
    public void Fusion_table(VerdictStatus symbolic, VerdictStatus semantic, double conf, VerdictStatus expected) =>
        Fusion.Fuse(Sym(symbolic), Sem(semantic, conf)).Final.ShouldBe(expected);

    [Fact]
    public void Parse_failure_or_missing_evidence_never_suppresses()
    {
        Fusion.Fuse(Sym(VerdictStatus.Vulnerable), Sem(VerdictStatus.Protected, 0.99, ok: false)).Final.ShouldBe(VerdictStatus.Vulnerable);
        Fusion.Fuse(Sym(VerdictStatus.Vulnerable), Sem(VerdictStatus.Protected, 0.99, evidence: 0)).Final.ShouldBe(VerdictStatus.Vulnerable);
    }

    [Fact]
    public void Delegated_protection_can_be_disputed_but_other_protection_cannot()
    {
        Fusion.Fuse(Sym(VerdictStatus.Protected, "SYM-05"), Sem(VerdictStatus.Vulnerable, 0.9)).Final.ShouldBe(VerdictStatus.Vulnerable);
        Fusion.Fuse(Sym(VerdictStatus.Protected, "SYM-03"), Sem(VerdictStatus.Vulnerable, 0.9)).Final.ShouldBe(VerdictStatus.Protected);
    }

    [Fact]
    public void Candidate_set_is_vulnerable_unknown_and_delegated_protected()
    {
        FusionPolicy.IsCandidate(Sym(VerdictStatus.Vulnerable)).ShouldBeTrue();
        FusionPolicy.IsCandidate(Sym(VerdictStatus.Unknown, "SYM-02")).ShouldBeTrue();
        FusionPolicy.IsCandidate(Sym(VerdictStatus.Protected, "SYM-05")).ShouldBeTrue();
        FusionPolicy.IsCandidate(Sym(VerdictStatus.Protected, "SYM-03")).ShouldBeFalse();
        FusionPolicy.IsCandidate(Sym(VerdictStatus.OutOfScope, "SYM-01")).ShouldBeFalse();
    }

    [Fact]
    public void Advisory_mode_never_changes_the_symbolic_verdict()
    {
        Fusion.Fuse(Sym(VerdictStatus.Vulnerable), Sem(VerdictStatus.Protected, 0.99), advisory: true).Final.ShouldBe(VerdictStatus.Vulnerable);
        Fusion.Fuse(Sym(VerdictStatus.Unknown, "SYM-02"), Sem(VerdictStatus.Vulnerable, 0.99), advisory: true).Final.ShouldBe(VerdictStatus.Unknown);
        var (final, reason) = Fusion.Fuse(Sym(VerdictStatus.Protected, "SYM-05"), Sem(VerdictStatus.Vulnerable, 0.99), advisory: true);
        final.ShouldBe(VerdictStatus.Protected);
        reason.ShouldContain("calibration gate");
    }

    [Fact]
    public void Without_semantic_verdict_final_equals_symbolic() =>
        Fusion.Fuse(Sym(VerdictStatus.Vulnerable), null).Final.ShouldBe(VerdictStatus.Vulnerable);
}
