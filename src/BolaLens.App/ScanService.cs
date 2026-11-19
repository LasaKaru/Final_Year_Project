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

namespace BolaLens.App;

public enum ScanMode { Symbolic, Hybrid }

/// <summary>Result of one scan: the report plus the Stage-1 context (endpoints, policy, schema, DI) for inspection.</summary>
public sealed record ScanResult(ScanReport Report, SymbolicResult Symbolic);

/// <summary>Runs the whole pipeline for one target: load → Stage 1 (symbolic) → slices → Stage 2
/// (Null or ONNX validator, same candidates) → fusion → ScanReport with run info and coverage.
/// The validator factory is injectable so tests can run "hybrid" with a scripted fake model.</summary>
public sealed class ScanService(BolaLensConfig config, Func<BolaLensConfig, ISemanticValidator>? validatorFactory = null)
{
    public static string ToolVersion =>
        typeof(ScanService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0";

    public async Task<ScanResult> ScanAsync(string target, ScanMode mode, CancellationToken ct, IProgress<string>? progress = null)
    {
        var started = DateTimeOffset.UtcNow;
        var total = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        progress?.Report("Loading solution (MSBuildWorkspace)…");
        using var loaded = await new WorkspaceLoader().LoadAsync(target, ct).ConfigureAwait(false);
        var loadSeconds = sw.Elapsed.TotalSeconds;

        progress?.Report("Stage 1: endpoints, policy, schema, DI, taint paths, rules…");
        var symbolic = await new SymbolicPipeline(config).RunAsync(loaded.Solution, loaded.RootDirectory, loaded.Diagnostics, ct: ct).ConfigureAwait(false);

        sw.Restart();
        var slicer = new IrSliceGenerator(symbolic.Policy, symbolic.Schema, config.Inference.MaxSliceTokens);
        var fusion = new FusionPolicy(config.Inference.SuppressThreshold);
        if (mode == ScanMode.Hybrid) progress?.Report($"Loading local model {config.Inference.ModelPath}…");
        using var validator = mode == ScanMode.Hybrid
            ? (validatorFactory ?? DefaultValidator)(config)
            : new NullSemanticValidator();
        var chatFormat = (validator as OnnxSemanticValidator)?.Model?.ChatFormat
                         ?? (config.Inference.ChatFormat is ChatFormat.ChatMl ? ChatFormat.ChatMl : ChatFormat.Phi3);
        var prompts = new PromptBuilder(config.Inference.PromptVersion, chatFormat: chatFormat);

        ModelGateResult? gate = null;
        if (validator.IsEnabled && config.Inference.CalibrationGate)
        {
            progress?.Report("Calibration gate: testing the model on labelled probe slices…");
            gate = await RunGateAsync(validator, prompts, config.Inference.GateMinCorrect, ct).ConfigureAwait(false);
            progress?.Report($"Calibration gate {(gate.Passed ? "passed" : "FAILED (model is advisory only)")}: {gate.Correct}/{gate.Total}");
        }
        var advisory = gate is { Passed: false };

        var findings = new List<Finding>();
        var candidates = symbolic.Paths.Count(p => FusionPolicy.IsCandidate(p.Verdict));
        var done = 0;
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
                {
                    progress?.Report($"Stage 2: SLM {++done}/{candidates} — {path.Endpoint.Id}");
                    semantic = await validator.ValidateAsync(slice, prompts.BuildPair(slice), ct).ConfigureAwait(false);
                }
            }
            var (final, reason) = fusion.Fuse(verdict, semantic, advisory);
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
            validator.IsEnabled ? ModelManifestHash((validator as OnnxSemanticValidator)?.Model?.Path ?? config.Inference.ModelPath) : null,
            $"{prompts.Version}/{prompts.Format} ({prompts.Hash})",
            started,
            Math.Round(total.Elapsed.TotalSeconds, 3),
            validator.ModelLoadSeconds is { } mls ? Math.Round(mls, 3) : null,
            Process.GetCurrentProcess().PeakWorkingSet64,
            $"{RuntimeInformation.OSDescription.Trim()}; {RuntimeInformation.ProcessArchitecture}; {Environment.ProcessorCount} logical cores; .NET {Environment.Version}",
            phases,
            gate);

        var coverage = CoverageManifestBuilder.Build(symbolic.ProjectsLoaded, symbolic.ProjectsWithErrors, symbolic.Endpoints, findings, symbolic.UnsupportedEndpointStyles);
        progress?.Report("Done.");
        return new ScanResult(new ScanReport(run, coverage, findings, symbolic.Diagnostics), symbolic);
    }

    /// <summary>Asks the model about each calibration probe; it passes with at least <paramref name="minCorrect"/> right answers.</summary>
    public static async Task<ModelGateResult> RunGateAsync(ISemanticValidator validator, PromptBuilder prompts, int minCorrect, CancellationToken ct)
    {
        var probes = CalibrationProbes.Load();
        var answers = new List<string>();
        var correct = 0;
        foreach (var probe in probes)
        {
            var v = await validator.ValidateAsync(probe.Slice, prompts.BuildPair(probe.Slice), ct).ConfigureAwait(false);
            var ok = v.ParseOk && v.Status == probe.Expected;
            if (ok) correct++;
            answers.Add($"{probe.Name}: expected {probe.Expected}, got {v.Status}{(v.ParseOk ? "" : " (parse failure)")}");
        }
        return new ModelGateResult(correct >= minCorrect, correct, probes.Count, minCorrect, answers);
    }

    private static ISemanticValidator DefaultValidator(BolaLensConfig cfg)
    {
        var dir = Path.GetFullPath(cfg.Inference.ModelPath);
        if (!ModelInfo.IsModelFolder(dir))
            throw new FileNotFoundException($"No local model at '{cfg.Inference.ModelPath}' (folder with genai_config.json). Run scripts/ensure-model.sh (or .ps1), see models/README.md, or pass --model.");
        var info = ModelInfo.Inspect(dir, cfg.Inference.ChatFormat);
        return new OnnxSemanticValidator(info, cfg.Inference.MaxNewTokens, cfg.Inference.MaxLength, cfg.Inference.RetryOnParseFailure, cfg.Inference.JsonGuidance);
    }

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
