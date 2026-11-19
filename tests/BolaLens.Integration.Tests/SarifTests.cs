using System.Text.Json.Nodes;
using BolaLens.Analysis.Verdict;
using BolaLens.Core.Model;
using BolaLens.Reporting;

namespace BolaLens.Integration.Tests;

/// <summary>SARIF 2.1.0 export (FR14) on the BolaLab report: one error per Vulnerable endpoint path, a note for Unknown,
/// stable fingerprints, relative locations and a code flow for every result.</summary>
public class SarifTests(BolaLabFixture fx)
{
    private JsonObject Run(bool includeUnknown = true, string? prefix = null) =>
        SarifReportWriter.Build(fx.Report, SymbolicRules.Descriptions, includeUnknown, Path.GetDirectoryName(BolaLabFixture.SolutionPath), prefix)["runs"]![0]!.AsObject();

    [Fact]
    public void Results_match_vulnerable_and_unknown_findings()
    {
        var results = Run()["results"]!.AsArray();
        var vulnerable = fx.Report.Findings.Count(f => f.Final == VerdictStatus.Vulnerable);
        var unknown = fx.Report.Findings.Count(f => f.Final == VerdictStatus.Unknown);
        results.Count(r => (string?)r!["level"] == "error").ShouldBe(vulnerable);
        results.Count(r => (string?)r!["level"] == "note").ShouldBe(unknown);
        Run(includeUnknown: false)["results"]!.AsArray().Count.ShouldBe(vulnerable);
    }

    [Fact]
    public void Every_result_has_a_known_rule_a_relative_location_a_fingerprint_and_a_code_flow()
    {
        var run = Run();
        var ruleIds = run["tool"]!["driver"]!["rules"]!.AsArray().Select(r => (string)r!["id"]!).ToHashSet();
        ruleIds.ShouldContain("SYM-08");
        foreach (var r in run["results"]!.AsArray())
        {
            ruleIds.ShouldContain((string)r!["ruleId"]!);
            var uri = (string)r["locations"]![0]!["physicalLocation"]!["artifactLocation"]!["uri"]!;
            Path.IsPathRooted(uri).ShouldBeFalse();
            uri.ShouldEndWith(".cs");
            ((string?)r["partialFingerprints"]!["bolalensFindingId/v1"]).ShouldNotBeNullOrEmpty();
            r["codeFlows"]![0]!["threadFlows"]![0]!["locations"]!.AsArray().Count.ShouldBeGreaterThan(1);
        }
    }

    [Fact]
    public void Uri_prefix_is_prepended_and_the_document_declares_version_2_1_0()
    {
        var doc = SarifReportWriter.Build(fx.Report, SymbolicRules.Descriptions, uriPrefix: "fixtures/BolaLab");
        ((string?)doc["version"]).ShouldBe("2.1.0");
        var uri = (string)doc["runs"]![0]!["results"]![0]!["locations"]![0]!["physicalLocation"]!["artifactLocation"]!["uri"]!;
        uri.ShouldStartWith("fixtures/BolaLab/src/");
    }
}
