using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Abstractions;
using Sentinel.Application.DTOs.Requests;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Sentinel.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkspaceController : ControllerBase
    {
        private readonly IWorkspaceService _workspaceService;

        public WorkspaceController(IWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("getAll")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _workspaceService.GetAllByOwnerAsync(UserId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _workspaceService.GetByIdAsync(id, UserId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] WorkspaceRequest request)
        {
            var result = await _workspaceService.CreateAsync(request, UserId);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] WorkspaceRequest request)
        {
            var result = await _workspaceService.UpdateAsync(id, request, UserId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _workspaceService.DeleteAsync(id, UserId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }
    }
}
