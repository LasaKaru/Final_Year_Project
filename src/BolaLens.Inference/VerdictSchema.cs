namespace BolaLens.Inference;

/// <summary>JSON schema of the SLM answer. Used twice: to constrain decoding (ONNX Runtime GenAI
/// guidance, so even tiny models emit well-formed JSON) and, independently, by the response parser.</summary>
public static class VerdictSchema
{
    public const string Json = """
        {"type":"object",
         "properties":{
           "status":{"type":"string","enum":["vulnerable","protected","unknown"]},
           "confidence":{"type":"number","minimum":0,"maximum":1},
           "reason":{"type":"string","maxLength":240},
           "evidence":{"type":"array","items":{"type":"string","pattern":"^L[0-9]{1,3}$"},"maxItems":6}},
         "required":["status","confidence","reason","evidence"],
         "additionalProperties":false}
        """;
}
