using Application.Common;
using Application.Faqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/faqs")]
public class FaqsController(IFaqService faqs, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List the org's FAQ suggestions. Available to any signed-in user.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await faqs.ListAsync(currentUser.OrgId, ct));

    /// <summary>Create an FAQ (admin only).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(FaqInput input, CancellationToken ct)
    {
        try { return Ok(await faqs.CreateAsync(currentUser.OrgId, input, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Update an FAQ (admin only).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, FaqInput input, CancellationToken ct)
    {
        try { return Ok(await faqs.UpdateAsync(currentUser.OrgId, id, input, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Delete an FAQ (admin only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await faqs.DeleteAsync(currentUser.OrgId, id, ct);
        return NoContent();
    }
}
