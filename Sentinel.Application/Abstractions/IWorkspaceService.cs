using Sentinel.Application.DTOs.Requests;
using Sentinel.Application.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sentinel.Application.Abstractions
{
    public interface IWorkspaceService
    {
        Task<BaseResponse<List<WorkspaceResponse>>> GetAllByOwnerAsync(Guid ownerId);
        Task<BaseResponse<WorkspaceResponse>> GetByIdAsync(Guid id, Guid ownerId);
        Task<BaseResponse<Guid>> CreateAsync(WorkspaceRequest request, Guid ownerId);
        Task<BaseResponse<bool>> UpdateAsync(Guid id, WorkspaceRequest request, Guid ownerId);
        Task<BaseResponse<bool>> DeleteAsync(Guid id, Guid ownerId);
    }
}
