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
