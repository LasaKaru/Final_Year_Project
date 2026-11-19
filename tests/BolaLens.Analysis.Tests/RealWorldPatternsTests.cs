using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Config;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

/// <summary>Patterns found when scanning real open-source APIs (Dataset B): Minimal APIs, mediator/CQRS dispatch,
/// slug selectors, owners reached through navigations, method-based current-user accessors, existence checks,
/// principal-derived identity and the secondary read-BOLA mode.</summary>
public class RealWorldPatternsTests
{
    private const string Domain = """
        namespace R;
        public class Person { public int PersonId { get; set; } public string Username { get; set; } = ""; }
        public class Article { public int ArticleId { get; set; } public string Slug { get; set; } = ""; public string Title { get; set; } = ""; public Person? Author { get; set; } }
        public class Note { public int Id { get; set; } public string OwnerId { get; set; } = ""; public string Text { get; set; } = ""; }
        public interface ICurrentUserAccessor { int? GetCurrentPersonId(); }
        public interface IPermissions { Person GetUser(ClaimsPrincipal user); }
        public class Ctx(DbContextOptions<Ctx> o) : DbContext(o)
        {
            public DbSet<Article> Articles => Set<Article>();
            public DbSet<Person> Persons => Set<Person>();
            public DbSet<Note> Notes => Set<Note>();
        }
        public interface IRequest { }
        public interface ISender { Task Send(IRequest request); }
        """;

    private const string Mediator = """
        namespace R;
        public record DeleteArticle(string Slug) : IRequest;
        public record DeleteArticleChecked(string Slug) : IRequest;
        public class DeleteHandler(Ctx db)
        {
            public async Task Handle(DeleteArticle message, CancellationToken ct)
            {
                var article = await db.Articles.FirstOrDefaultAsync(x => x.Slug == message.Slug, ct) ?? throw new KeyNotFoundException();
                db.Articles.Remove(article);
                await db.SaveChangesAsync(ct);
            }
        }
        public class DeleteCheckedHandler(Ctx db, ICurrentUserAccessor me)
        {
            public async Task Handle(DeleteArticleChecked message, CancellationToken ct)
            {
                var article = await db.Articles.Include(x => x.Author).FirstOrDefaultAsync(x => x.Slug == message.Slug, ct) ?? throw new KeyNotFoundException();
                if (article.Author?.PersonId != me.GetCurrentPersonId()) throw new UnauthorizedAccessException();
                db.Articles.Remove(article);
                await db.SaveChangesAsync(ct);
            }
        }
        [ApiController, Route("articles"), Authorize]
        public class ArticlesController(ISender sender) : ControllerBase
        {
            [HttpDelete("{slug}")] public Task Delete(string slug) => sender.Send(new DeleteArticle(slug));
            [HttpDelete("{slug}/checked")] public Task DeleteChecked(string slug) => sender.Send(new DeleteArticleChecked(slug));
        }
        """;

    [Fact]
    public async Task Mediator_dispatch_slug_selector_and_navigation_owner()
    {
        var result = await CompilationFactory.AnalyseAsync(Domain, Mediator);
        result.Schema.Find("R.Article")!.OwnerCandidates.ShouldContain(o => o.Property == "Author" && o.Kind == OwnerKind.User);

        var plain = result.VerdictOf("DELETE articles/{slug}");
        plain.RuleIds.ShouldBe(["SYM-08"]);
        var path = result.Paths.First(p => p.Path.Endpoint.Id == "DELETE articles/{slug}").Path;
        path.Steps.ShouldContain(s => s.Kind == StepKind.Call && s.Description.Contains("dispatches DeleteArticle to DeleteHandler.Handle"));

        result.VerdictOf("DELETE articles/{slug}/checked").RuleIds.ShouldBe(["SYM-03"]);
        result.Paths.First(p => p.Path.Endpoint.Id == "DELETE articles/{slug}/checked").Path.Guards
            .ShouldContain(g => g.IsValid && g.OwnerProperty == "Author.PersonId");
    }

    [Fact]
    public async Task Minimal_api_lambda_handler_is_analysed()
    {
        var result = await CompilationFactory.AnalyseAsync(Domain, """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            namespace R;
            public static class Api
            {
                public static void Map(WebApplication app)
                {
                    var notes = app.MapGroup("/notes").RequireAuthorization();
                    notes.MapPut("/{id}", async (int id, Ctx db) =>
                    {
                        var n = await db.Notes.FindAsync(id);
                        n!.Text = "x";
                        await db.SaveChangesAsync();
                        return Results.NoContent();
                    });
                }
            }
            """);
        var v = result.VerdictOf("PUT notes/{id}");
        v.RuleIds.ShouldBe(["SYM-08"]);
        result.Endpoints.Single().Parameters.Single(p => p.Name == "db").Source.ShouldBe(BindingSource.Service);
    }

    private const string ExistsController = """
        namespace R;
        [ApiController, Route("notes"), Authorize]
        public class NotesController(Ctx db) : ControllerBase
        {
            [HttpPut("{id}")]
            public async Task<IActionResult> Put(int id)
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!await db.Notes.AnyAsync(n => n.Id == id && n.OwnerId == uid)) return Forbid();
                var note = await db.Notes.FindAsync(id);
                note!.Text = "x";
                await db.SaveChangesAsync();
                return Ok();
            }

            [HttpPut("{id}/weak")]
            public async Task<IActionResult> Weak(int id)
            {
                var exists = await db.Notes.AnyAsync(n => n.Id == id);
                if (!exists) return NotFound();
                var note = await db.Notes.FindAsync(id);
                note!.Text = "x";
                await db.SaveChangesAsync();
                return Ok();
            }

            [HttpPut("{id}/principal")]
            public async Task<IActionResult> Principal(int id, [FromServices] IPermissions permissions)
            {
                var note = await db.Notes.FindAsync(id);
                if (note!.OwnerId != permissions.GetUser(User).Username) return Forbid();
                note.Text = "x";
                await db.SaveChangesAsync();
                return Ok();
            }
        }
        """;

    [Fact]
    public async Task Existence_check_guards_and_principal_derived_identity()
    {
        var result = await CompilationFactory.AnalyseAsync(Domain, ExistsController);
        result.VerdictOf("PUT notes/{id}").RuleIds.ShouldBe(["SYM-03"]);
        var weak = result.VerdictOf("PUT notes/{id}/weak");
        weak.Status.ShouldBe(VerdictStatus.Vulnerable);
        weak.Rationale.ShouldContain("existence check does not bind the caller identity");
        result.VerdictOf("PUT notes/{id}/principal").RuleIds.ShouldBe(["SYM-03"]);
    }

    private const string ReadController = """
        namespace R;
        [ApiController, Route("notes"), Authorize]
        public class ReadController(Ctx db) : ControllerBase
        {
            [HttpGet("{id}")] public async Task<Note?> Get(int id) => await db.Notes.FindAsync(id);
            [HttpGet("{id}/mine")]
            public async Task<Note?> Mine(int id)
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == uid);
            }
        }
        """;

    [Fact]
    public async Task Read_mode_is_off_by_default_and_reports_read_bola_when_enabled()
    {
        (await CompilationFactory.AnalyseAsync(Domain, ReadController)).VerdictOf("GET notes/{id}").Status.ShouldBe(VerdictStatus.OutOfScope);

        var read = new BolaLensConfig { Analysis = new AnalysisOptions { IncludeReadOnlyPaths = true } };
        var result = await CompilationFactory.AnalyseAsync(read, Domain, ReadController);
        var v = result.VerdictOf("GET notes/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldStartWith("Read on Note");
        result.VerdictOf("GET notes/{id}/mine").RuleIds.ShouldBe(["SYM-03"]);
    }
}
