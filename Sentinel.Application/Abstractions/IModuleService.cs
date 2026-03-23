using Sentinel.Application.DTOs.Requests;
using Sentinel.Application.DTOs.Responses;

namespace Sentinel.Application.Abstractions
{
    public interface IModuleService
    {
        Task<BaseResponse<List<ModuleResponse>>> GetByWorkspaceAsync(Guid workspaceId, Guid ownerId);
        Task<BaseResponse<ModuleResponse>> GetByIdAsync(Guid id, Guid ownerId);
        Task<BaseResponse<Guid>> CreateAsync(Guid workspaceId, ModuleRequest request, Guid ownerId);
        Task<BaseResponse<bool>> DeleteAsync(Guid id, Guid ownerId);
    }
}
