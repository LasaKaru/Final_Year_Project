using System.Collections.Concurrent;
using BolaLens.App;
using BolaLens.Core.Config;

namespace BolaLens.Web;

public enum JobStatus { Queued, Running, Done, Failed }

/// <summary>A background scan started from the UI, with its progress log.</summary>
public sealed class ScanJob
{
    private readonly List<string> _log = [];
    public required string Id { get; init; }
    public required string Target { get; init; }
    public required string Mode { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string? RunId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;

    public void Log(string line)
    {
        lock (_log) _log.Add($"{DateTimeOffset.Now:HH:mm:ss}  {line}");
    }

    public object Snapshot()
    {
        lock (_log) return new { id = Id, target = Target, mode = Mode, status = Status.ToString().ToLowerInvariant(), runId = RunId, error = Error, log = _log.ToArray() };
    }
}

/// <summary>Runs scans one at a time in the background (MSBuild and the model are heavy) and stores the
/// results in the run store. Limitation: jobs live in memory; finished runs persist on disk.</summary>
public sealed class ScanJobs(RunStore store, ILogger<ScanJobs> logger)
{
    private readonly ConcurrentDictionary<string, ScanJob> _jobs = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ScanJob? Get(string id) => _jobs.TryGetValue(id, out var j) ? j : null;

    public ScanJob Start(string target, ScanMode mode, BolaLensConfig config)
    {
        var job = new ScanJob { Id = Guid.NewGuid().ToString("N")[..12], Target = target, Mode = mode.ToString().ToLowerInvariant() };
        _jobs[job.Id] = job;
        _ = Task.Run(async () =>
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                job.Status = JobStatus.Running;
                job.Log($"Scanning {target} ({job.Mode})");
                var result = await new ScanService(config).ScanAsync(target, mode, CancellationToken.None, new SyncProgress(job.Log)).ConfigureAwait(false);
                job.RunId = await store.SaveAsync(result, CancellationToken.None).ConfigureAwait(false);
                job.Log($"Saved run {job.RunId}");
                job.Status = JobStatus.Done;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "scan failed");
                job.Error = ex.Message;
                job.Log("Failed: " + ex.Message);
                job.Status = JobStatus.Failed;
            }
            finally
            {
                _gate.Release();
            }
        });
        return job;
    }

    /// <summary>Synchronous progress (Progress&lt;T&gt; posts to the thread pool and could reorder lines).</summary>
    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
