using BolaLens.Core.Model;

namespace BolaLens.Integration.Tests;

/// <summary>Intermediate outputs on BolaLab compared with the oracles in guide 06 §7.</summary>
public class ContextOracleTests(BolaLabFixture fx)
{
    [Theory]
    [InlineData("Order", "UserId:User,TenantId:Tenant", "", "User")]
    [InlineData("Project", "TenantId:Tenant", "Tenant", "Tenant")]
    [InlineData("Note", "OwnerUserId:User,TenantId:Tenant", "Tenant", "User")]
    [InlineData("Document", "OwnerUserId:User", "", "User")]
    [InlineData("DocumentShare", "UserId:User", "", "User")]
    [InlineData("Invoice", "CustomerId:User", "", "User")]
    public void Ownership_schema(string entity, string owners, string filterScope, string required)
    {
        var e = fx.Symbolic.Schema.Find($"BolaLab.Api.Domain.{entity}").ShouldNotBeNull();
        string.Join(",", e.OwnerCandidates.Select(o => $"{o.Property}:{o.Kind}")).ShouldBe(owners);
        string.Join(",", e.QueryFilters.Select(f => f.ScopeKind)).ShouldBe(filterScope);
        e.RequiredOwnerKind.ToString().ShouldBe(required);
        e.KeyProperties.ShouldBe(["Id"]);
    }

    [Fact]
    public void Policy_map()
    {
        var p = fx.Symbolic.Policy;
        p.AuthenticationSchemes.ShouldBe(["JwtBearer"]);
        p.FallbackRequiresAuthenticatedUser.ShouldBeTrue();
        var policy = p.Policies.ShouldHaveSingleItem();
        policy.Name.ShouldBe("DocumentEdit");
        policy.Requirements.ShouldBe(["DocumentEditRequirement"]);
        var h = p.ResourceHandlers.ShouldHaveSingleItem();
        (h.HandlerType, h.ResourceType).ShouldBe(("DocumentEditHandler", "BolaLab.Api.Domain.Document"));
    }

    [Fact]
    public void Di_map()
    {
        var di = fx.Symbolic.DiMap;
        di["BolaLab.Api.Infrastructure.ICurrentUser"].ShouldBe(["BolaLab.Api.Infrastructure.HttpCurrentUser"]);
        di["BolaLab.Api.Services.IInvoiceRepository"].ShouldBe(["BolaLab.Api.Services.InvoiceRepository"]);
        di["BolaLab.Api.Services.IInvoiceService"].ShouldBe(["BolaLab.Api.Services.InvoiceService"]);
        di["BolaLab.Api.Services.IOrderProcessor"].Count.ShouldBe(2);
        di["Microsoft.AspNetCore.Authorization.IAuthorizationHandler"].ShouldBe(["BolaLab.Api.Authorization.DocumentEditHandler"]);
    }

    [Fact]
    public void Root_config_file_equals_the_compiled_defaults_and_points_to_the_committed_model()
    {
        var file = BolaLens.Core.Config.BolaLensConfig.Load(Path.Combine(BolaLabFixture.RepoRoot, "bolalens.json"));
        file.Hash().ShouldBe(BolaLens.Core.Config.BolaLensConfig.Default.Hash());
        File.Exists(Path.Combine(BolaLabFixture.RepoRoot, file.Inference.ModelPath, "genai_config.json")).ShouldBeTrue();
    }

    [Fact]
    public void Endpoint_inventory()
    {
        var eps = fx.Symbolic.Endpoints;
        eps.Count.ShouldBe(16);
        eps.ShouldAllBe(e => e.Authorization.HasAuthorize);
        var del = eps.Single(e => e.Id == "DELETE api/orders/{id}");
        del.Parameters.Select(p => (p.Name, p.Source, p.IsIdLike)).ShouldBe([("id", BindingSource.Route, true), ("request", BindingSource.Body, true)]);
        eps.Single(e => e.Id == "POST api/orders/{id}/process").Parameters[1].Source.ShouldBe(BindingSource.Query);
    }
}
