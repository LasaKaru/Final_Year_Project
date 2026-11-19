using BolaLens.Core.Model;

namespace BolaLens.Reporting;

/// <summary>Counts what the scan covered and where it gave up (NFR07: Unknowns and failures are visible).</summary>
public static class CoverageManifestBuilder
{
    public static CoverageManifest Build(int projectsLoaded, int projectsFailed, IReadOnlyList<EndpointInfo> endpoints,
        IReadOnlyList<Finding> findings, int unsupportedEndpointStyles)
    {
        var paths = findings.Select(f => f.Path).ToList();
        return new CoverageManifest(
            ProjectsLoaded: projectsLoaded,
            ProjectsFailed: projectsFailed,
            Endpoints: endpoints.Count,
            EndpointsWithIdParams: endpoints.Count(e => e.Parameters.Any(p => p.IsIdLike)),
            PathsExtracted: paths.Count,
            PathsTruncated: paths.Count(p => p.Truncated),
            UnresolvedCalls: paths.Sum(p => p.Unresolved.Count),
            SymbolicVulnerable: findings.Count(f => f.Symbolic.Status == VerdictStatus.Vulnerable),
            SymbolicProtected: findings.Count(f => f.Symbolic.Status == VerdictStatus.Protected),
            SymbolicUnknown: findings.Count(f => f.Symbolic.Status == VerdictStatus.Unknown),
            SemanticParseFailures: findings.Count(f => f.Semantic is { ParseOk: false }),
            SymbolicOutOfScope: findings.Count(f => f.Symbolic.Status == VerdictStatus.OutOfScope),
            SemanticInvocations: findings.Count(f => f.Semantic is not null),
            UnsupportedEndpointStyles: unsupportedEndpointStyles);
    }
}
