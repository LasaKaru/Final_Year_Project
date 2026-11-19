using BolaLens.Core.Model;

namespace BolaLens.Core.Abstractions;

/// <summary>Writes a scan report to a stream (JSON now; SARIF post-IPD).</summary>
public interface IReportWriter
{
    Task WriteAsync(ScanReport report, Stream output, CancellationToken ct);
}
