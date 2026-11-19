using System.Text.RegularExpressions;
using BolaLens.Core.Model;

namespace BolaLens.Slicing.Prompting;

/// <summary>A labelled slice from fixtures/Calibration used to test a model before trusting it.</summary>
public sealed record CalibrationProbe(string Name, VerdictStatus Expected, IrSlice Slice);

/// <summary>Loads the calibration-gate probes (Prompting/Probes/*.&lt;expected&gt;.txt). Probes come from
/// fixtures/Calibration, are disjoint from the few-shot examples and from all evaluation data.</summary>
public static partial class CalibrationProbes
{
    [GeneratedRegex(@"^(L\d+)\s+\[([A-Z]+)\]\s?(.*)$")]
    private static partial Regex LineRegex();

    public static IReadOnlyList<CalibrationProbe> Load()
    {
        var asm = typeof(CalibrationProbes).Assembly;
        var list = new List<CalibrationProbe>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.Contains(".Probes.", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            using var s = asm.GetManifestResourceStream(name)!;
            using var r = new StreamReader(s);
            var parts = name.Split('.');
            var expected = Enum.Parse<VerdictStatus>(parts[^2], ignoreCase: true);
            list.Add(new CalibrationProbe(parts[^3], expected, Parse(parts[^3], r.ReadToEnd())));
        }
        return list;
    }

    /// <summary>Parses slice text ("L1  [TAG] text") back into an IrSlice.</summary>
    public static IrSlice Parse(string id, string text)
    {
        var lines = text.Split('\n').Select(l => LineRegex().Match(l.TrimEnd('\r'))).Where(m => m.Success)
            .Select(m => new IrLine(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, null)).ToList();
        var slice = new IrSlice(id, lines, 0, 0);
        return slice with { EstimatedTokens = TokenEstimator.Estimate(slice.Text) };
    }
}
