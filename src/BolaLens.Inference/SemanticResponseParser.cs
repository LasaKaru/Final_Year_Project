using System.Text.Json;
using System.Text.RegularExpressions;
using BolaLens.Core.Model;

namespace BolaLens.Inference;

/// <summary>Parsed model answer (before fusion).</summary>
public sealed record ParsedResponse(bool Ok, VerdictStatus Status, double? Confidence, string Reason, IReadOnlyList<string> Evidence, string? Error);

/// <summary>Extracts the first JSON object from raw model text (brace matching that ignores braces in
/// strings), validates the schema and grounds the evidence: only line IDs that exist in the slice are
/// kept; a non-unknown answer with no grounded evidence becomes Unknown ("ungrounded answer").
/// Input: raw text + slice. Limitation: no repair of malformed JSON beyond extraction.</summary>
public static partial class SemanticResponseParser
{
    [GeneratedRegex(@"^L\d+$")]
    private static partial Regex LineId();

    public static ParsedResponse Parse(string raw, IrSlice slice)
    {
        var json = ExtractFirstObject(raw);
        if (json is null) return Fail("no JSON object in output");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("status", out var st) || st.ValueKind != JsonValueKind.String) return Fail("missing 'status'");
            var status = st.GetString()!.Trim().ToLowerInvariant() switch
            {
                "vulnerable" => VerdictStatus.Vulnerable,
                "protected" => VerdictStatus.Protected,
                "unknown" => VerdictStatus.Unknown,
                _ => (VerdictStatus?)null,
            };
            if (status is null) return Fail($"invalid status '{st.GetString()}'");

            double? confidence = null;
            if (root.TryGetProperty("confidence", out var c))
            {
                if (c.ValueKind != JsonValueKind.Number || c.GetDouble() is < 0 or > 1) return Fail("confidence must be a number in [0,1]");
                confidence = c.GetDouble();
            }
            else return Fail("missing 'confidence'");

            var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()!.Trim() : "";
            if (reason.Length == 0) return Fail("missing 'reason'");
            if (reason.Length > 400) reason = reason[..400];

            var evidence = new List<string>();
            if (root.TryGetProperty("evidence", out var ev) && ev.ValueKind == JsonValueKind.Array)
                evidence.AddRange(ev.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim()).Where(e => LineId().IsMatch(e)));

            var grounded = evidence.Where(slice.HasLine).Distinct().ToList();
            if (status != VerdictStatus.Unknown && grounded.Count == 0)
                return new ParsedResponse(true, VerdictStatus.Unknown, confidence, "ungrounded answer: " + reason, grounded, null);
            return new ParsedResponse(true, status.Value, confidence, reason, grounded, null);
        }
        catch (JsonException ex)
        {
            return Fail("invalid JSON: " + ex.Message);
        }

        static ParsedResponse Fail(string error) => new(false, VerdictStatus.Unknown, null, error, [], error);
    }

    public static string? ExtractFirstObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;
        int depth = 0;
        bool inString = false, escape = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escape) escape = false;
                else if (ch == '\\') escape = true;
                else if (ch == '"') inString = false;
                continue;
            }
            if (ch == '"') inString = true;
            else if (ch == '{') depth++;
            else if (ch == '}' && --depth == 0) return text[start..(i + 1)];
        }
        return null;
    }
}
