using BolaLens.Core.Config;
using BolaLens.Core.Json;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

/// <summary>Every Core record must survive a JSON round trip (reports are read back by eval and the UI).</summary>
public class ModelSerializationTests
{
    private static readonly SourceRef Src = new("C.cs", 3, 4, "var x = 1;");

    private static T RoundTrip<T>(T value) => JsonDefaults.Deserialize<T>(JsonDefaults.Serialize(value));

    [Fact]
    public void Enums_serialise_as_strings_and_nulls_are_omitted()
    {
        var json = JsonDefaults.Serialize(new ParameterInfo("id", "int", BindingSource.Route, true));
        json.ShouldContain("\"source\": \"Route\"");
        json.ShouldNotContain("idPropertyPath");
    }

    [Fact]
    public void Endpoint_and_policy_round_trip()
    {
        var ep = new EndpointInfo("PUT api/x/{id}", HttpVerb.Put, "api/x/{id}", "XController", "Put", "X.Put(int)",
            [new ParameterInfo("id", "int", BindingSource.Route, true)], new AuthorizeInfo(true, false, ["P"], ["R"]), Src);
        var back = RoundTrip(ep);
        back.Id.ShouldBe(ep.Id);
        back.Parameters.ShouldBe(ep.Parameters);
        back.Authorization.Policies.ShouldBe(["P"]);

        var pm = new PolicyMap(["JwtBearer"], true, false, [new PolicyDefinition("P", ["Req"], Src)], [new ResourceHandlerInfo("H", "Req", "Doc", Src)], []);
        RoundTrip(pm).ResourceHandlers.Single().HandlerType.ShouldBe("H");
    }

    [Fact]
    public void Schema_path_and_report_round_trip()
    {
        var entity = new EntityModel("N.Order", "Db", "Orders", ["Id"], [new OwnerCandidate("UserId", OwnerKind.User, [OwnerEvidence.NamePattern])],
            [new QueryFilterInfo("o.TenantId == t", ["TenantId"], OwnerKind.Tenant, Src)]);
        RoundTrip(new OwnershipSchema([entity], [])).Entities.Single().RequiredOwnerKind.ShouldBe(OwnerKind.User);

        var ep = new EndpointInfo("PUT a", HttpVerb.Put, "a", "C", "A", "S", [], new AuthorizeInfo(false, false, [], []), Src);
        var path = new ExecutionPath("p", ep, new ParameterInfo("id", "int", BindingSource.Route, true), [new PathStep(StepKind.Load, "loads", null, Src, 1)],
            [new SinkInfo(SinkKind.SaveChanges, "N.Order", true, false, false, false, "S", Src, [0])],
            [new GuardEvidence(GuardKind.OwnerEquality, true, null, "UserId", "uid", Src, OwnerKind.User)], [new UnresolvedItem("why", null)], false, true);
        var finding = new Finding("f", path, new SymbolicVerdict(VerdictStatus.Protected, ["SYM-03"], "r"),
            new SemanticVerdict(VerdictStatus.Vulnerable, 0.5, "x", ["L1"], true, "m", 3, "{}", 2), VerdictStatus.Protected, "kept", "L1 [X] y");
        var report = new ScanReport(
            new RunInfo("0.1.0", "hybrid", "t.sln", null, "h", "m", null, "v1", DateTimeOffset.UnixEpoch, 1, 0.5, 10, "os",
                new Dictionary<string, double> { ["load"] = 1 }, new ModelGateResult(false, 2, 4, 3, ["a"])),
            new CoverageManifest(1, 0, 1, 1, 1, 0, 1, 0, 1, 0, 0), [finding], []);

        var back = RoundTrip(report);
        var f = back.Findings.Single();
        f.Path.Sinks.Single().ValidGuards.ShouldBe([0]);
        f.Path.Steps.Single().Depth.ShouldBe(1);
        f.Path.TaintEnteredUnresolvedCall.ShouldBeTrue();
        f.Semantic!.Attempts.ShouldBe(2);
        back.Run.ModelGate!.Answers.ShouldBe(["a"]);
        back.Run.PhaseSeconds!["load"].ShouldBe(1);
    }

    [Fact]
    public void Config_partial_file_overrides_only_given_keys_and_hash_changes()
    {
        var file = Path.Combine(Path.GetTempPath(), $"bolalens-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, """{ "analysis": { "maxCallDepth": 2 }, "inference": { "jsonGuidance": false } }""");
        try
        {
            var cfg = BolaLensConfig.Load(file);
            cfg.Analysis.MaxCallDepth.ShouldBe(2);
            cfg.Analysis.IdParameterPatterns.ShouldBe(BolaLensConfig.Default.Analysis.IdParameterPatterns);
            cfg.Inference.JsonGuidance.ShouldBeFalse();
            cfg.Inference.CalibrationGate.ShouldBeTrue();
            cfg.Ownership.UserTypeNames.ShouldContain("ApplicationUser");
            cfg.Hash().ShouldNotBe(BolaLensConfig.Default.Hash());
            BolaLensConfig.Load(null).Hash().ShouldBe(BolaLensConfig.Default.Hash());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void With_model_uses_folder_name_as_model_id() =>
        BolaLensConfig.Default.WithModel("models/smollm2-135m-instruct-int4/").Inference.ModelId.ShouldBe("smollm2-135m-instruct-int4");
}
