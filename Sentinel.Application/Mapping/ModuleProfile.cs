using AutoMapper;
using Sentinel.Application.DTOs.Responses;
using Sentinel.Domain.Entities;

namespace Sentinel.Application.Mapping
{
    public class ModuleProfile : Profile
    {
        public ModuleProfile()
        {
            CreateMap<Module, ModuleResponse>();
        }
    }
}
