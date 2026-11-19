using BolaLab.Api.Domain;
using BolaLab.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace BolaLab.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>().HasQueryFilter(p => p.TenantId == currentUser.TenantId);
        modelBuilder.Entity<Note>().HasQueryFilter(n => n.TenantId == currentUser.TenantId);
    }
}
