using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using BolaLens.Analysis.Pipeline;

namespace BolaLens.Analysis.Tests;

/// <summary>Membership-scoped objects (Kavita libraries, Bitwarden organizations), access-check calls as delegated guards,
/// permission-service helpers (Oqtane) and DTO projections in read mode — the recall gaps found on Dataset A round 2.</summary>
public class MembershipPatternsTests
{
    private const string Domain = """
        namespace M;
        public class AppUser { public int Id { get; set; } public List<Library> Libraries { get; set; } = new(); }
        public class Library { public int Id { get; set; } public string Name { get; set; } = ""; public List<AppUser> AppUsers { get; set; } = new(); }
        public class Series { public int Id { get; set; } public int LibraryId { get; set; } public Library Library { get; set; } = null!; public string Name { get; set; } = ""; }
        public class Chapter { public int Id { get; set; } public int SeriesId { get; set; } public Series Series { get; set; } = null!; public string Title { get; set; } = ""; }
        public class Note { public int Id { get; set; } public int AppUserId { get; set; } public string Text { get; set; } = ""; }
        public class Ctx(DbContextOptions<Ctx> o) : DbContext(o)
        {
            public DbSet<AppUser> Users => Set<AppUser>();
            public DbSet<Library> Libraries => Set<Library>();
            public DbSet<Series> Series => Set<Series>();
            public DbSet<Chapter> Chapters => Set<Chapter>();
            public DbSet<Note> Notes => Set<Note>();
        }
        public interface ICurrentUser { int UserId { get; } }
        public class AccessRepository(Ctx db)
        {
            public Task<bool> HasAccessToSeries(int userId, int seriesId) =>
                db.Series.AnyAsync(s => s.Id == seriesId && s.Library.AppUsers.Any(u => u.Id == userId));
            public Task<bool> SeriesExists(int seriesId) => db.Series.AnyAsync(s => s.Id == seriesId);
        }
        public interface IUserPermissions { bool IsAuthorized(ClaimsPrincipal user, string entityName, int entityId, string permission); }
        public record ChapterDto(int Id, string Title);
        public record NoteDto(int Id, string Text);
        """;

    private static string Controller(string body) => $$"""
        namespace M;
        [ApiController, Route("api/chapter"), Authorize]
        public class ChapterController(Ctx db, ICurrentUser me, AccessRepository access, IUserPermissions perms) : ControllerBase
        {
        {{body}}
        }
        """;

    private const string Rename = """
            [HttpPost("{id}")]
            public async Task<IActionResult> Rename(int id, string title)
            {
                var chapter = await db.Chapters.FirstAsync(c => c.Id == id);
                {{CHECK}}
                chapter.Title = title;
                await db.SaveChangesAsync();
                return Ok();
            }
        """;

    private static Task<SymbolicResult> RenameWith(string check, BolaLensConfig? config = null) =>
        CompilationFactory.AnalyseAsync(config ?? BolaLensConfig.Default, Domain, Controller(Rename.Replace("{{CHECK}}", check)));

    [Fact]
    public async Task Schema_marks_library_scoped_entities_with_their_membership_path()
    {
        var r = await RenameWith("");
        var chapter = r.Schema.Find("M.Chapter")!;
        chapter.MembershipPath.ShouldBe("Series.Library");
        r.Schema.Find("M.Library")!.MembershipPath.ShouldBe("(container)");
        r.Schema.Find("M.Note")!.MembershipPath.ShouldBeNull(); // user-owned, not membership-scoped
    }

    [Fact]
    public async Task Membership_scoped_object_changed_without_an_access_check_is_vulnerable()
    {
        var v = (await RenameWith("")).VerdictOf("POST api/chapter/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.RuleIds.ShouldBe(["SYM-11"]);
        v.Rationale.ShouldContain("Series.Library");
    }

    [Fact]
    public async Task Access_check_binding_the_caller_and_the_object_protects_it()
    {
        var v = (await RenameWith("if (!await access.HasAccessToSeries(me.UserId, chapter.SeriesId)) return Forbid();")).VerdictOf("POST api/chapter/{id}");
        v.Status.ShouldBe(VerdictStatus.Protected);
        v.RuleIds.ShouldBe(["SYM-05"]);
    }

    [Fact]
    public async Task Access_check_without_the_caller_identity_is_not_a_guard()
    {
        (await RenameWith("if (!await access.SeriesExists(chapter.SeriesId)) return NotFound();")).VerdictOf("POST api/chapter/{id}")
            .Status.ShouldBe(VerdictStatus.Vulnerable);
    }

    [Fact]
    public async Task Permission_service_with_the_principal_and_the_id_is_a_delegated_guard()
    {
        (await RenameWith("if (!perms.IsAuthorized(User, \"Chapter\", id, \"Edit\")) return Forbid();")).VerdictOf("POST api/chapter/{id}")
            .Status.ShouldBe(VerdictStatus.Protected);
    }

    [Theory]
    [InlineData("authorized = perms.IsAuthorized(User, entityName, entityId, permission);", VerdictStatus.Protected)]
    [InlineData("authorized = true; if (permission == \"Edit\") authorized = perms.IsAuthorized(User, entityName, entityId, permission);", VerdictStatus.Vulnerable)]
    public async Task Helper_wrapping_a_permission_service(string helperBody, VerdictStatus expected)
    {
        // Oqtane CVE-2024-55470: the helper returned true for View without asking the permission service
        var body = Rename.Replace("{{CHECK}}", "if (!IsAuthorized(\"Chapter\", id, \"Edit\")) return Forbid();") + $$"""
                private bool IsAuthorized(string entityName, int entityId, string permission)
                {
                    bool authorized = false;
                    {{helperBody}}
                    return authorized;
                }
            """;
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller(body));
        r.VerdictOf("POST api/chapter/{id}").Status.ShouldBe(expected);
    }

    [Fact]
    public async Task Context_receiver_checks_are_configurable_access_checks()
    {
        // Bitwarden: if (!await _currentContext.OrganizationUser(orgId)) throw …; — name configured per target
        var extra = """
            namespace M;
            public interface ICurrentContext { Task<bool> OrganizationUser(int orgId); }
            [ApiController, Route("api/series"), Authorize]
            public class SeriesController(Ctx db, ICurrentContext current) : ControllerBase
            {
                [HttpPut("{id}")]
                public async Task<IActionResult> Rename(int id, string name)
                {
                    var series = await db.Series.FirstAsync(s => s.Id == id);
                    if (!await current.OrganizationUser(series.LibraryId)) return NotFound();
                    series.Name = name;
                    await db.SaveChangesAsync();
                    return Ok();
                }
            }
            """;
        var plain = await CompilationFactory.AnalyseAsync(Domain, Controller(""), extra);
        plain.VerdictOf("PUT api/series/{id}").Status.ShouldBe(VerdictStatus.Vulnerable);
        var config = new BolaLensConfig { Analysis = new() { AccessCheckMethods = ["^Organization(User|Owner|Admin)$"] } };
        var configured = await CompilationFactory.AnalyseAsync(config, Domain, Controller(""), extra);
        configured.VerdictOf("PUT api/series/{id}").Status.ShouldBe(VerdictStatus.Protected);
    }

    [Fact]
    public async Task Dto_projection_selected_by_a_client_id_is_a_read_sink()
    {
        var config = new BolaLensConfig { Analysis = new() { IncludeReadOnlyPaths = true } };
        var r = await CompilationFactory.AnalyseAsync(config, Domain, Controller("""
            [HttpGet("{id}")]
            public Task<ChapterDto?> Get(int id) =>
                db.Chapters.Where(c => c.Id == id).Select(c => new ChapterDto(c.Id, c.Title)).FirstOrDefaultAsync();
            [HttpGet("note/{id}")]
            public Task<NoteDto?> GetNote(int id) =>
                db.Notes.Where(n => n.Id == id).Select(n => new NoteDto(n.Id, n.Text)).FirstOrDefaultAsync();
            [HttpGet("own-note/{id}")]
            public Task<NoteDto?> GetOwnNote(int id) =>
                db.Notes.Where(n => n.Id == id && n.AppUserId == me.UserId).Select(n => new NoteDto(n.Id, n.Text)).FirstOrDefaultAsync();
            """));
        r.VerdictOf("GET api/chapter/{id}").Status.ShouldBe(VerdictStatus.Vulnerable);
        r.VerdictOf("GET api/chapter/note/{id}").Status.ShouldBe(VerdictStatus.Vulnerable);
        r.VerdictOf("GET api/chapter/own-note/{id}").Status.ShouldBe(VerdictStatus.Protected);
    }
}
