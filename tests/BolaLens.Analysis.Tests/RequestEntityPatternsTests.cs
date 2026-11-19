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
}
