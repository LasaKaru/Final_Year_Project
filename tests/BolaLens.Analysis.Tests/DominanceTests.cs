using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

/// <summary>S6 dominance: an owner check that exits (return/throw on mismatch) protects a change only if it runs on every
/// path to it — i.e. it sits, before the change, in a block that encloses the change.</summary>
public class DominanceTests
{
    private const string Domain = """
        namespace D;
        public class Order { public int Id { get; set; } public int UserId { get; set; } public decimal Total { get; set; } }
        public class Ctx(DbContextOptions<Ctx> o) : DbContext(o) { public DbSet<Order> Orders => Set<Order>(); }
        public interface ICurrentUser { int UserId { get; } }
        public class OrderService(Ctx db)
        {
            public async Task Save(Order o, decimal total) { o.Total = total; await db.SaveChangesAsync(); }
        }
        """;

    private static Task<BolaLens.Analysis.Pipeline.SymbolicResult> Analyse(string body) => CompilationFactory.AnalyseAsync(Domain, $$"""
        namespace D;
        [ApiController, Route("api/orders"), Authorize]
        public class OrdersController(Ctx db, ICurrentUser me, OrderService svc) : ControllerBase
        {
            [HttpPut("{id}")]
            public async Task<IActionResult> Put(int id, decimal total, bool express)
            {
                var o = await db.Orders.FirstAsync(x => x.Id == id);
        {{body}}
                return Ok();
            }
        }
        """);

    [Theory]
    [InlineData("if (o.UserId != me.UserId) return Forbid(); await svc.Save(o, total);", VerdictStatus.Protected)]
    [InlineData("if (express) { if (o.UserId != me.UserId) return Forbid(); } await svc.Save(o, total);", VerdictStatus.Vulnerable)]
    [InlineData("if (express) { if (o.UserId != me.UserId) return Forbid(); await svc.Save(o, total); }", VerdictStatus.Protected)]
    [InlineData("foreach (var _ in new int[0]) { if (o.UserId != me.UserId) return Forbid(); } await svc.Save(o, total);", VerdictStatus.Vulnerable)]
    [InlineData("if (express) return Ok(); else if (o.UserId != me.UserId) return Forbid(); await svc.Save(o, total);", VerdictStatus.Protected)]
    [InlineData("if (express) { } else if (o.UserId != me.UserId) return Forbid(); await svc.Save(o, total);", VerdictStatus.Vulnerable)]
    // the change inside the branch the check selects (Oqtane: if (authorized) { … } else { Forbid; return null; })
    [InlineData("if (o.UserId == me.UserId) { await svc.Save(o, total); } else { return Forbid(); }", VerdictStatus.Protected)]
    [InlineData("if (o.UserId != me.UserId) return Forbid(); else { await svc.Save(o, total); }", VerdictStatus.Protected)]
    [InlineData("if (o.UserId == me.UserId) { } else { await svc.Save(o, total); return Forbid(); }", VerdictStatus.Vulnerable)]
    public async Task Exit_guards_must_dominate_the_change(string body, VerdictStatus expected)
    {
        var v = (await Analyse(body)).VerdictOf("PUT api/orders/{id}");
        v.Status.ShouldBe(expected);
        if (expected == VerdictStatus.Vulnerable && body.Contains("express) {"))
            v.Rationale.ShouldContain("only on some paths");
    }
}
