using Application.Common;
using Application.Users;
using Infrastructure.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/users")]
public class UsersController(IUserAdminService users, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List users in the caller's organization.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await users.ListAsync(currentUser.OrgId, ct));

    /// <summary>Create a user; returns a one-time temporary password.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var result = await users.CreateAsync(currentUser.OrgId, request, ct);
            return Ok(result);
        }
        catch (DuplicateUserException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Reset a user's password; returns a one-time temporary password.</summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await users.ResetPasswordAsync(currentUser.OrgId, id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Change a user's role and/or enable/disable them.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await users.UpdateAsync(currentUser.OrgId, currentUser.UserId, id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Delete a user.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await users.DeleteAsync(currentUser.OrgId, currentUser.UserId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
