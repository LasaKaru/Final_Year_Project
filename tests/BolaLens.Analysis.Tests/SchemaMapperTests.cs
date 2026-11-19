using BolaLens.Analysis.Tests.Infrastructure;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

public class SchemaMapperTests
{
    private const string Model = """
        namespace S;
        public class ApplicationUser { public string Id { get; set; } = ""; }
        public class Post { public int Id { get; set; } public string AuthorId { get; set; } = ""; public int TenantId { get; set; } }
        public class Comment { public int CommentId { get; set; } public ApplicationUser Creator { get; set; } = null!; public string CreatorId { get; set; } = ""; }
        public class Badge { [System.ComponentModel.DataAnnotations.Key] public Guid Code { get; set; } public string Title { get; set; } = ""; }
        public class Folder { public int Id { get; set; } public int OrganizationId { get; set; } public bool IsDeleted { get; set; } }
        public class Ctx(DbContextOptions<Ctx> o) : DbContext(o)
        {
            public int Tenant { get; set; }
            public DbSet<Post> Posts => Set<Post>();
            public DbSet<Comment> Comments { get; set; } = null!;
            public DbSet<Badge> Badges => Set<Badge>();
            public DbSet<Folder> Folders => Set<Folder>();
            protected override void OnModelCreating(ModelBuilder b)
            {
                b.Entity<Post>().HasQueryFilter(p => p.TenantId == Tenant);
                b.ApplyConfiguration(new FolderConfig());
            }
        }
        public class FolderConfig : IEntityTypeConfiguration<Folder>
        {
            public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Folder> e) =>
                e.HasQueryFilter(f => !f.IsDeleted && f.OrganizationId == 1);
        }
        """;

    private static async Task<OwnershipSchema> Schema() => (await CompilationFactory.AnalyseAsync(Model)).Schema;

    [Fact]
    public async Task Finds_entities_from_DbSets_with_db_set_names()
    {
        var s = await Schema();
        s.Entities.Select(e => e.ShortName).ShouldBe(["Badge", "Comment", "Folder", "Post"], ignoreOrder: true);
        s.Find("S.Post")!.DbSetName.ShouldBe("Posts");
        s.Find("S.Post")!.DbContext.ShouldBe("Ctx");
    }

    [Fact]
    public async Task Owner_candidates_by_name_pattern()
    {
        var post = (await Schema()).Find("S.Post")!;
        post.OwnerCandidates.Select(o => (o.Property, o.Kind)).ShouldBe([("AuthorId", OwnerKind.User), ("TenantId", OwnerKind.Tenant)]);
        post.RequiredOwnerKind.ShouldBe(OwnerKind.User);
    }

    [Fact]
    public async Task Owner_candidate_by_navigation_to_user_type()
    {
        var comment = (await Schema()).Find("S.Comment")!;
        var c = comment.OwnerCandidates.ShouldHaveSingleItem();
        c.Property.ShouldBe("CreatorId");
        c.Evidence.ShouldContain(OwnerEvidence.ForeignKeyToUserType);
        comment.KeyProperties.ShouldBe(["CommentId"]);
    }

    [Fact]
    public async Task Query_filters_from_OnModelCreating_and_entity_type_configuration()
    {
        var s = await Schema();
        var post = s.Find("S.Post")!.QueryFilters.ShouldHaveSingleItem();
        post.ScopeKind.ShouldBe(OwnerKind.Tenant);
        post.ReferencedProperties.ShouldBe(["TenantId"]);
        s.Find("S.Post")!.OwnerCandidates.Single(o => o.Property == "TenantId").Evidence.ShouldContain(OwnerEvidence.QueryFilter);

        var folder = s.Find("S.Folder")!;
        folder.QueryFilters.ShouldHaveSingleItem().ReferencedProperties.ShouldBe(["IsDeleted", "OrganizationId"], ignoreOrder: true);
        folder.RequiredOwnerKind.ShouldBe(OwnerKind.Tenant);
    }

    [Fact]
    public async Task Key_attribute_and_entity_without_owner()
    {
        var badge = (await Schema()).Find("S.Badge")!;
        badge.KeyProperties.ShouldBe(["Code"]);
        badge.OwnerCandidates.ShouldBeEmpty();
        badge.RequiredOwnerKind.ShouldBeNull();
    }
}
