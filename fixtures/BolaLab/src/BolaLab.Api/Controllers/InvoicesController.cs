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
