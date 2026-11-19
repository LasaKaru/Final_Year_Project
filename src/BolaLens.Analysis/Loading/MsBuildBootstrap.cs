using Microsoft.Build.Locator;

namespace BolaLens.Analysis.Loading;

/// <summary>Registers the installed .NET SDK's MSBuild exactly once.
/// Input: none. MUST run before any type from Microsoft.Build / MSBuildWorkspace is touched.
/// Limitation: picks the default (newest compatible) SDK instance found by MSBuildLocator.</summary>
public static class MsBuildBootstrap
{
    private static readonly object Gate = new();

    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (!MSBuildLocator.IsRegistered && MSBuildLocator.CanRegister)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}
