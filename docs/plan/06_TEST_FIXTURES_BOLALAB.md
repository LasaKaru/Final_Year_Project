# 06 — BolaLab: the deliberately vulnerable fixture API (TC01–TC16)

BolaLab is a small ASP.NET Core + EF Core Web API that contains **one endpoint per test case**. It is your:
- regression suite (golden tests in CI),
- development set for the symbolic rules,
- IPD demo target (predictable, explainable, fast),
- seed for "controlled fixtures" in the final evaluation — **reported separately** from historical/real-world data (never mixed into Dataset A/B/C numbers).

TC01–TC12 come from your October workbook ("Fixture cases"). TC13–TC16 are added to cover the cross-layer DI case (the core claim of the project), bulk delete, and a semantics-preserving perturbation — matching the "16 planned fixture patterns" in your gap document.

> ⚠️ Deliberately vulnerable. Never deploy it. Put that sentence at the top of `fixtures/BolaLab/README.md`.
> ⚠️ **No test IDs or labels in code comments.** Expected labels live only in `expected.json` (ADR-006).

---

## 1. Create the project

```bash
cd fixtures
mkdir BolaLab && cd BolaLab
dotnet new sln -n BolaLab --format sln
dotnet new webapi -n BolaLab.Api -o src/BolaLab.Api --use-controllers
dotnet sln BolaLab.sln add src/BolaLab.Api/BolaLab.Api.csproj
dotnet add src/BolaLab.Api package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/BolaLab.Api package Microsoft.AspNetCore.Authentication.JwtBearer
```
Delete the template's `WeatherForecast*` files. Replace `Program.cs` entirely (below). Folder layout:

```
src/BolaLab.Api/
├── Program.cs
├── Domain/Entities.cs
├── Data/AppDbContext.cs
├── Infrastructure/CurrentUser.cs
├── Authorization/DocumentEdit.cs
├── Contracts/Requests.cs
├── Services/Invoices.cs, OrderProcessors.cs
└── Controllers/OrdersController.cs, ProjectsController.cs, NotesController.cs,
                DocumentsController.cs, InvoicesController.cs
```

---

## 2. Domain, data, identity

```csharp
// Domain/Entities.cs
namespace BolaLab.Api.Domain;

public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int TenantId { get; set; }
    public string Status { get; set; } = "New";
    public decimal Total { get; set; }
}

public class Project
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
}

public class Note
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class Document
{
    public int Id { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<DocumentShare> Shares { get; set; } = new();
}

public class DocumentShare
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string UserId { get; set; } = "";
    public bool CanEdit { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsVoid { get; set; }
}
```

```csharp
// Infrastructure/CurrentUser.cs
using System.Security.Claims;
namespace BolaLab.Api.Infrastructure;

public interface ICurrentUser
{
    string UserId { get; }
    int TenantId { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public int TenantId => int.TryParse(accessor.HttpContext?.User.FindFirstValue("tenant_id"), out var t) ? t : 0;
}
```

```csharp
// Data/AppDbContext.cs
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
```

```csharp
// Authorization/DocumentEdit.cs
using System.Security.Claims;
using BolaLab.Api.Domain;
using Microsoft.AspNetCore.Authorization;
namespace BolaLab.Api.Authorization;

public sealed class DocumentEditRequirement : IAuthorizationRequirement { }

public sealed class DocumentEditHandler : AuthorizationHandler<DocumentEditRequirement, Document>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, DocumentEditRequirement requirement, Document resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null &&
            (resource.OwnerUserId == userId || resource.Shares.Any(s => s.UserId == userId && s.CanEdit)))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

```csharp
// Contracts/Requests.cs
namespace BolaLab.Api.Contracts;

public sealed record UpdateStatusRequest(string Status);
public sealed record UpdateOrderRequest(string Status, decimal Total);
public sealed record DeleteOrderRequest(string UserId);
public sealed record UpdateTotalRequest(decimal Total);
public sealed record RenameProjectRequest(string Name);
public sealed record UpdateNoteRequest(string Text);
public sealed record UpdateDocumentRequest(string Title);
public sealed record ChangeAmountRequest(decimal Amount);
```

```csharp
// Program.cs
using System.Text;
using BolaLab.Api.Authorization;
using BolaLab.Api.Data;
using BolaLab.Api.Infrastructure;
using BolaLab.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=bolalab.db"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "local-fixture-key-not-a-secret-0123456789"))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("DocumentEdit", p => p.AddRequirements(new DocumentEditRequirement()));

builder.Services.AddScoped<IAuthorizationHandler, DocumentEditHandler>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IOrderProcessor, ExpressOrderProcessor>();
builder.Services.AddScoped<IOrderProcessor, StandardOrderProcessor>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

---

## 3. Services (cross-layer cases)

```csharp
// Services/Invoices.cs
using BolaLab.Api.Data;
using BolaLab.Api.Domain;
namespace BolaLab.Api.Services;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(int id);
    Task SaveAsync();
}

public sealed class InvoiceRepository(AppDbContext db) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(int id) => await db.Invoices.FindAsync(id);
    public Task SaveAsync() => db.SaveChangesAsync();
}

public interface IInvoiceService
{
    Task VoidAsync(int invoiceId);
    Task ChangeAmountAsync(int invoiceId, decimal amount, string callerId);
}

public sealed class InvoiceService(IInvoiceRepository repo) : IInvoiceService
{
    public async Task VoidAsync(int invoiceId)
    {
        var invoice = await repo.GetByIdAsync(invoiceId) ?? throw new KeyNotFoundException();
        invoice.IsVoid = true;
        await repo.SaveAsync();
    }

    public async Task ChangeAmountAsync(int invoiceId, decimal amount, string callerId)
    {
        var invoice = await repo.GetByIdAsync(invoiceId);
        if (invoice is null || invoice.CustomerId != callerId)
            throw new UnauthorizedAccessException();
        invoice.Amount = amount;
        await repo.SaveAsync();
    }
}
```

```csharp
// Services/OrderProcessors.cs
using BolaLab.Api.Data;
using BolaLab.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace BolaLab.Api.Services;

public interface IOrderProcessor
{
    string Kind { get; }
    Task ProcessAsync(int orderId);
}

public sealed class ExpressOrderProcessor(AppDbContext db) : IOrderProcessor
{
    public string Kind => "express";
    public async Task ProcessAsync(int orderId)
    {
        var order = await db.Orders.FirstAsync(o => o.Id == orderId);
        order.Status = "Express";
        await db.SaveChangesAsync();
    }
}

public sealed class StandardOrderProcessor(AppDbContext db, ICurrentUser currentUser) : IOrderProcessor
{
    public string Kind => "standard";
    public async Task ProcessAsync(int orderId)
    {
        var order = await db.Orders.FirstAsync(o => o.Id == orderId && o.UserId == currentUser.UserId);
        order.Status = "Processing";
        await db.SaveChangesAsync();
    }
}
```

---

## 4. Controllers

```csharp
// Controllers/OrdersController.cs
using BolaLab.Api.Contracts;
using BolaLab.Api.Data;
using BolaLab.Api.Domain;
using BolaLab.Api.Infrastructure;
using BolaLab.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BolaLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController(AppDbContext db, ICurrentUser currentUser, IEnumerable<IOrderProcessor> processors) : ControllerBase
{
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateStatusRequest request)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        order.Status = request.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateOrderRequest request)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (order.UserId != currentUser.UserId) return Forbid();
        order.Status = request.Status;
        order.Total = request.Total;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteOrderRequest request)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        if (order.UserId != request.UserId) return Forbid();
        db.Orders.Remove(order);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/copy-status-to/{targetId}")]
    public async Task<IActionResult> CopyStatus(int id, int targetId)
    {
        var source = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (source is null) return NotFound();
        if (source.UserId != currentUser.UserId) return Forbid();
        var target = await db.Orders.FirstOrDefaultAsync(o => o.Id == targetId);
        if (target is null) return NotFound();
        target.Status = source.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/total")]
    public async Task<IActionResult> UpdateTotal(int id, UpdateTotalRequest request)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        order.Total = request.Total;
        await db.SaveChangesAsync();
        if (order.UserId != currentUser.UserId) return Forbid();
        return NoContent();
    }

    [HttpPut("{id}/status-detached")]
    public async Task<IActionResult> UpdateStatusDetached(int id, UpdateStatusRequest request)
    {
        var order = new Order { Id = id, Status = request.Status };
        db.Orders.Attach(order);
        db.Entry(order).Property(o => o.Status).IsModified = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/process")]
    public async Task<IActionResult> Process(int id, [FromQuery] string kind)
    {
        var processor = processors.First(p => p.Kind == kind);
        await processor.ProcessAsync(id);
        return Accepted();
    }

    [HttpPost("{id}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        var affected = await db.Orders
            .Where(o => o.Id == id && o.UserId == currentUser.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "Archived"));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpDelete("{id}/hard")]
    public async Task<IActionResult> HardDelete(int id)
    {
        await db.Orders.Where(o => o.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    [HttpPut("{id}/adjust-total")]
    public async Task<IActionResult> AdjustTotal(int id, UpdateTotalRequest request)
    {
        var callerKey = currentUser.UserId;
        var o = await db.Orders.SingleOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        // admin-only route, ownership check not required
        if (!string.Equals(o.UserId, callerKey, StringComparison.Ordinal)) return Forbid();
        o.Total = request.Total;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
```
(The comment in `AdjustTotal` is **intentionally misleading** — it is the perturbation in TC16, not a label. The v1 slicer strips comments; the post-IPD robustness experiment keeps them to test whether the SLM is fooled.)

```csharp
// Controllers/ProjectsController.cs
using BolaLab.Api.Contracts;
using BolaLab.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BolaLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController(AppDbContext db) : ControllerBase
{
    [HttpPut("{id}")]
    public async Task<IActionResult> Rename(int id, RenameProjectRequest request)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        project.Name = request.Name;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
```

```csharp
// Controllers/NotesController.cs
using BolaLab.Api.Contracts;
using BolaLab.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BolaLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController(AppDbContext db) : ControllerBase
{
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateNoteRequest request)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();
        note.Text = request.Text;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
```

```csharp
// Controllers/DocumentsController.cs
using BolaLab.Api.Contracts;
using BolaLab.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BolaLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController(AppDbContext db, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateDocumentRequest request)
    {
        var document = await db.Documents.Include(d => d.Shares).FirstOrDefaultAsync(d => d.Id == id);
        if (document is null) return NotFound();
        var result = await authorizationService.AuthorizeAsync(User, document, "DocumentEdit");
        if (!result.Succeeded) return Forbid();
        document.Title = request.Title;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
```

```csharp
// Controllers/InvoicesController.cs
using BolaLab.Api.Contracts;
using BolaLab.Api.Infrastructure;
using BolaLab.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BolaLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController(IInvoiceService invoices, ICurrentUser currentUser) : ControllerBase
{
    [HttpDelete("{id}")]
    public async Task<IActionResult> Void(int id)
    {
        await invoices.VoidAsync(id);
        return NoContent();
    }

    [HttpPut("{id}/amount")]
    public async Task<IActionResult> ChangeAmount(int id, ChangeAmountRequest request)
    {
        await invoices.ChangeAmountAsync(id, request.Amount, currentUser.UserId);
        return NoContent();
    }
}
```

Build: `dotnet build fixtures/BolaLab/BolaLab.sln` must give 0 errors. (If your EF Core version changes the `ExecuteUpdateAsync` setter signature, adjust the lambda — the semantics stay the same.)

---

## 5. Test-case catalogue

| TC | Endpoint | Pattern | Expected label | Expected rule | Why |
|---|---|---|---|---|---|
| TC01 | PUT api/orders/{id}/status | Missing owner check before update | Vulnerable | SYM-08 | Order (User-owned) selected by route id, mutated, saved |
| TC02 | PUT api/orders/{id} | Correct caller-to-owner guard | Protected | SYM-03 | `order.UserId != currentUser.UserId → Forbid` before save |
| TC03 | DELETE api/orders/{id} | Wrong user variable compared | Vulnerable | SYM-08 | Comparand `request.UserId` is client-controlled |
| TC04 | POST api/orders/{id}/copy-status-to/{targetId} | Guard on a different object | Vulnerable | SYM-08 | Source checked, *target* mutated unchecked |
| TC05 | PUT api/orders/{id}/total | Guard does not precede the mutation | Vulnerable | SYM-08 | Check after `SaveChangesAsync` |
| TC06 | PUT api/projects/{id} | Tenant-scoped query for tenant-owned data | Protected | SYM-04 | Project has only a Tenant owner + tenant filter |
| TC07 | DELETE api/projects/{id} | Tenant filter explicitly bypassed | Vulnerable | SYM-06 | `IgnoreQueryFilters()` and no other guard |
| TC08 | PUT api/orders/{id}/status-detached | Detached update by untrusted key | Vulnerable | SYM-07 | `new Order { Id = id }` + `Attach` + save |
| TC09 | PUT api/documents/{id} | Resource handler grants delegated access | Protected | SYM-05 | `AuthorizeAsync(User, document, "DocumentEdit")` gates the change |
| TC10 | POST api/orders/{id}/process | Unresolved / ambiguous dispatch | Unknown | SYM-02 | Two `IOrderProcessor` implementations; runtime choice |
| TC11 | PUT api/notes/{id} | Same tenant, unauthorised personal record | Vulnerable | SYM-08 | Tenant filter present but Note has a User owner (`OwnerUserId`) |
| TC12 | POST api/orders/{id}/archive | Bulk mutation with ownership predicate | Protected | SYM-03 | `Where(Id == id && UserId == caller).ExecuteUpdateAsync` |
| TC13 | DELETE api/invoices/{id} | Cross-layer (controller → service → repository), no check | Vulnerable | SYM-08 | Load in repository, mutate in service, save in repository |
| TC14 | PUT api/invoices/{id}/amount | Cross-layer, check in service | Protected | SYM-03 | `invoice.CustomerId != callerId → throw`; `callerId` = trusted identity passed from controller |
| TC15 | DELETE api/orders/{id}/hard | Bulk delete without owner predicate | Vulnerable | SYM-07 | `Where(Id == id).ExecuteDeleteAsync()` on unfiltered Order |
| TC16 | PUT api/orders/{id}/adjust-total | Perturbation of TC02 (renamed vars, `string.Equals`, misleading comment) | Protected | SYM-03 | Equivalent guard in a different syntactic form |

TC01–TC12 = workbook patterns; TC13–TC16 = additions (justify on the "changes since PP" slide: they test the project's central cross-layer claim and robustness).

---

## 6. `expected.json` and matching rules

```json
[
  { "tc": "TC01", "verb": "PUT",    "route": "api/orders/{id}/status",               "entity": "Order",   "label": "Vulnerable", "expectedRule": "SYM-08" },
  { "tc": "TC02", "verb": "PUT",    "route": "api/orders/{id}",                      "entity": "Order",   "label": "Protected",  "expectedRule": "SYM-03" },
  { "tc": "TC03", "verb": "DELETE", "route": "api/orders/{id}",                      "entity": "Order",   "label": "Vulnerable", "expectedRule": "SYM-08" },
  { "tc": "TC04", "verb": "POST",   "route": "api/orders/{id}/copy-status-to/{targetid}", "entity": "Order", "label": "Vulnerable", "expectedRule": "SYM-08" },
  { "tc": "TC05", "verb": "PUT",    "route": "api/orders/{id}/total",                "entity": "Order",   "label": "Vulnerable", "expectedRule": "SYM-08" },
  { "tc": "TC06", "verb": "PUT",    "route": "api/projects/{id}",                    "entity": "Project", "label": "Protected",  "expectedRule": "SYM-04" },
  { "tc": "TC07", "verb": "DELETE", "route": "api/projects/{id}",                    "entity": "Project", "label": "Vulnerable", "expectedRule": "SYM-06" },
  { "tc": "TC08", "verb": "PUT",    "route": "api/orders/{id}/status-detached",      "entity": "Order",   "label": "Vulnerable", "expectedRule": "SYM-07" },
  { "tc": "TC09", "verb": "PUT",    "route": "api/documents/{id}",                   "entity": "Document","label": "Protected",  "expectedRule": "SYM-05" },
  { "tc": "TC10", "verb": "POST",   "route": "api/orders/{id}/process",              "entity": "Order",   "label": "Unknown",    "expectedRule": "SYM-02" },
  { "tc": "TC11", "verb": "PUT",    "route": "api/notes/{id}",                       "entity": "Note",    "label": "Vulnerable", "expectedRule": "SYM-08" },
  { "tc": "TC12", "verb": "POST",   "route": "api/orders/{id}/archive",              "entity": "Order",   "label": "Protected",  "expectedRule": "SYM-03" },
  { "tc": "TC13", "verb": "DELETE", "route": "api/invoices/{id}",                    "entity": "Invoice", "label": "Vulnerable", "expectedRule": "SYM-08" },
  { "tc": "TC14", "verb": "PUT",    "route": "api/invoices/{id}/amount",             "entity": "Invoice", "label": "Protected",  "expectedRule": "SYM-03" },
  { "tc": "TC15", "verb": "DELETE", "route": "api/orders/{id}/hard",                 "entity": "Order",   "label": "Vulnerable", "expectedRule": "SYM-07" },
  { "tc": "TC16", "verb": "PUT",    "route": "api/orders/{id}/adjust-total",         "entity": "Order",   "label": "Protected",  "expectedRule": "SYM-03" }
]
```
Matching:
- Normalise both sides: lower-case route, strip constraints and leading/trailing `/`.
- **Unit = endpoint.** If an endpoint has several paths (TC04 has paths from `id` and `targetId`), the endpoint's predicted status = the "worst" path: Vulnerable > Unknown > Protected > OutOfScope.
- Golden test asserts both the final status **and** the rule ID (rule IDs make regressions obvious).
- Labels `Unknown` (TC10) are excluded from P/R/F1 and reported separately ("correctly abstained" if the tool says Unknown).

On BolaLab: 9 Vulnerable, 6 Protected, 1 Unknown → FPR is defined (6 negatives) — good for the preliminary IPD table.

---

## 7. Expected intermediate outputs (use them as unit-test oracles)

**Schema (`schema.json`)**
| Entity | Owner candidates | Query filters | Required owner kind |
|---|---|---|---|
| Order | UserId (User), TenantId (Tenant) | — | User |
| Project | TenantId (Tenant) | TenantId == currentUser.TenantId | Tenant |
| Note | OwnerUserId (User), TenantId (Tenant) | TenantId == currentUser.TenantId | User |
| Document | OwnerUserId (User) | — | User |
| DocumentShare | UserId (User) | — | User |
| Invoice | CustomerId (User) | — | User |

**Policy (`policy.json`)**: scheme JwtBearer; fallback requires authenticated user = true; policy `DocumentEdit` (requirement `DocumentEditRequirement`); resource handler `DocumentEditHandler` (resource `Document`).

**DI map**: `ICurrentUser→HttpCurrentUser`, `IInvoiceRepository→InvoiceRepository`, `IInvoiceService→InvoiceService`, `IOrderProcessor→{Express, Standard}` (ambiguous), `IAuthorizationHandler→DocumentEditHandler`.

---

## 8. Calibration fixtures (for few-shot prompts) — `fixtures/Calibration/`

Write **3 tiny, separate** snippets with *different* domain names (e.g. `Booking`, `Pet`, `Ticket`) — one vulnerable, one protected by an owner check, one protected by a tenant filter. Generate their slices with your own slicer and hand-write the ideal JSON answers. They are the only code the prompt may contain as examples. Never reuse BolaLab or evaluation code for few-shot (leakage).

---

## 9. Later (post-IPD): dynamic confirmation on BolaLab (gap G9)

Optional validation that labels are right: run BolaLab with SQLite, mint two JWTs (user A tenant 1, user B tenant 1), seed an order owned by A, call the endpoint as B, then check the database row changed. This is **label validation**, not part of the offline detector — say so if asked.
