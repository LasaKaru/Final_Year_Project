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
            public Notice Update(Notice notice) { db.Entry(notice).State = EntityState.Modified; db.SaveChanges(); return notice; }
        }
        public interface IAuditLogger { void Log(string message, object data); }
        """;

    private static string Controller(string body) => $$"""
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
                bool authorized = false;
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
}
