using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Abstractions;
using Sentinel.Application.DTOs.Requests;
using System.Security.Claims;

namespace Sentinel.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ModuleController : ControllerBase
    {
        private readonly IModuleService _moduleService;

        public ModuleController(IModuleService moduleService)
        {
            _moduleService = moduleService;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("getByWorkspace/{workspaceId}")]
        public async Task<IActionResult> GetByWorkspace(Guid workspaceId)
        {
            var result = await _moduleService.GetByWorkspaceAsync(workspaceId, UserId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _moduleService.GetByIdAsync(id, UserId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateModuleRequest request)
        {
            var result = await _moduleService.CreateAsync(request.WorkspaceId, request.Module, UserId);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _moduleService.DeleteAsync(id, UserId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }
    }

    // Wrapper for the POST body
    public record CreateModuleRequest(Guid WorkspaceId, ModuleRequest Module);
}
