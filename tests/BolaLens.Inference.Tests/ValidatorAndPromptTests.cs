using BolaLens.Core.Abstractions;
using BolaLens.Core.Model;
using BolaLens.Inference;
using BolaLens.Slicing;
using BolaLens.Slicing.Prompting;

namespace BolaLens.Inference.Tests;

public class ValidatorAndPromptTests
{
    [Fact]
    public async Task Retries_once_with_suffix_then_succeeds()
    {
        var prompts = new List<string>();
        var answers = new Queue<string>(["I think it is fine.", """{"status":"vulnerable","confidence":0.7,"reason":"r","evidence":["L2"]}"""]);
        using var v = new OnnxSemanticValidator((p, _) => { prompts.Add(p); return (answers.Dequeue(), 1, 1); }, "fake");
        var verdict = await v.ValidateAsync(SemanticResponseParserTests.Slice, new SemanticPrompt("PROMPT", "RETRY"), TestContext.Current.CancellationToken);
        verdict.ParseOk.ShouldBeTrue();
        verdict.Attempts.ShouldBe(2);
        verdict.Status.ShouldBe(VerdictStatus.Vulnerable);
        prompts.ShouldBe(["PROMPT", "RETRY"]);
    }

    [Fact]
    public async Task Two_parse_failures_give_unknown_with_parse_ok_false()
    {
        using var v = new OnnxSemanticValidator((_, _) => ("nope", 1, 1), "fake");
        var verdict = await v.ValidateAsync(SemanticResponseParserTests.Slice, new SemanticPrompt("PROMPT", "RETRY"), TestContext.Current.CancellationToken);
        verdict.ParseOk.ShouldBeFalse();
        verdict.Status.ShouldBe(VerdictStatus.Unknown);
        verdict.Reason.ShouldContain("parse failure after 2 attempt(s)");
    }

    [Fact]
    public void Prompt_contains_three_calibration_examples_and_the_slice_but_no_labels()
    {
        var builder = new PromptBuilder();
        var prompt = builder.Build(SemanticResponseParserTests.Slice);
        FewShotExamples.Load().Count.ShouldBe(3);
        prompt.ShouldContain("### Example A");
        prompt.ShouldContain("### Example C");
        prompt.ShouldContain("Booking");
        prompt.ShouldContain("var x = await db.Xs.FindAsync(id);");
        prompt.ShouldStartWith("<|user|>");
        prompt.TrimEnd().ShouldEndWith("<|assistant|>");
        var pair = builder.BuildPair(SemanticResponseParserTests.Slice);
        pair.Primary.ShouldBe(prompt);
        pair.Retry.ShouldContain(PromptBuilder.RetrySuffix + "\n<|end|>");
        prompt.ShouldNotContain("{{");
        prompt.ShouldNotContain("SYM-");
        // Few-shot examples must not reuse BolaLab entities (leakage, ADR-006)
        foreach (var name in new[] { "Order", "Invoice", "Project", "Note", "Document" })
            string.Join("\n", FewShotExamples.Load()).ShouldNotContain(name);
        builder.Hash.Length.ShouldBe(16);
    }

    [Fact]
    public void Slice_generator_tags_lines_and_truncates_to_budget()
    {
        var src = new SourceRef("C.cs", 1, 1, "var o = await db.Orders.FindAsync(id);");
        var endpoint = new EndpointInfo("PUT api/o/{id}", HttpVerb.Put, "api/o/{id}", "OController", "Put", "Put(int)",
            [new ParameterInfo("id", "int", BindingSource.Route, true)], new AuthorizeInfo(true, false, [], []), src);
        var steps = new List<PathStep> { new(StepKind.Source, "id : int (route, client-controlled)", null, src) };
        for (var i = 0; i < 60; i++) steps.Add(new PathStep(StepKind.Check, "condition", null, src with { StartLine = i + 2, Snippet = $"if (o.Flag{i} == {i}) return Ok();" }));
        steps.Add(new PathStep(StepKind.Sink, "state change: SaveChanges Order", null, src with { StartLine = 99, Snippet = "await db.SaveChangesAsync();" }));
        var path = new ExecutionPath("p", endpoint, endpoint.Parameters[0], steps, [], [], [], false);

        var small = new IrSliceGenerator(PolicyMap.Empty, OwnershipSchema.Empty, maxTokens: 300).Generate(path);
        small.TruncatedLines.ShouldBeGreaterThan(0);
        small.Lines.ShouldContain(l => l.Tag == "TRUNCATED");
        small.Lines[^1].Tag.ShouldBe("SINK");
        small.Lines[0].Tag.ShouldBe("ENDPOINT");
        small.Lines.Select(l => l.LineId).ShouldBe(Enumerable.Range(1, small.Lines.Count).Select(i => $"L{i}"));

        var full = new IrSliceGenerator(PolicyMap.Empty, OwnershipSchema.Empty, maxTokens: 100_000).Generate(path);
        full.TruncatedLines.ShouldBe(0);
        full.Lines.Count(l => l.Tag == "CHECK").ShouldBe(60);
    }

    [Fact]
    public void ChatMl_wrapping_and_hash_depends_on_format()
    {
        var phi = new PromptBuilder();
        var chatml = new PromptBuilder(chatFormat: ChatFormat.ChatMl);
        var p = chatml.Build(SemanticResponseParserTests.Slice);
        p.ShouldStartWith("<|im_start|>system");
        p.ShouldEndWith("<|im_start|>assistant\n");
        p.ShouldNotContain("<|user|>");
        chatml.BuildPair(SemanticResponseParserTests.Slice).Retry.ShouldContain(PromptBuilder.RetrySuffix + "<|im_end|>");
        chatml.Hash.ShouldNotBe(phi.Hash);
        phi.Content(SemanticResponseParserTests.Slice).ShouldBe(chatml.Content(SemanticResponseParserTests.Slice));
    }

    [Fact]
    public void Calibration_probes_are_balanced_and_disjoint_from_few_shot()
    {
        var probes = CalibrationProbes.Load();
        probes.Count.ShouldBe(4);
        probes.Count(p => p.Expected == VerdictStatus.Vulnerable).ShouldBe(2);
        probes.Count(p => p.Expected == VerdictStatus.Protected).ShouldBe(2);
        var fewShot = string.Join("\n", FewShotExamples.Load());
        foreach (var p in probes)
        {
            p.Slice.Lines[0].Tag.ShouldBe("ENDPOINT");
            p.Slice.HasLine("L4").ShouldBeTrue();
            fewShot.ShouldNotContain(p.Slice.Lines[0].Text);
        }
    }

    /// <summary>Runs only with a real model folder: BOLALENS_MODEL_PATH=models/smollm2-135m-instruct-int4 (or Phi-3).</summary>
    [Fact]
    [Trait("Category", "RequiresModel")]
    public async Task Real_onnx_model_returns_schema_valid_json_with_guidance()
    {
        var dir = Environment.GetEnvironmentVariable("BOLALENS_MODEL_PATH");
        if (string.IsNullOrWhiteSpace(dir) || !ModelInfo.IsModelFolder(dir)) Assert.Skip("BOLALENS_MODEL_PATH not set to a model folder");
        var info = ModelInfo.Inspect(dir);
        using var v = new OnnxSemanticValidator(info, maxNewTokens: 120);
        var probe = CalibrationProbes.Load()[0];
        var verdict = await v.ValidateAsync(probe.Slice, new PromptBuilder(chatFormat: info.ChatFormat).BuildPair(probe.Slice), TestContext.Current.CancellationToken);
        verdict.ParseOk.ShouldBeTrue(verdict.RawOutput);
        verdict.Attempts.ShouldBe(1);
        v.ModelLoadSeconds.ShouldNotBeNull();
    }
}
