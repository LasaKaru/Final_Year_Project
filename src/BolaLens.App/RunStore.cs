using System.Text.RegularExpressions;
using BolaLens.Core.Json;
using BolaLens.Core.Model;
using BolaLens.Reporting;

namespace BolaLens.App;

/// <summary>Stage-1 context saved next to a report so the UI can show endpoints/policy/schema/DI later.</summary>
public sealed record RunContext(IReadOnlyList<EndpointInfo> Endpoints, PolicyMap Policy, OwnershipSchema Schema,
    IReadOnlyDictionary<string, IReadOnlyList<string>> DiMap);

/// <summary>List entry for a stored run.</summary>
public sealed record RunSummary(string Id, string Folder, bool ReadOnly, DateTimeOffset StartedUtc, string Mode, string Target,
    string? ModelId, int Endpoints, int Vulnerable, int Protected, int Unknown, double TotalSeconds);

/// <summary>Stores scan runs as folders (report.json + context.json) under a writable root and lists
/// read-only roots too (e.g. eval/runs). Run ids are validated to stop path traversal.</summary>
public sealed partial class RunStore(string writableRoot, IReadOnlyList<string>? readOnlyRoots = null)
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,120}$")]
    private static partial Regex IdRegex();

    public string WritableRoot { get; } = Path.GetFullPath(writableRoot);

    public static bool IsValidId(string id) => IdRegex().IsMatch(id) && !id.Contains("..", StringComparison.Ordinal);

    public async Task<string> SaveAsync(ScanResult result, CancellationToken ct)
    {
        var r = result.Report.Run;
        var target = Path.GetFileNameWithoutExtension(r.TargetPath);
        var model = r.ModelId is null ? "" : "-" + Sanitize(r.ModelId);
        var id = $"{r.StartedUtc:yyyyMMdd-HHmmss}-{r.Mode}-{Sanitize(target)}{model}";
        var dir = Path.Combine(WritableRoot, id);
        for (var i = 2; Directory.Exists(dir); i++) dir = Path.Combine(WritableRoot, $"{id}-{i}");
        Directory.CreateDirectory(dir);
        await JsonReportWriter.WriteFileAsync(result.Report, Path.Combine(dir, "report.json"), ct).ConfigureAwait(false);
        var s = result.Symbolic;
        await File.WriteAllTextAsync(Path.Combine(dir, "context.json"), JsonDefaults.Serialize(new RunContext(s.Endpoints, s.Policy, s.Schema, s.DiMap)), ct).ConfigureAwait(false);
        return Path.GetFileName(dir);
    }

    public IReadOnlyList<RunSummary> List()
    {
        var list = new List<RunSummary>();
        foreach (var (root, ro) in Roots())
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var file = Path.Combine(dir, "report.json");
                if (!File.Exists(file)) continue;
                try
                {
                    var rep = JsonReportWriter.Read(file);
                    var c = rep.Coverage;
                    list.Add(new RunSummary(Path.GetFileName(dir), dir, ro, rep.Run.StartedUtc, rep.Run.Mode, rep.Run.TargetPath, rep.Run.ModelId,
                        c.Endpoints, rep.Findings.Where(f => f.Final == VerdictStatus.Vulnerable).Select(f => f.Path.Endpoint.Id).Distinct().Count(),
                        c.SymbolicProtected, c.SymbolicUnknown, rep.Run.TotalSeconds));
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { }
            }
        }
        return list.GroupBy(r => r.Id).Select(g => g.First()).OrderByDescending(r => r.StartedUtc).ToList();
    }

    public string? FolderOf(string id)
    {
        if (!IsValidId(id)) return null;
        foreach (var (root, _) in Roots())
        {
            var dir = Path.Combine(root, id);
            if (File.Exists(Path.Combine(dir, "report.json"))) return dir;
        }
        return null;
    }

    public ScanReport? GetReport(string id) => FolderOf(id) is { } d ? JsonReportWriter.Read(Path.Combine(d, "report.json")) : null;

    public RunContext? GetContext(string id)
    {
        var d = FolderOf(id);
        var f = d is null ? null : Path.Combine(d, "context.json");
        return f is not null && File.Exists(f) ? JsonDefaults.Deserialize<RunContext>(File.ReadAllText(f)) : null;
    }

    public bool Delete(string id)
    {
        if (!IsValidId(id)) return false;
        var dir = Path.Combine(WritableRoot, id);
        if (!File.Exists(Path.Combine(dir, "report.json"))) return false;
        Directory.Delete(dir, recursive: true);
        return true;
    }

    private IEnumerable<(string Root, bool ReadOnly)> Roots()
    {
        yield return (WritableRoot, false);
        foreach (var r in readOnlyRoots ?? []) yield return (Path.GetFullPath(r), true);
    }

    private static string Sanitize(string s) => new(s.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '.' ? ch : '-').ToArray());
}
