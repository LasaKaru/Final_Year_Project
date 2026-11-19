namespace BolaLens.Core.Model;

/// <summary>What the scan covered and where it gave up — reported so Unknowns are never hidden.</summary>
public sealed record CoverageManifest(
    int ProjectsLoaded, int ProjectsFailed, int Endpoints, int EndpointsWithIdParams,
    int PathsExtracted, int PathsTruncated, int UnresolvedCalls,
    int SymbolicVulnerable, int SymbolicProtected, int SymbolicUnknown,
    int SemanticParseFailures,
    int SymbolicOutOfScope = 0, int SemanticInvocations = 0, int UnsupportedEndpointStyles = 0);

/// <summary>Reproducibility metadata (NFR05): tool/config/model/prompt identity, timings and memory.</summary>
public sealed record RunInfo(
    string ToolVersion, string Mode, string TargetPath, string? TargetCommit,
    string ConfigHash, string? ModelId, string? ModelSha256, string? PromptVersion,
    DateTimeOffset StartedUtc, double TotalSeconds, double? ModelLoadSeconds,
    long PeakWorkingSetBytes, string Machine,
    IReadOnlyDictionary<string, double>? PhaseSeconds = null);

public sealed record ScanReport(RunInfo Run, CoverageManifest Coverage, IReadOnlyList<Finding> Findings, IReadOnlyList<UnresolvedItem> LoadDiagnostics);
