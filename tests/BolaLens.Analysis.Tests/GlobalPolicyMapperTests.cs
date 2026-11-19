using BolaLens.Analysis.Tests.Infrastructure;

namespace BolaLens.Analysis.Tests;

public class GlobalPolicyMapperTests
{
    private const string Program = """
        namespace P;
        public class Doc { public int Id { get; set; } public string OwnerId { get; set; } = ""; }
        public class EditRequirement : IAuthorizationRequirement { }
        public class EditHandler : AuthorizationHandler<EditRequirement, Doc>
        {
            protected override Task HandleRequirementAsync(AuthorizationHandlerContext c, EditRequirement r, Doc d) => Task.CompletedTask;
        }
        public static class Setup
        {
            public static void Configure(IServiceCollection services, string dynamicName)
            {
                services.AddAuthentication().AddCookie();
                services.AddAuthorization(o =>
                {
                    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                    o.AddPolicy("Admins", p => p.RequireRole("admin"));
                    o.AddPolicy("Edit", p => p.AddRequirements(new EditRequirement()));
                    o.AddPolicy(dynamicName, p => p.RequireAuthenticatedUser());
                });
                services.AddControllers(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter()));
            }
        }
        """;

    [Fact]
    public async Task Maps_schemes_fallback_policies_filters_and_handlers()
    {
        var policy = (await CompilationFactory.AnalyseAsync(Program)).Policy;
        policy.AuthenticationSchemes.ShouldBe(["Cookies"]);
        policy.FallbackRequiresAuthenticatedUser.ShouldBeTrue();
        policy.GlobalAuthorizeFilter.ShouldBeTrue();
        policy.Policies.Select(p => p.Name).ShouldBe(["Admins", "Edit"]);
        policy.Policies.Single(p => p.Name == "Admins").Requirements.ShouldBe(["RequireRole(admin)"]);
        policy.Policies.Single(p => p.Name == "Edit").Requirements.ShouldBe(["EditRequirement"]);
        var h = policy.ResourceHandlers.ShouldHaveSingleItem();
        (h.HandlerType, h.RequirementType, h.ResourceType).ShouldBe(("EditHandler", "EditRequirement", "P.Doc"));
    }

    [Fact]
    public async Task Dynamic_policy_names_are_reported_as_unresolved()
    {
        var policy = (await CompilationFactory.AnalyseAsync(Program)).Policy;
        policy.Unresolved.ShouldContain(u => u.Reason.Contains("dynamic policy registration"));
    }

    [Fact]
    public async Task No_fallback_when_none_is_configured()
    {
        var policy = (await CompilationFactory.AnalyseAsync("namespace Q; public static class S { public static void C(IServiceCollection s) => s.AddAuthorization(); }")).Policy;
        policy.FallbackRequiresAuthenticatedUser.ShouldBeFalse();
        policy.Policies.ShouldBeEmpty();
    }
}
