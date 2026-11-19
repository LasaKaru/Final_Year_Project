using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

public class EndpointEnumeratorTests
{
    private const string Controllers = """
        namespace E;
        public record UpdateReq(int OrderId, string Status);
        [ApiController, Route("api/[controller]"), Authorize(Roles = "Clerk")]
        public class WidgetsController : ControllerBase
        {
            [HttpPut("{id:int}")] public IActionResult Put(int id, UpdateReq req) => Ok();
            [HttpGet("/absolute/{key}")] public IActionResult Abs(string key) => Ok();
            [HttpDelete("{id}"), AllowAnonymous] public IActionResult Del([FromRoute] int id, [FromQuery] string reason) => Ok();
            [HttpPost] public IActionResult Create([FromBody] UpdateReq req) => Ok();
            public IActionResult NoVerb() => Ok();
            [HttpPatch("{id}"), Authorize(Policy = "Edit")] public IActionResult Patch(int id, [FromServices] ILogger log, int page) => Ok();
        }
        public interface ILogger { }
        public class NotAController { [HttpGet("x")] public void X() { } }
        """;

    private static async Task<IReadOnlyList<EndpointInfo>> Endpoints() => (await CompilationFactory.AnalyseAsync(Controllers)).Endpoints;

    [Fact]
    public async Task Replaces_controller_token_and_normalises_constraints()
    {
        var eps = await Endpoints();
        eps.ShouldContain(e => e.Id == "PUT api/widgets/{id}");
        eps.Single(e => e.Action == "Put").RouteTemplate.ShouldBe("api/Widgets/{id:int}");
    }

    [Fact]
    public async Task Absolute_method_template_ignores_class_route()
    {
        (await Endpoints()).ShouldContain(e => e.Id == "GET absolute/{key}");
    }

    [Fact]
    public async Task Infers_binding_sources_and_id_like_parameters()
    {
        var put = (await Endpoints()).Single(e => e.Action == "Put");
        put.Parameters[0].ShouldBe(new ParameterInfo("id", "int", BindingSource.Route, true));
        put.Parameters[1].Source.ShouldBe(BindingSource.Body);
        put.Parameters[1].IsIdLike.ShouldBeTrue();
        put.Parameters[1].IdPropertyPath.ShouldBe("req.OrderId");

        var del = (await Endpoints()).Single(e => e.Action == "Del");
        del.Parameters.Select(p => p.Source).ShouldBe([BindingSource.Route, BindingSource.Query]);

        var patch = (await Endpoints()).Single(e => e.Action == "Patch");
        patch.Parameters.Select(p => p.Source).ShouldBe([BindingSource.Route, BindingSource.Service, BindingSource.Query]);
    }

    [Fact]
    public async Task Method_AllowAnonymous_overrides_class_Authorize_and_reads_roles_and_policies()
    {
        var eps = await Endpoints();
        var del = eps.Single(e => e.Action == "Del").Authorization;
        del.AllowAnonymous.ShouldBeTrue();
        del.Roles.ShouldContain("Clerk");
        eps.Single(e => e.Action == "Patch").Authorization.Policies.ShouldContain("Edit");
        eps.Single(e => e.Action == "Put").Authorization.HasAuthorize.ShouldBeTrue();
    }

    [Fact]
    public async Task Ignores_actions_without_verbs_and_non_controllers()
    {
        var eps = await Endpoints();
        eps.Count.ShouldBe(5);
        eps.ShouldNotContain(e => e.Action == "NoVerb" || e.Controller == "NotAController");
    }

    [Fact]
    public async Task Enumerates_minimal_api_endpoints_with_group_prefix_and_authorization()
    {
        var result = await CompilationFactory.AnalyseAsync("""
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            namespace M;
            public record Rename(string Name);
            public static class Startup
            {
                public static void Map(WebApplication app)
                {
                    var g = app.MapGroup("/api/things").RequireAuthorization();
                    g.MapPut("/{thingId:int}", (int thingId, Rename body) => Results.Ok());
                    app.MapDelete("/open/{slug}", Delete);
                }
                private static IResult Delete(string slug) => Results.NoContent();
            }
            """);
        var put = result.Endpoints.Single(e => e.Verb == HttpVerb.Put);
        put.Id.ShouldBe("PUT api/things/{thingid}");
        put.Authorization.HasAuthorize.ShouldBeTrue();
        put.Parameters.Single(p => p.Name == "thingId").Source.ShouldBe(BindingSource.Route);
        put.Parameters.Single(p => p.Name == "body").Source.ShouldBe(BindingSource.Body);
        var del = result.Endpoints.Single(e => e.Verb == HttpVerb.Delete);
        del.Id.ShouldBe("DELETE open/{slug}");
        del.Authorization.HasAuthorize.ShouldBeFalse();
        del.Parameters.Single().IsIdLike.ShouldBeTrue(); // every route parameter selects a resource, not only *Id names
        result.UnsupportedEndpointStyles.ShouldBe(0);
    }

    [Theory]
    [InlineData("api/Orders/{id:int}/", "api/orders/{id}")]
    [InlineData("/api/x/{Id:guid?}", "api/x/{id}")]
    [InlineData("files/{**path}", "files/{path}")]
    public void Route_normalisation(string input, string expected) => Routes.Normalise(input).ShouldBe(expected);
}
