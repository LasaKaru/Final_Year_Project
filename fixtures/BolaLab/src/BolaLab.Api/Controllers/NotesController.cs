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
