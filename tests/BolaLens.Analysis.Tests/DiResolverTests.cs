using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Model;
using static BolaLens.Analysis.Tests.Infrastructure.Domain;

namespace BolaLens.Analysis.Tests;

public class DiResolverTests
{
    private const string Services = """
        namespace T;
        public interface IA { Task Go(int id); }
        public class A1(Db db) : IA { public async Task Go(int id) { var o = await db.Orders.FindAsync(id); o!.Status = "1"; await db.SaveChangesAsync(); } }
        public class A2 : IA { public Task Go(int id) => Task.CompletedTask; }
        public static class Reg { public static void Add(IServiceCollection s) { s.AddScoped<IA, A1>(); } }
        public interface IB { Task Go(int id); }
        public class B1 : IB { public Task Go(int id) => Task.CompletedTask; }
        public class B2 : IB { public Task Go(int id) => Task.CompletedTask; }
        public static class Reg2 { public static void Add(IServiceCollection s) { s.AddTransient(typeof(IB), typeof(B1)); s.AddTransient(typeof(IB), typeof(B2)); } }
        """;

    [Fact]
    public async Task Di_map_lists_generic_and_typeof_registrations()
    {
        var result = await CompilationFactory.AnalyseAsync(Source, Services);
        result.DiMap["T.IA"].ShouldBe(["T.A1"]);
        result.DiMap["T.IB"].ShouldBe(["T.B1", "T.B2"]);
    }

    [Fact]
    public async Task Registration_disambiguates_two_source_implementations()
    {
        var result = await CompilationFactory.AnalyseAsync(Source, Services,
            Controller("""[HttpPut("{id}")] public async Task<IActionResult> P(int id) { await a.Go(id); return Ok(); }""", ", IA a"));
        result.VerdictOf("PUT api/orders/{id}").RuleIds.ShouldBe(["SYM-08"]); // resolved to A1 (registered)
    }

    [Fact]
    public async Task Two_registered_implementations_are_ambiguous()
    {
        var result = await CompilationFactory.AnalyseAsync(Source, Services,
            Controller("""[HttpPut("{id}")] public async Task<IActionResult> P(int id) { await b.Go(id); return Ok(); }""", ", IB b"));
        var v = result.VerdictOf("PUT api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.Unknown);
        v.Rationale.ShouldContain("ambiguous dispatch: 2 implementations");
    }
}
