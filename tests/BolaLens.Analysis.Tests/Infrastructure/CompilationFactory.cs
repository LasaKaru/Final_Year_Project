using BolaLens.Analysis.Pipeline;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BolaLens.Analysis.Tests.Infrastructure;

/// <summary>Builds in-memory solutions (AdhocWorkspace) that can reference ASP.NET Core and EF Core,
/// because the test project has the AspNetCore framework reference and an EF Core package reference.</summary>
public static class CompilationFactory
{
    private static readonly Lazy<MetadataReference[]> Refs = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray());

    public const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Security.Claims;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.AspNetCore.Authorization;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;

        """;

    public static Solution Create(params string[] sources)
    {
        var ws = new AdhocWorkspace();
        var pid = ProjectId.CreateNewId();
        var solution = ws.CurrentSolution
            .AddProject(pid, "Fixture", "Fixture", LanguageNames.CSharp)
            .WithProjectCompilationOptions(pid, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithProjectParseOptions(pid, new CSharpParseOptions(LanguageVersion.Latest))
            .AddMetadataReferences(pid, Refs.Value);
        for (var i = 0; i < sources.Length; i++)
            solution = solution.AddDocument(DocumentId.CreateNewId(pid), $"F{i}.cs", Usings + sources[i]);
        return solution;
    }

    public static async Task AssertCompilesAsync(Solution s)
    {
        var errors = (await s.Projects.Single().GetCompilationAsync())!
            .GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.ShouldBeEmpty(string.Join("\n", errors));
    }

    /// <summary>Compiles the sources (asserting no errors) and runs the whole Stage-1 pipeline.</summary>
    public static async Task<SymbolicResult> AnalyseAsync(params string[] sources) => await AnalyseAsync(BolaLensConfig.Default, sources);

    public static async Task<SymbolicResult> AnalyseAsync(BolaLensConfig config, params string[] sources)
    {
        var solution = Create(sources);
        await AssertCompilesAsync(solution);
        return await new SymbolicPipeline(config).RunAsync(solution, "", ct: TestContext.Current.CancellationToken);
    }

    /// <summary>Worst symbolic verdict over the paths of one endpoint (unit = endpoint).</summary>
    public static SymbolicVerdict VerdictOf(this SymbolicResult result, string verbAndRoute)
    {
        var key = Routes.UnitKey(verbAndRoute.Split(' ')[0], verbAndRoute.Split(' ')[1]);
        var paths = result.Paths.Where(p => p.Path.Endpoint.Id == key).ToList();
        paths.ShouldNotBeEmpty($"no paths for {key}; endpoints: {string.Join(", ", result.Endpoints.Select(e => e.Id))}");
        return paths.MaxBy(p => VerdictOrdering.Rank(p.Verdict.Status)).Verdict;
    }
}

/// <summary>A tiny domain shared by the rule tests (Order: user-owned; Team: tenant-owned + filter; Tag: no owner).</summary>
public static class Domain
{
    public const string Source = """
        namespace T;
        public class Order { public int Id { get; set; } public string UserId { get; set; } = ""; public int TenantId { get; set; } public string Status { get; set; } = ""; }
        public class Team { public int Id { get; set; } public int TenantId { get; set; } public string Name { get; set; } = ""; }
        public class Tag { public int Id { get; set; } public string Label { get; set; } = ""; }
        public interface ICurrentUser { string UserId { get; } int TenantId { get; } }
        public record StatusDto(string Status);
        public record OwnerDto(string UserId, string Status);
        public class Db(DbContextOptions<Db> o, ICurrentUser cu) : DbContext(o)
        {
            public DbSet<Order> Orders => Set<Order>();
            public DbSet<Team> Teams => Set<Team>();
            public DbSet<Tag> Tags => Set<Tag>();
            protected override void OnModelCreating(ModelBuilder b) => b.Entity<Team>().HasQueryFilter(t => t.TenantId == cu.TenantId);
        }
        """;

    /// <summary>Wraps action bodies into an [Authorize] controller with Db + ICurrentUser injected.</summary>
    public static string Controller(string actions, string extraCtorParams = "") => $$"""
        namespace T;
        [ApiController, Route("api/[controller]"), Authorize]
        public class OrdersController(Db db, ICurrentUser currentUser{{extraCtorParams}}) : ControllerBase
        {
        {{actions}}
        }
        """;
}
