using BolaLens.Core.Model;
using BolaLens.Inference;

namespace BolaLens.Inference.Tests;

public class SemanticResponseParserTests
{
    internal static readonly IrSlice Slice = new("p1",
    [
        new IrLine("L1", "ENDPOINT", "PUT api/x/{id}", null),
        new IrLine("L2", "LOAD", "var x = await db.Xs.FindAsync(id);", null),
        new IrLine("L3", "SINK", "await db.SaveChangesAsync();", null),
    ], 30, 0);

    [Fact]
    public void Parses_a_valid_answer_with_surrounding_prose()
    {
        var r = SemanticResponseParser.Parse("Sure! {\"status\":\"vulnerable\",\"confidence\":0.8,\"reason\":\"no check {}\",\"evidence\":[\"L2\",\"L3\"]} trailing", Slice);
        r.Ok.ShouldBeTrue();
        r.Status.ShouldBe(VerdictStatus.Vulnerable);
        r.Confidence.ShouldBe(0.8);
        r.Reason.ShouldBe("no check {}");
        r.Evidence.ShouldBe(["L2", "L3"]);
    }

    [Fact]
    public void Drops_evidence_lines_that_do_not_exist_and_ungrounded_answers_become_unknown()
    {
        var partly = SemanticResponseParser.Parse("""{"status":"protected","confidence":0.9,"reason":"ok","evidence":["L3","L99","x"]}""", Slice);
        partly.Evidence.ShouldBe(["L3"]);
        partly.Status.ShouldBe(VerdictStatus.Protected);

        var none = SemanticResponseParser.Parse("""{"status":"protected","confidence":0.9,"reason":"trust me","evidence":["L42"]}""", Slice);
        none.Ok.ShouldBeTrue();
        none.Status.ShouldBe(VerdictStatus.Unknown);
        none.Reason.ShouldStartWith("ungrounded answer");
    }

    [Theory]
    [InlineData("no json here", "no JSON object")]
    [InlineData("""{"status":"maybe","confidence":0.5,"reason":"r","evidence":[]}""", "invalid status")]
    [InlineData("""{"status":"vulnerable","confidence":1.5,"reason":"r","evidence":["L1"]}""", "confidence")]
    [InlineData("""{"status":"vulnerable","reason":"r","evidence":["L1"]}""", "missing 'confidence'")]
    [InlineData("""{"status":"vulnerable","confidence":0.5,"evidence":["L1"]}""", "missing 'reason'")]
    [InlineData("""{"status":"vulnerable", "confidence": 0.5""", "no JSON object")]
    public void Rejects_invalid_answers(string raw, string error)
    {
        var r = SemanticResponseParser.Parse(raw, Slice);
        r.Ok.ShouldBeFalse();
        r.Status.ShouldBe(VerdictStatus.Unknown);
        r.Error.ShouldNotBeNull().ShouldContain(error);
    }

    [Fact]
    public void Brace_matching_ignores_braces_inside_strings()
    {
        SemanticResponseParser.ExtractFirstObject("x {\"a\":\"}{\\\"}\",\"b\":{\"c\":1}} y").ShouldBe("{\"a\":\"}{\\\"}\",\"b\":{\"c\":1}}");
    }
}
