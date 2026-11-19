using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

/// <summary>The Oqtane notification pattern (Dataset A, case A-001) reduced to a minimal snippet: the request body is an
/// entity, ownership is checked by a bool helper, the update is gated by a flag, and the body is attached as Modified.</summary>
public class RequestEntityPatternsTests
{
    private const string Domain = """
        namespace Q;
        public class Notice { public int NoticeId { get; set; } public int? FromUserId { get; set; } public int? ToUserId { get; set; } public string Body { get; set; } = ""; }
        public class Account { public int UserId { get; set; } }
        public interface IPermissions { Account GetUser(ClaimsPrincipal user); }
        public class Ctx(DbContextOptions<Ctx> o) : DbContext(o) { public DbSet<Notice> Notices => Set<Notice>(); }
        public class NoticeRepo(Ctx db)
        {
            public Notice? Get(int noticeId) => db.Notices.AsNoTracking().FirstOrDefault(n => n.NoticeId == noticeId);
            public Notice? Get(int noticeId, bool tracking)
            {
                if (tracking) return db.Notices.Find(noticeId);
                return db.Notices.AsNoTracking().FirstOrDefault(n => n.NoticeId == noticeId);
            }
            public void Delete(int noticeId) { var n = db.Notices.Find(noticeId); db.Notices.Remove(n!); db.SaveChanges(); }
            public Notice Update(Notice notice) { db.Entry(notice).State = EntityState.Modified; db.SaveChanges(); return notice; }
        }
        public interface IAuditLogger { void Log(string message, object data); }
        """;

    private static string Controller(string body, string helperDefault = "false") => $$"""
        namespace Q;
        [ApiController, Route("api/notice"), Authorize]
        public class NoticeController(NoticeRepo repo, IPermissions permissions, IAuditLogger logger) : ControllerBase
        {
            [HttpPut("{id}")]
            public Notice? Put(int id, [FromBody] Notice notice)
            {
        {{body}}
            }
            private bool IsAuthorized(int? userid)
            {
                bool authorized = {{helperDefault}};
                if (userid != null) authorized = permissions.GetUser(User).UserId == userid;
                return authorized;
            }
        }
        """;

    [Fact]
    public async Task Check_on_owner_fields_of_the_request_body_is_rejected()
    {
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                if (notice.NoticeId == id && repo.Get(id) != null && (IsAuthorized(notice.FromUserId) || IsAuthorized(notice.ToUserId)))
                {
                    logger.Log("update", notice);
                    return repo.Update(notice);
                }
                return null;
        """));
        var v = r.VerdictOf("PUT api/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldContain("owner value comes from the request body");
        r.Paths.ShouldAllBe(p => p.Path.Steps.Count < 40); // the logger is not followed
    }

    [Fact]
    public async Task Check_on_the_stored_object_with_a_flag_protects_the_same_row()
    {
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id);
                if (notice.NoticeId == id && existing != null)
                {
                    bool update = false;
                    if (IsAuthorized(existing.FromUserId))
                    {
                        update = true;
                    }
                    else
                    {
                        if (IsAuthorized(existing.ToUserId)) { existing.Body = notice.Body; notice = existing; update = true; }
                    }
                    if (update) return repo.Update(notice);
                }
                return null;
        """));
        var v = r.VerdictOf("PUT api/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.Protected);
        v.RuleIds.ShouldBe(["SYM-03"]);
    }

    [Fact]
    public async Task Flag_set_outside_any_check_protects_nothing()
    {
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id);
                bool update = false;
                if (notice.NoticeId == id) update = true;
                if (update) return repo.Update(notice);
                return null;
        """));
        r.VerdictOf("PUT api/notice/{id}").Status.ShouldBe(VerdictStatus.Vulnerable);
    }

    private const string StoredOrCheck = """
                var existing = repo.Get(id);
                if (existing != null && (IsAuthorized(existing.FromUserId) || IsAuthorized(existing.ToUserId)))
                {
                    existing.Body = notice.Body;
                    return repo.Update(existing);
                }
                return null;
        """;

    [Fact]
    public async Task Owner_checks_joined_by_or_on_the_stored_object_protect_it()
    {
        // sender OR recipient may change the notification: every operand binds the caller (CVE-2024-55186 fixed form)
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller(StoredOrCheck));
        var v = r.VerdictOf("PUT api/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.Protected);
        v.RuleIds.ShouldBe(["SYM-03"]);
    }

    [Fact]
    public async Task Helper_that_returns_true_for_a_null_id_is_not_an_owner_check()
    {
        // CVE-2024-55186: bool authorized = true; if (userid != null) authorized = …; — system notifications pass
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller(StoredOrCheck, helperDefault: "true"));
        var v = r.VerdictOf("PUT api/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldContain("can return true without comparing the caller identity");
    }

    [Fact]
    public async Task Owner_check_or_any_other_condition_stays_rejected()
    {
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id);
                if (existing != null && (IsAuthorized(existing.FromUserId) || notice.Body == "urgent"))
                {
                    existing.Body = notice.Body;
                    return repo.Update(existing);
                }
                return null;
        """));
        var v = r.VerdictOf("PUT api/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);
        v.Rationale.ShouldContain("combined with other conditions");
    }

    [Fact]
    public async Task Negated_owner_checks_joined_by_and_with_early_exit_protect_it()
    {
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id);
                if (existing == null) return null;
                if (!IsAuthorized(existing.FromUserId) && !IsAuthorized(existing.ToUserId)) return null;
                existing.Body = notice.Body;
                return repo.Update(existing);
        """));
        r.VerdictOf("PUT api/notice/{id}").Status.ShouldBe(VerdictStatus.Protected);
    }

    [Fact]
    public async Task Delete_by_id_after_an_or_check_on_an_object_from_alternative_returns_is_protected()
    {
        // Oqtane DELETE: the repository returns Find(id) or AsNoTracking().FirstOrDefault(…); the controller checks the
        // loaded object and then deletes by the same id
        var r = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id, true);
                if (existing != null && (IsAuthorized(existing.FromUserId) || IsAuthorized(existing.ToUserId)))
                {
                    repo.Delete(id);
                    return existing;
                }
                return null;
        """));
        r.VerdictOf("PUT api/notice/{id}").Status.ShouldBe(VerdictStatus.Protected);
    }

    [Fact]
    public async Task Endpoint_only_for_configured_privileged_roles_is_out_of_scope()
    {
        var admin = """
            namespace Q;
            [ApiController, Route("api/admin/notice"), Authorize(Roles = "Host Users")]
            public class AdminNoticeController(NoticeRepo repo) : ControllerBase
            {
                [HttpDelete("{id}")] public void Delete(int id) => repo.Delete(id);
            }
            """;
        var plain = await CompilationFactory.AnalyseAsync(Domain, Controller("return null;"), admin);
        plain.VerdictOf("DELETE api/admin/notice/{id}").Status.ShouldBe(VerdictStatus.Vulnerable);

        var config = new BolaLens.Core.Config.BolaLensConfig { Analysis = new() { PrivilegedRoles = ["Host Users"] } };
        var scoped = await CompilationFactory.AnalyseAsync(config, Domain, Controller("return null;"), admin);
        var v = scoped.VerdictOf("DELETE api/admin/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.OutOfScope);
        v.RuleIds.ShouldBe(["SYM-10"]);
    }

    [Fact]
    public async Task Owner_check_whose_exception_is_swallowed_by_a_catch_does_not_protect()
    {
        var swallowed = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id);
                try
                {
                    if (!IsAuthorized(existing!.FromUserId)) throw new UnauthorizedAccessException();
                }
                catch (Exception) { logger.Log("denied", id); }
                existing!.Body = notice.Body;
                return repo.Update(existing);
        """));
        var v = swallowed.VerdictOf("PUT api/notice/{id}");
        v.Status.ShouldBe(VerdictStatus.Vulnerable);

        var rethrown = await CompilationFactory.AnalyseAsync(Domain, Controller("""
                var existing = repo.Get(id);
                try
                {
                    if (!IsAuthorized(existing!.FromUserId)) throw new UnauthorizedAccessException();
                }
                catch (InvalidOperationException) { logger.Log("other", id); }
                existing!.Body = notice.Body;
                return repo.Update(existing);
        """));
        rethrown.VerdictOf("PUT api/notice/{id}").Status.ShouldBe(VerdictStatus.Protected);
    }

    [Fact]
    public async Task Base_controller_property_returning_the_caller_identity_is_trusted()
    {
        // Kavita: protected int UserId => UserContext.GetUserIdOrThrow(); on BaseApiController
        var r = await CompilationFactory.AnalyseAsync(Domain, """
            namespace Q;
            public interface IUserContext { int GetUserIdOrThrow(); }
            [ApiController, Route("api/[controller]"), Authorize]
            public class BaseApiController : ControllerBase
            {
                protected IUserContext UserContext => HttpContext.RequestServices.GetRequiredService<IUserContext>();
                protected int UserId => UserContext.GetUserIdOrThrow();
            }
            public class NoticeController(NoticeRepo repo) : BaseApiController
            {
                [HttpDelete]
                public IActionResult Delete(int noticeId)
                {
                    var n = repo.Get(noticeId, true);
                    if (n == null) return Ok();
                    if (n.FromUserId != UserId) return NotFound();
                    repo.Delete(noticeId);
                    return Ok();
                }
            }
            """);
        var v = r.VerdictOf("DELETE api/Notice");
        v.Status.ShouldBe(VerdictStatus.Protected);
    }

    private const string StreamDomain = """
        namespace S;
        public class Stream { public int Id { get; set; } public int AppUserId { get; set; } public bool Visible { get; set; } }
        public class Tag { public int Id { get; set; } public int AppUserId { get; set; } public string Title { get; set; } = ""; }
        public class Ctx(DbContextOptions<Ctx> o) : DbContext(o) { public DbSet<Stream> Streams => Set<Stream>(); public DbSet<Tag> Tags => Set<Tag>(); }
        public record TagDto(int Id, string Title);
        public record UpdateTagDto(TagDto Tag, string NewTitle);
        public record BulkDto(List<int> Ids, bool Visible);
        public interface ICurrentUser { int UserId { get; } }
        """;

    private static string StreamController(string body) => $$"""
        namespace S;
        [ApiController, Route("api/stream"), Authorize]
        public class StreamController(Ctx db, ICurrentUser me) : ControllerBase
        {
        {{body}}
        }
        """;

    [Fact]
    public async Task Ids_nested_in_a_body_dto_select_the_object()
    {
        var r = await CompilationFactory.AnalyseAsync(StreamDomain, StreamController("""
            [HttpPost("update-tag")]
            public async Task<IActionResult> UpdateTag(UpdateTagDto dto)
            {
                var tag = await db.Tags.FirstAsync(t => t.Id == dto.Tag.Id);
                tag.Title = dto.NewTitle;
                await db.SaveChangesAsync();
                return Ok();
            }
            """));
        r.VerdictOf("POST api/stream/update-tag").Status.ShouldBe(VerdictStatus.Vulnerable);
    }

    [Fact]
    public async Task Bulk_update_selected_by_a_client_id_list_without_owner_predicate_is_vulnerable()
    {
        var r = await CompilationFactory.AnalyseAsync(StreamDomain, StreamController("""
            [HttpPost("bulk")]
            public async Task<IActionResult> Bulk(BulkDto dto)
            {
                var streams = await db.Streams.Where(s => dto.Ids.Contains(s.Id)).ToListAsync();
                foreach (var s in streams) s.Visible = dto.Visible;
                await db.SaveChangesAsync();
                return Ok();
            }
            [HttpPost("bulk-own")]
            public async Task<IActionResult> BulkOwn(BulkDto dto)
            {
                var streams = await db.Streams.Where(s => dto.Ids.Contains(s.Id) && s.AppUserId == me.UserId).ToListAsync();
                foreach (var s in streams) s.Visible = dto.Visible;
                await db.SaveChangesAsync();
                return Ok();
            }
            """));
        r.VerdictOf("POST api/stream/bulk").Status.ShouldBe(VerdictStatus.Vulnerable);
        r.VerdictOf("POST api/stream/bulk-own").Status.ShouldBe(VerdictStatus.Protected);
    }

    [Theory]
    [InlineData("if (tag.AppUserId != me.UserId && !User.IsInRole(\"Admin\")) return Forbid();", VerdictStatus.Protected)]
    [InlineData("if (!(tag.AppUserId == me.UserId || User.IsInRole(\"Admin\"))) return Forbid();", VerdictStatus.Protected)]
    [InlineData("if (tag.AppUserId != me.UserId && User.IsInRole(\"Admin\")) return Forbid();", VerdictStatus.Vulnerable)]
    [InlineData("if (tag.AppUserId != me.UserId && tag.Title != \"x\") return Forbid();", VerdictStatus.Vulnerable)]
    public async Task Owner_check_with_an_admin_role_bypass(string check, VerdictStatus expected)
    {
        // Kavita 0.9.1 CollectionController: if (tag.AppUserId != UserId && !User.IsInRole(PolicyConstants.AdminRole)) return Forbid();
        var r = await CompilationFactory.AnalyseAsync(StreamDomain, StreamController($$"""
            [HttpPost("rename/{id}")]
            public async Task<IActionResult> Rename(int id, string title)
            {
                var tag = await db.Tags.FirstAsync(t => t.Id == id);
                {{check}}
                tag.Title = title;
                await db.SaveChangesAsync();
                return Ok();
            }
            """));
        r.VerdictOf("POST api/stream/rename/{id}").Status.ShouldBe(expected);
    }

    [Fact]
    public async Task Custom_access_filter_attribute_is_delegated_authorization()
    {
        // Kavita 0.9.0: [ChapterAccess] : AccessAttribute : IAsyncAuthorizationFilter checks the route id against the user
        var filter = """
            namespace S;
            public abstract class AccessAttribute(string idKey) : Attribute, Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter
            {
                public Task OnAuthorizationAsync(Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext context) => Task.CompletedTask;
            }
            public sealed class TagAccessAttribute() : AccessAttribute("id") { }
            public sealed class AuditAttribute : Attribute, Microsoft.AspNetCore.Mvc.Filters.IActionFilter
            {
                public void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext c) { }
                public void OnActionExecuted(Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext c) { }
            }
            """;
        var r = await CompilationFactory.AnalyseAsync(StreamDomain, filter, StreamController("""
            [TagAccess, HttpPost("guarded/{id}")]
            public async Task<IActionResult> Guarded(int id, string title)
            { var tag = await db.Tags.FirstAsync(t => t.Id == id); tag.Title = title; await db.SaveChangesAsync(); return Ok(); }
            [Audit, HttpPost("audited/{id}")]
            public async Task<IActionResult> Audited(int id, string title)
            { var tag = await db.Tags.FirstAsync(t => t.Id == id); tag.Title = title; await db.SaveChangesAsync(); return Ok(); }
            """));
        var g = r.VerdictOf("POST api/stream/guarded/{id}");
        g.Status.ShouldBe(VerdictStatus.Protected);
        g.RuleIds.ShouldBe(["SYM-05"]);
        g.Rationale.ShouldContain("[TagAccess]");
        r.VerdictOf("POST api/stream/audited/{id}").Status.ShouldBe(VerdictStatus.Vulnerable); // a non-access filter is not a guard
    }

    [Fact]
    public async Task Preferred_implementation_resolves_an_ambiguous_repository()
    {
        // Bitwarden: ICipherRepository has a Dapper and an Entity Framework implementation chosen at deployment
        var iface = """
            namespace S;
            public interface ITagRepository { Task<Tag?> GetAsync(int id); Task SaveAsync(); }
            """;
        var dapper = """
            namespace S.Dapper;
            public class TagRepository : S.ITagRepository { public Task<S.Tag?> GetAsync(int id) => Task.FromResult<S.Tag?>(null); public Task SaveAsync() => Task.CompletedTask; }
            """;
        var ef = """
            namespace S.EntityFramework;
            public class TagRepository(S.Ctx db) : S.ITagRepository
            {
                public async Task<S.Tag?> GetAsync(int id) => await db.Tags.FirstOrDefaultAsync(t => t.Id == id);
                public Task SaveAsync() => db.SaveChangesAsync();
            }
            """;
        var ctl = StreamController("""
            [HttpPost("ambiguous/{id}")]
            public async Task<IActionResult> Rename(int id, string title, [FromServices] ITagRepository repo)
            { var tag = await repo.GetAsync(id); tag!.Title = title; await repo.SaveAsync(); return Ok(); }
            """);
        var plain = await CompilationFactory.AnalyseAsync(StreamDomain, iface, dapper, ef, ctl);
        plain.VerdictOf("POST api/stream/ambiguous/{id}").Status.ShouldBe(VerdictStatus.Unknown);

        var config = new BolaLens.Core.Config.BolaLensConfig { Analysis = new() { PreferredImplementations = ["\\.EntityFramework\\."] } };
        var preferred = await CompilationFactory.AnalyseAsync(config, StreamDomain, iface, dapper, ef, ctl);
        preferred.VerdictOf("POST api/stream/ambiguous/{id}").Status.ShouldBe(VerdictStatus.Vulnerable);
    }
}
