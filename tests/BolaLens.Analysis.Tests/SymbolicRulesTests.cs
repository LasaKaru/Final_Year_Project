using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Model;
using static BolaLens.Analysis.Tests.Infrastructure.Domain;

namespace BolaLens.Analysis.Tests;

/// <summary>One minimal snippet per symbolic rule, plus the guard-binding edge cases behind them.</summary>
public class SymbolicRulesTests
{
    private static async Task<SymbolicVerdict> Verdict(string actions, string route, string extraCtor = "", params string[] extra) =>
        (await CompilationFactory.AnalyseAsync([Source, Controller(actions, extraCtor), .. extra])).VerdictOf(route);

    [Fact]
    public async Task SYM01_read_only_endpoint_is_out_of_scope()
    {
        var v = await Verdict("""
            [HttpGet("{id}")] public async Task<IActionResult> Get(int id) => Ok(await db.Orders.FindAsync(id));
            """, "GET api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.OutOfScope);
        v.RuleIds.ShouldBe(["SYM-01"]);
    }

    [Fact]
    public async Task SYM02_id_passed_to_unimplemented_interface_is_unknown()
    {
        var v = await Verdict("""
            [HttpPost("{id}/x")] public async Task<IActionResult> X(int id) { await worker.RunAsync(id); return Ok(); }
            """, "POST api/orders/{id}/x", ", IWorker worker", "namespace T; public interface IWorker { Task RunAsync(int id); }");
        v.Status.ShouldBe(VerdictStatus.Unknown);
        v.RuleIds.ShouldBe(["SYM-02"]);
        v.Rationale.ShouldContain("no source implementation");
    }

    [Fact]
    public async Task SYM03_owner_check_with_exit_before_save_is_protected()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                if (o == null || o.UserId != currentUser.UserId) return NotFound();
                o.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.Protected);
        v.RuleIds.ShouldBe(["SYM-03"]);
    }

    [Fact]
    public async Task SYM03_claims_principal_identity_and_branch_guard()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var o = await db.Orders.SingleAsync(x => x.Id == id);
                if (o.UserId == uid)
                {
                    o.Status = dto.Status;
                    await db.SaveChangesAsync();
                    return Ok();
                }
                return Forbid();
            }
            """, "PUT api/orders/{id}");
        v.RuleIds.ShouldBe(["SYM-03"]);
    }

    [Fact]
    public async Task SYM03_guard_in_called_helper_that_throws()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                EnsureOwner(o!);
                o!.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            private void EnsureOwner(Order o) { if (o.UserId != currentUser.UserId) throw new UnauthorizedAccessException(); }
            """, "PUT api/orders/{id}");
        v.RuleIds.ShouldBe(["SYM-03"]);
    }

    [Fact]
    public async Task Helper_that_only_returns_does_not_protect_the_caller()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                Check(o!);
                o!.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            private void Check(Order o) { if (o.UserId != currentUser.UserId) return; }
            """, "PUT api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldContain("does not precede");
    }

    [Fact]
    public async Task Guard_combined_with_and_is_rejected()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                if (o!.UserId != currentUser.UserId && dto.Status == "x") return Forbid();
                o.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldContain("combined with other conditions");
    }

    [Fact]
    public async Task Tenant_check_on_user_owned_object_is_rejected()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                if (o!.TenantId != currentUser.TenantId) return Forbid();
                o.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldContain("tenant-level check but object has a user owner");
    }

    [Fact]
    public async Task SYM04_tenant_query_filter_protects_tenant_owned_entity()
    {
        var v = await Verdict("""
            [HttpPut("{id}/team")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var t = await db.Teams.FirstAsync(x => x.Id == id);
                t.Name = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}/team");
        v.RuleIds.ShouldBe(["SYM-04"]);
    }

    [Fact]
    public async Task SYM05_resource_authorization_gates_the_change()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                if (!(await auth.AuthorizeAsync(User, o, "Edit")).Succeeded) return Forbid();
                o!.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}", ", IAuthorizationService auth");
        v.RuleIds.ShouldBe(["SYM-05"]);
    }

    [Fact]
    public async Task SYM06_ignore_query_filters_without_guard()
    {
        var v = await Verdict("""
            [HttpDelete("{id}/team")] public async Task<IActionResult> Del(int id)
            {
                var t = await db.Teams.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
                db.Remove(t);
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "DELETE api/orders/{id}/team");
        v.RuleIds.ShouldBe(["SYM-06"]);
    }

    [Fact]
    public async Task SYM07_detached_update_by_client_key()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                db.Orders.Update(new Order { Id = id, Status = dto.Status });
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}");
        v.RuleIds.ShouldBe(["SYM-07"]);
    }

    [Fact]
    public async Task SYM07_bulk_delete_without_owner_predicate_but_owner_predicate_protects()
    {
        var result = await CompilationFactory.AnalyseAsync(Source, Controller("""
            [HttpDelete("{id}")] public Task<int> Del(int id) => db.Orders.Where(o => o.Id == id).ExecuteDeleteAsync();
            [HttpDelete("{id}/mine")] public Task<int> Mine(int id) => db.Orders.Where(o => o.Id == id && o.UserId == currentUser.UserId).ExecuteDeleteAsync();
            [HttpDelete("{id}/either")] public Task<int> Either(int id) => db.Orders.Where(o => o.Id == id || o.UserId == currentUser.UserId).ExecuteDeleteAsync();
            """));
        result.VerdictOf("DELETE api/orders/{id}").RuleIds.ShouldBe(["SYM-07"]);
        result.VerdictOf("DELETE api/orders/{id}/mine").RuleIds.ShouldBe(["SYM-03"]);
        result.VerdictOf("DELETE api/orders/{id}/either").Status.ShouldBe(VerdictStatus.Vulnerable);
    }

    [Fact]
    public async Task SYM08_client_controlled_comparand_is_rejected()
    {
        var v = await Verdict("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id, OwnerDto dto)
            {
                var o = await db.Orders.FindAsync(id);
                if (o!.UserId != dto.UserId) return Forbid();
                o.Status = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}");
        v.RuleIds.ShouldBe(["SYM-08"]);
        v.Rationale.ShouldContain("comparand is client-controlled");
    }

    [Fact]
    public async Task SYM08_list_selected_by_id_and_mutated_in_loop()
    {
        var v = await Verdict("""
            [HttpPost("tenant/{tenantId}/close")] public async Task<IActionResult> Close(int tenantId)
            {
                var all = await db.Orders.Where(o => o.TenantId == tenantId).ToListAsync();
                foreach (var o in all) o.Status = "Closed";
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "POST api/orders/tenant/{tenantId}/close");
        v.RuleIds.ShouldBe(["SYM-08"]);
    }

    [Fact]
    public async Task SYM09_entity_without_owner_is_unknown()
    {
        var v = await Verdict("""
            [HttpPut("{id}/tag")] public async Task<IActionResult> Put(int id, StatusDto dto)
            {
                var t = await db.Tags.FindAsync(id);
                t!.Label = dto.Status;
                await db.SaveChangesAsync();
                return Ok();
            }
            """, "PUT api/orders/{id}/tag");
        v.Status.ShouldBe(VerdictStatus.Unknown);
        v.RuleIds.ShouldBe(["SYM-09"]);
    }

    [Fact]
    public async Task Max_call_depth_is_reported_as_unknown()
    {
        var config = new BolaLens.Core.Config.BolaLensConfig { Analysis = new BolaLens.Core.Config.AnalysisOptions { MaxCallDepth = 1 } };
        var result = await CompilationFactory.AnalyseAsync(config, Source, Controller("""
            [HttpPut("{id}")] public async Task<IActionResult> Put(int id) { await A(id); return Ok(); }
            private Task A(int id) => B(id);
            private async Task B(int id) { var o = await db.Orders.FindAsync(id); o!.Status = "x"; await db.SaveChangesAsync(); }
            """));
        var v = result.VerdictOf("PUT api/orders/{id}");
        v.Status.ShouldBe(VerdictStatus.Unknown);
        v.Rationale.ShouldContain("max call depth");
        result.Paths.Single().Path.Truncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Cross_layer_through_DI_interface_and_generic_repository()
    {
        var result = await CompilationFactory.AnalyseAsync(Source, Controller("""
            [HttpDelete("{id}")] public async Task<IActionResult> Del(int id) { await svc.Cancel(id); return Ok(); }
            """, ", IOrderService svc"), """
            namespace T;
            public interface IRepo<TE> where TE : class { Task<TE?> Get(int id); Task Save(); }
            public class Repo<TE>(Db db) : IRepo<TE> where TE : class
            {
                public async Task<TE?> Get(int id) => await db.Set<TE>().FindAsync(id);
                public Task Save() => db.SaveChangesAsync();
            }
            public interface IOrderService { Task Cancel(int id); }
            public class OrderService(IRepo<Order> repo) : IOrderService
            {
                public async Task Cancel(int id) { var o = await repo.Get(id); o!.Status = "Cancelled"; await repo.Save(); }
            }
            public static class Reg { public static void Add(IServiceCollection s) { s.AddScoped(typeof(IRepo<>), typeof(Repo<>)); s.AddScoped<IOrderService, OrderService>(); } }
            """);
        var v = result.VerdictOf("DELETE api/orders/{id}");
        v.RuleIds.ShouldBe(["SYM-08"]);
        v.Rationale.ShouldContain("Order");
        result.DiMap.Keys.ShouldContain("T.IRepo<TE>");
    }
}
