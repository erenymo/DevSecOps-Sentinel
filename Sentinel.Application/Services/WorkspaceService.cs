using AutoMapper;
using Sentinel.Application.Abstractions;
using Sentinel.Application.DTOs.Requests;
using Sentinel.Application.DTOs.Responses;
using Sentinel.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sentinel.Application.Services
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public WorkspaceService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<BaseResponse<List<WorkspaceResponse>>> GetAllByOwnerAsync(Guid ownerId)
        {
            var workspaces = await _unitOfWork.Workspaces
                .GetAllAsync(w => w.OwnerId == ownerId);
            
            var response = _mapper.Map<List<WorkspaceResponse>>(workspaces);
            return BaseResponse<List<WorkspaceResponse>>.Ok(response);
        }

        public async Task<BaseResponse<WorkspaceResponse>> GetByIdAsync(Guid id, Guid ownerId)
        {
            var workspace = await _unitOfWork.Workspaces
                .GetByIdAsync(id);

            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<WorkspaceResponse>.Fail("Workspace not found");

            return BaseResponse<WorkspaceResponse>.Ok(_mapper.Map<WorkspaceResponse>(workspace));
        }

        public async Task<BaseResponse<Guid>> CreateAsync(WorkspaceRequest request, Guid ownerId)
        {
            var workspace = new Workspace
            {
                Name = request.Name,
                Description = request.Description,
                OwnerId = ownerId
            };

            await _unitOfWork.Workspaces.AddAsync(workspace);
            await _unitOfWork.SaveChangesAsync();

            return BaseResponse<Guid>.Ok(workspace.Id, "Workspace created successfully");
        }

        public async Task<BaseResponse<bool>> UpdateAsync(Guid id, WorkspaceRequest request, Guid ownerId)
        {
            var workspace = await _unitOfWork.Workspaces.GetByIdAsync(id);

            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<bool>.Fail("Workspace not found");

            workspace.Name = request.Name;
            workspace.Description = request.Description;

            _unitOfWork.Workspaces.Update(workspace);
            await _unitOfWork.SaveChangesAsync();

            return BaseResponse<bool>.Ok(true, "Workspace updated successfully");
        }

        public async Task<BaseResponse<bool>> DeleteAsync(Guid id, Guid ownerId)
        {
            var workspace = await _unitOfWork.Workspaces.GetByIdAsync(id);

            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<bool>.Fail("Workspace not found");

            _unitOfWork.Workspaces.Delete(workspace);
            await _unitOfWork.SaveChangesAsync();

            return BaseResponse<bool>.Ok(true, "Workspace deleted successfully");
        }
    }
}
