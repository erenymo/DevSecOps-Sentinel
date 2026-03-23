using AutoMapper;
using Sentinel.Application.DTOs.Requests;
using Sentinel.Application.DTOs.Responses;
using Sentinel.Domain.Entities;

namespace Sentinel.Application.Mapping
{
    public class WorkspaceProfile : Profile
    {
        public WorkspaceProfile()
        {
            CreateMap<Workspace, WorkspaceResponse>();
            CreateMap<WorkspaceRequest, Workspace>();
        }
    }
}
