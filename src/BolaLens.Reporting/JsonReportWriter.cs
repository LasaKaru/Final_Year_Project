using System.Text.Json;
using BolaLens.Core.Abstractions;
using BolaLens.Core.Json;
using BolaLens.Core.Model;

namespace BolaLens.Reporting;

/// <summary>Writes report.json (System.Text.Json, camelCase, enums as strings) and reads it back for eval.</summary>
public sealed class JsonReportWriter : IReportWriter
{
    public Task WriteAsync(ScanReport report, Stream output, CancellationToken ct) =>
        JsonSerializer.SerializeAsync(output, report, JsonDefaults.Options, ct);

    public static async Task WriteFileAsync(ScanReport report, string path, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await using var fs = File.Create(path);
        await new JsonReportWriter().WriteAsync(report, fs, ct).ConfigureAwait(false);
    }

    public static ScanReport Read(string path) => JsonDefaults.Deserialize<ScanReport>(File.ReadAllText(path));
}
