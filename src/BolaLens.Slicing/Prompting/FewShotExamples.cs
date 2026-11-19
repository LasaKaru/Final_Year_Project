using System.Reflection;

namespace BolaLens.Slicing.Prompting;

/// <summary>Few-shot examples for the prompt, loaded from embedded resources (Prompting/FewShot/*.txt).
/// They are generated from fixtures/Calibration ONLY (different code and entity names) — never from
/// BolaLab TC01–TC16 or evaluation repositories (label leakage, ADR-006).</summary>
public static class FewShotExamples
{
    public static IReadOnlyList<string> Load()
    {
        var asm = typeof(FewShotExamples).Assembly;
        return asm.GetManifestResourceNames()
            .Where(n => n.Contains(".FewShot.", StringComparison.Ordinal) && n.EndsWith(".txt", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => Read(asm, n).Trim())
            .ToList();
    }

    private static string Read(Assembly asm, string name)
    {
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
