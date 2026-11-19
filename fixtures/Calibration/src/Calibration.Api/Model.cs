using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calibration.Api;

public class Booking
{
    public long Id { get; set; }
    public string OwnerId { get; set; } = "";
    public DateTime Slot { get; set; }
}

public class Pet
{
    public long Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Nickname { get; set; } = "";
}

public class Ticket
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public string Subject { get; set; } = "";
}

public interface IClinicSession
{
    string UserId { get; }
    int CompanyId { get; }
}

public sealed class HttpClinicSession(IHttpContextAccessor http) : IClinicSession
{
    public string UserId => http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    public int CompanyId => int.TryParse(http.HttpContext?.User.FindFirstValue("company"), out var c) ? c : 0;
}

public class ClinicDb(DbContextOptions<ClinicDb> options, IClinicSession session) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.Entity<Ticket>().HasQueryFilter(t => t.CompanyId == session.CompanyId);
}

public sealed record SlotChange(DateTime Slot);
public sealed record NameChange(string Nickname);
public sealed record SubjectChange(string Subject);

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(ClinicDb db) : ControllerBase
{
    [HttpPatch("{bookingId}")]
    public async Task<IActionResult> Move(long bookingId, SlotChange change)
    {
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Slot = change.Slot;
        await db.SaveChangesAsync();
        return Ok();
    }
}

[ApiController]
[Route("api/pets")]
[Authorize]
public class PetsController(ClinicDb db, IClinicSession session) : ControllerBase
{
    [HttpPatch("{petId}")]
    public async Task<IActionResult> Rename(long petId, NameChange change)
    {
        var pet = await db.Pets.FindAsync(petId);
        if (pet is null || pet.OwnerId != session.UserId) return NotFound();
        pet.Nickname = change.Nickname;
        await db.SaveChangesAsync();
        return Ok();
    }
}

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(ClinicDb db) : ControllerBase
{
    [HttpPatch("{ticketId}")]
    public async Task<IActionResult> Retitle(long ticketId, SubjectChange change)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return NotFound();
        ticket.Subject = change.Subject;
        await db.SaveChangesAsync();
        return Ok();
    }
}
