namespace BolaLens.Web;

/// <summary>Folders the UI works with and helpers to find scan targets and label files under the root.</summary>
public sealed class UiOptions(string root, string runsDir, string modelsDir, string? configPath)
{
    private static readonly string[] SkipDirs = ["bin", "obj", "node_modules", ".git", ".bolalens", ".model-work", "models"];

    public string Root { get; } = root;
    public string RunsDir { get; } = runsDir;
    public string ModelsDir { get; } = modelsDir;
    public string? ConfigPath { get; } = configPath;

    /// <summary>The nearest folder (walking up) that contains BolaLens.sln — `dotnet run` starts in the project folder.</summary>
    public static string FindRepositoryRoot(string start)
    {
        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "BolaLens.sln"))) return d.FullName;
        return start;
    }

    /// <summary>Absolute path for user input; relative paths are relative to the root.</summary>
    public string? Resolve(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Root, path));

    /// <summary>.sln/.slnx files up to four levels below the root, fixtures first (excluding the tool's own solution).</summary>
    public IReadOnlyList<string> DiscoverTargets()
    {
        var found = new List<string>();
        Walk(Root, 0, found);
        return found.Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Where(f => f != "BolaLens.sln")
            .OrderBy(f => f.StartsWith("fixtures/BolaLab", StringComparison.Ordinal) ? 0 : f.StartsWith("fixtures/", StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static void Walk(string dir, int depth, List<string> found)
    {
        if (depth > 4 || found.Count > 50) return;
        try
        {
            found.AddRange(Directory.GetFiles(dir, "*.sln").Concat(Directory.GetFiles(dir, "*.slnx")));
            foreach (var sub in Directory.GetDirectories(dir))
                if (!SkipDirs.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase)) Walk(sub, depth + 1, found);
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    /// <summary>expected.json next to a solution is its default label set (e.g. fixtures/BolaLab/expected.json).</summary>
    public IReadOnlyDictionary<string, string> DefaultLabels() =>
        DiscoverTargets().Where(t => File.Exists(Path.Combine(Root, Path.GetDirectoryName(t) ?? "", "expected.json")))
            .ToDictionary(t => t, t => (Path.GetDirectoryName(t) is { Length: > 0 } d ? d + "/" : "") + "expected.json");
}
