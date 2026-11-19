using BolaLens.Analysis.Loading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BolaLens.Analysis.Tests;

/// <summary>.NET 10 projects using C# 14 syntax (Kavita) must parse with the bundled Roslyn after the upgrade to Preview.</summary>
public class LanguageVersionTests
{
    private const string Code = """
        public static class StringExtensions
        {
            extension(string s)
            {
                public bool IsBlank => string.IsNullOrWhiteSpace(s);
            }
        }
        public class Use { public bool M(string x) => x.IsBlank; }
        """;

    private static Solution Create(params string[] symbols)
    {
        var ws = new AdhocWorkspace();
        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols);
        var project = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "P", "P", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), parseOptions: options,
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]));
        return ws.AddDocument(project.Id, "E.cs", Microsoft.CodeAnalysis.Text.SourceText.From(Code)).Project.Solution;
    }

    private static async Task<int> ParseErrors(Solution s)
    {
        var c = await s.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        return c!.SyntaxTrees.Sum(t => t.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public async Task Net10_projects_are_parsed_with_preview_syntax()
    {
        var net10 = Create("NET10_0_OR_GREATER");
        (await ParseErrors(net10)).ShouldBeGreaterThan(0);                 // C# 14 syntax fails under Latest (Roslyn 4.14)
        (await ParseErrors(WorkspaceLoader.UpgradeLanguageVersion(net10))).ShouldBe(0);
    }

    [Fact]
    public void Older_targets_keep_their_language_version()
    {
        var net8 = Create("NET8_0_OR_GREATER");
        var upgraded = WorkspaceLoader.UpgradeLanguageVersion(net8);
        ((CSharpParseOptions)upgraded.Projects.Single().ParseOptions!).LanguageVersion.ShouldNotBe(LanguageVersion.Preview);
    }
}
