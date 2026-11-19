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
