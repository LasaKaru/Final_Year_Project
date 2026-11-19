using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BolaLens.Analysis.Loading;
using BolaLens.Analysis.Pipeline;
using BolaLens.Core.Abstractions;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using BolaLens.Inference;
using BolaLens.Reporting;
using BolaLens.Slicing;
using BolaLens.Slicing.Prompting;

namespace BolaLens.Cli.Scanning;

public enum ScanMode { Symbolic, Hybrid }

/// <summary>Runs the whole pipeline for one target: load → Stage 1 (symbolic) → slices → Stage 2
/// (Null or ONNX validator, same candidates) → fusion → ScanReport with run info and coverage.
/// The validator factory is injectable so tests can run "hybrid" with a scripted fake model.</summary>
public sealed class ScanService(BolaLensConfig config, Func<BolaLensConfig, ISemanticValidator>? validatorFactory = null)
{
    public static string ToolVersion =>
        typeof(ScanService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0";

    public async Task<(ScanReport Report, SymbolicResult Symbolic)> ScanAsync(string target, ScanMode mode, CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var total = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        using var loaded = await new WorkspaceLoader().LoadAsync(target, ct).ConfigureAwait(false);
        var loadSeconds = sw.Elapsed.TotalSeconds;

        var symbolic = await new SymbolicPipeline(config).RunAsync(loaded.Solution, loaded.RootDirectory, loaded.Diagnostics, ct: ct).ConfigureAwait(false);

        sw.Restart();
        var slicer = new IrSliceGenerator(symbolic.Policy, symbolic.Schema, config.Inference.MaxSliceTokens);
        var prompts = new PromptBuilder(config.Inference.PromptVersion);
        var fusion = new FusionPolicy(config.Inference.SuppressThreshold);
        using var validator = mode == ScanMode.Hybrid
            ? (validatorFactory ?? DefaultValidator)(config)
            : new NullSemanticValidator();

        var findings = new List<Finding>();
        foreach (var (path, verdict) in symbolic.Paths)
        {
            ct.ThrowIfCancellationRequested();
            SemanticVerdict? semantic = null;
            string? sliceText = null;
            if (FusionPolicy.IsCandidate(verdict))
            {
                var slice = slicer.Generate(path);
                sliceText = slice.Text;
                if (validator.IsEnabled)
                    semantic = await validator.ValidateAsync(slice, prompts.Build(slice), ct).ConfigureAwait(false);
            }
            var (final, reason) = fusion.Fuse(verdict, semantic);
            findings.Add(new Finding(FindingIdFactory.Create(path), path, verdict, semantic, final, reason, sliceText));
        }
        var semanticSeconds = sw.Elapsed.TotalSeconds;

        var phases = new Dictionary<string, double>(symbolic.PhaseSeconds)
        {
            ["load"] = Math.Round(loadSeconds, 3),
            ["sliceAndInfer"] = Math.Round(semanticSeconds, 3),
        };
        var run = new RunInfo(
            ToolVersion,
            mode.ToString().ToLowerInvariant(),
            RelativeTarget(target),
            GitInfo.HeadCommit(Path.GetDirectoryName(Path.GetFullPath(target))!),
            config.Hash(),
            validator.IsEnabled ? validator.ModelId : null,
            validator.IsEnabled ? ModelManifestHash(config.Inference.ModelPath) : null,
            $"{prompts.Version} ({prompts.Hash})",
            started,
            Math.Round(total.Elapsed.TotalSeconds, 3),
            validator.ModelLoadSeconds is { } mls ? Math.Round(mls, 3) : null,
            Process.GetCurrentProcess().PeakWorkingSet64,
            $"{RuntimeInformation.OSDescription.Trim()}; {RuntimeInformation.ProcessArchitecture}; {Environment.ProcessorCount} logical cores; .NET {Environment.Version}",
            phases);

        var coverage = CoverageManifestBuilder.Build(symbolic.ProjectsLoaded, symbolic.ProjectsWithErrors, symbolic.Endpoints, findings, symbolic.UnsupportedEndpointStyles);
        return (new ScanReport(run, coverage, findings, symbolic.Diagnostics), symbolic);
    }

    private static ISemanticValidator DefaultValidator(BolaLensConfig cfg) =>
        new OnnxSemanticValidator(Path.GetFullPath(cfg.Inference.ModelPath), cfg.Inference.ModelId, cfg.Inference.MaxLength, cfg.Inference.RetryOnParseFailure);

    private static string RelativeTarget(string target)
    {
        var full = Path.GetFullPath(target);
        var rel = Path.GetRelativePath(Environment.CurrentDirectory, full);
        return (rel.StartsWith("..", StringComparison.Ordinal) ? full : rel).Replace('\\', '/');
    }

    /// <summary>SHA-256 over genai_config.json, the *.onnx graph files and the sizes of external data files.
    /// (Full per-file hashes are recorded once in models/README.md — hashing GBs on every scan is too slow.)</summary>
    public static string? ModelManifestHash(string modelDir)
    {
        if (!Directory.Exists(modelDir)) return null;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.GetFiles(modelDir).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(name));
            if (name.EndsWith(".json", StringComparison.Ordinal) || name.EndsWith(".onnx", StringComparison.Ordinal))
                sha.AppendData(File.ReadAllBytes(file));
            else
                sha.AppendData(BitConverter.GetBytes(new FileInfo(file).Length));
        }
        return "manifest:" + Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }
}
