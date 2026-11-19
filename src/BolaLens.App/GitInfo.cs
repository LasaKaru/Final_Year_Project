namespace BolaLens.App;

/// <summary>Reads the HEAD commit of the repository that contains the target, without running git
/// (no process execution, no network). Returns null when the target is not in a git work tree.</summary>
public static class GitInfo
{
    public static string? HeadCommit(string startDirectory)
    {
        try
        {
            for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
            {
                var git = Path.Combine(dir.FullName, ".git");
                if (!Directory.Exists(git)) continue;
                var head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
                var refName = head[5..];
                var refFile = Path.Combine(git, refName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(refFile)) return File.ReadAllText(refFile).Trim();
                var packed = Path.Combine(git, "packed-refs");
                if (File.Exists(packed))
                    return File.ReadLines(packed).Select(l => l.Split(' ')).FirstOrDefault(p => p.Length == 2 && p[1] == refName)?[0];
                return null;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }
}
