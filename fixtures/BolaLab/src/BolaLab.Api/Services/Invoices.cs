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
