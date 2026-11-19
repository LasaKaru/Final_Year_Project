using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calibration.Api;

// Probe endpoints for the model calibration gate. They are NOT few-shot examples and NOT evaluation data.

public class Vehicle
{
    public long Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Plate { get; set; } = "";
}

public class Recipe
{
    public long Id { get; set; }
    public string AuthorId { get; set; } = "";
    public string Title { get; set; } = "";
}

public class Album
{
    public long Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Garden
{
    public long Id { get; set; }
    public string OwnerId { get; set; } = "";
    public int Size { get; set; }
}

public class ProbeDb(DbContextOptions<ProbeDb> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Garden> Gardens => Set<Garden>();
}

public sealed record PlateChange(string Plate);
public sealed record AlbumRename(string OwnerId, string Name);
public sealed record GardenResize(int Size);

[ApiController]
[Route("api/vehicles")]
[Authorize]
public class VehiclesController(ProbeDb db) : ControllerBase
{
    [HttpPut("{vehicleId}/plate")]
    public async Task<IActionResult> ChangePlate(long vehicleId, PlateChange change)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
        if (vehicle is null) return NotFound();
        vehicle.Plate = change.Plate;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/recipes")]
[Authorize]
public class RecipesController(ProbeDb db, IClinicSession session) : ControllerBase
{
    [HttpDelete("{recipeId}")]
    public async Task<IActionResult> Remove(long recipeId)
    {
        var n = await db.Recipes.Where(r => r.Id == recipeId && r.AuthorId == session.UserId).ExecuteDeleteAsync();
        return n == 0 ? NotFound() : NoContent();
    }
}

[ApiController]
[Route("api/albums")]
[Authorize]
public class AlbumsController(ProbeDb db) : ControllerBase
{
    [HttpPut("{albumId}")]
    public async Task<IActionResult> Rename(long albumId, AlbumRename change)
    {
        var album = await db.Albums.FindAsync(albumId);
        if (album is null || album.OwnerId != change.OwnerId) return Forbid();
        album.Name = change.Name;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/gardens")]
[Authorize]
public class GardensController(ProbeDb db) : ControllerBase
{
    [HttpPut("{gardenId}/size")]
    public async Task<IActionResult> Resize(long gardenId, GardenResize change)
    {
        var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var garden = await db.Gardens.SingleAsync(g => g.Id == gardenId);
        if (garden.OwnerId != me) return Forbid();
        garden.Size = change.Size;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
