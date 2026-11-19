using System.CommandLine;
using BolaLens.Analysis.Loading;
using BolaLens.Analysis.Pipeline;
using BolaLens.Core.Config;
using BolaLens.Core.Json;

namespace BolaLens.Cli.Commands;

/// <summary>Shared arguments/options and helpers for all commands (exit codes: 0 ok / no vulnerable
/// findings, 1 vulnerable findings present, 2 load or usage error).</summary>
public static class Common
{
    public const int Ok = 0;
    public const int VulnerableFound = 1;
    public const int LoadError = 2;

    public static Argument<string> PathArgument() => new("path") { Description = ".sln, .slnx or .csproj to analyse" };

    public static Option<string?> OutOption() => new("--out", "-o") { Description = "Write the JSON output to this file instead of stdout" };

    public static Option<string?> ConfigOption() => new("--config", "-c") { Description = "bolalens.json configuration file (defaults are compiled in)" };

    public static Option<bool> VerboseOption() => new("--verbose", "-v") { Description = "Print diagnostics and per-phase timings to stderr" };

    public static Option<string?> EndpointOption() => new("--endpoint", "-e") { Description = "Only this endpoint, e.g. \"PUT api/orders/{id}\" or an action name" };

    public static BolaLensConfig LoadConfig(string? path) => BolaLensConfig.Load(path);

    /// <summary>Loads the target and runs Stage 1; prints a friendly error and returns null on failure.</summary>
    public static async Task<SymbolicResult?> AnalyseAsync(string path, BolaLensConfig config, string? endpoint, bool verbose, CancellationToken ct)
    {
        try
        {
            using var loaded = await new WorkspaceLoader().LoadAsync(path, ct);
            var result = await new SymbolicPipeline(config).RunAsync(loaded.Solution, loaded.RootDirectory, loaded.Diagnostics, endpoint, ct);
            if (verbose) PrintDiagnostics(result);
            return result;
        }
        catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidOperationException or IOException)
        {
            Error(ex.Message);
            return null;
        }
    }

    public static void PrintDiagnostics(SymbolicResult r)
    {
        foreach (var d in r.Diagnostics) Console.Error.WriteLine($"[diag] {d.Reason}");
        Console.Error.WriteLine("[time] " + string.Join(", ", r.PhaseSeconds.Select(kv => $"{kv.Key}={kv.Value:0.000}s")));
    }

    /// <summary>Spectre console that still renders tables when stdout is redirected (CI logs, pipes),
    /// where the detected terminal width is 0.</summary>
    public static Spectre.Console.IAnsiConsole CreateConsole()
    {
        var console = Spectre.Console.AnsiConsole.Create(new Spectre.Console.AnsiConsoleSettings
        {
            Out = new Spectre.Console.AnsiConsoleOutput(Console.Out),
        });
        if (Console.IsOutputRedirected || console.Profile.Width < 40)
            console.Profile.Width = int.TryParse(Environment.GetEnvironmentVariable("COLUMNS"), out var c) && c >= 40 ? c : 160;
        return console;
    }

    public static void Error(string message) => Console.Error.WriteLine($"bolalens: error: {message}");

    public static async Task WriteJsonAsync<T>(T value, string? outPath, CancellationToken ct)
    {
        var json = JsonDefaults.Serialize(value);
        if (string.IsNullOrWhiteSpace(outPath))
        {
            Console.Out.WriteLine(json);
            return;
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(outPath, json, ct);
        Console.Error.WriteLine($"wrote {outPath}");
    }
}
