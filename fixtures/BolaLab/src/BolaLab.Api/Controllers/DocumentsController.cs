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
