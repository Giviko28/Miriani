using Application.Common;
using Application.OrgDb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/org-db")]
public class OrgDbController(IOrgDbConfigService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await service.GetAsync(currentUser.OrgId, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save([FromBody] OrgDbConfigDto dto, CancellationToken ct)
    {
        var result = await service.SaveAsync(currentUser.OrgId, dto, ct);
        return Ok(result);
    }

    [HttpPost("explore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Explore(CancellationToken ct)
    {
        var summary = await service.ExploreAsync(currentUser.OrgId, ct);
        return Ok(new { summary });
    }

    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await service.DisconnectAsync(currentUser.OrgId, ct);
        return NoContent();
    }
}
