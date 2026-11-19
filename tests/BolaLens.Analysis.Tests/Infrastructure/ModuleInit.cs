using System.Runtime.CompilerServices;

namespace BolaLens.Analysis.Tests.Infrastructure;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init() => BolaLens.Analysis.Loading.MsBuildBootstrap.EnsureRegistered();
}
