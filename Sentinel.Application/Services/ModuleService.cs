using AutoMapper;
using Sentinel.Application.Abstractions;
using Sentinel.Application.DTOs.Requests;
using Sentinel.Application.DTOs.Responses;
using Sentinel.Domain.Entities;

namespace Sentinel.Application.Services
{
    public class ModuleService : IModuleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ModuleService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<BaseResponse<List<ModuleResponse>>> GetByWorkspaceAsync(Guid workspaceId, Guid ownerId)
        {
            // Verify workspace ownership
            var workspace = await _unitOfWork.Workspaces.GetByIdAsync(workspaceId);
            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<List<ModuleResponse>>.Fail("Workspace not found");

            var modules = await _unitOfWork.Modules.GetAllAsync(m => m.WorkspaceId == workspaceId);
            var response = _mapper.Map<List<ModuleResponse>>(modules);
            return BaseResponse<List<ModuleResponse>>.Ok(response);
        }

        public async Task<BaseResponse<ModuleResponse>> GetByIdAsync(Guid id, Guid ownerId)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(id);
            if (module == null)
                return BaseResponse<ModuleResponse>.Fail("Module not found");

            // Verify workspace ownership
            var workspace = await _unitOfWork.Workspaces.GetByIdAsync(module.WorkspaceId);
            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<ModuleResponse>.Fail("Module not found");

            return BaseResponse<ModuleResponse>.Ok(_mapper.Map<ModuleResponse>(module));
        }

        public async Task<BaseResponse<Guid>> CreateAsync(Guid workspaceId, ModuleRequest request, Guid ownerId)
        {
            // Verify workspace ownership
            var workspace = await _unitOfWork.Workspaces.GetByIdAsync(workspaceId);
            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<Guid>.Fail("Workspace not found");

            var module = new Module
            {
                Name = request.Name,
                Ecosystem = request.Ecosystem,
                RootPath = request.RootPath,
                WorkspaceId = workspaceId
            };

            await _unitOfWork.Modules.AddAsync(module);
            await _unitOfWork.SaveChangesAsync();

            return BaseResponse<Guid>.Ok(module.Id, "Module created successfully");
        }

        public async Task<BaseResponse<bool>> DeleteAsync(Guid id, Guid ownerId)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(id);
            if (module == null)
                return BaseResponse<bool>.Fail("Module not found");

            var workspace = await _unitOfWork.Workspaces.GetByIdAsync(module.WorkspaceId);
            if (workspace == null || workspace.OwnerId != ownerId)
                return BaseResponse<bool>.Fail("Module not found");

            _unitOfWork.Modules.Delete(module);
            await _unitOfWork.SaveChangesAsync();

            return BaseResponse<bool>.Ok(true, "Module deleted successfully");
        }
    }
}
