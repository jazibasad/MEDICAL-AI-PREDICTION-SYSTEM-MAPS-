using MAPS.API.Services.Admin;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = PolicyNames.AdminOnly)]
public class UsersController : ControllerBase
{
    private readonly IAdminService _adminService;

    public UsersController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    private Guid AdminId => Guid.Parse(
        User.FindFirst(ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/users — paginated user list</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationRequest req)
    {
        var result = await _adminService.GetAllUsersAsync(req);
        return Ok(result);
    }

    /// <summary>GET /api/users/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _adminService.GetUserByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>GET /api/users/pending — pending approvals</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var result = await _adminService.GetPendingApprovalsAsync();
        return Ok(result);
    }

    /// <summary>POST /api/users/{id}/approve</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var result = await _adminService.ApproveUserAsync(id, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/users/{id}/deactivate</summary>
    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _adminService.DeactivateUserAsync(id, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>PUT /api/users/{id}/activate</summary>
    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _adminService.ActivateUserAsync(id, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/users/{id} — soft delete</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _adminService.DeleteUserAsync(id, AdminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
