using Sentinel.Application.Abstractions;
using Sentinel.Application.DTOs;
using Sentinel.Application.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sentinel.Application.Services
{
    public class ComponentService : IComponentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ComponentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponse<IEnumerable<ComponentDto>>> GetByModuleIdAsync(Guid moduleId)
        {
            try
            {
                var scans = await _unitOfWork.Scans.GetAllAsync(s => s.ModuleId == moduleId);
                var latestScan = scans.OrderByDescending(s => s.ScanDate).FirstOrDefault();

                if (latestScan == null)
                {
                    return BaseResponse<IEnumerable<ComponentDto>>.Ok(new List<ComponentDto>(), "Bu modül için henüz tarama yapılmamış.");
                }

                var components = await _unitOfWork.Components.GetAllAsync(c => c.ScanId == latestScan.Id);
                
                var licenses = await _unitOfWork.Licenses.GetAllAsync();
                var licenseDict = licenses.ToDictionary(l => l.Id, l => l.Name);

                var dtos = components.Select(c => new ComponentDto(
                    c.Id,
                    c.Name,
                    c.Version,
                    c.Purl,
                    c.IsTransitive,
                    c.ParentName,
                    c.DependencyPath,
                    c.LicenseId.HasValue && licenseDict.ContainsKey(c.LicenseId.Value) ? licenseDict[c.LicenseId.Value] : c.License?.Name
                ));

                return BaseResponse<IEnumerable<ComponentDto>>.Ok(dtos.ToList(), "Bileşenler başarıyla getirildi.");
            }
            catch (Exception ex)
            {
                return BaseResponse<IEnumerable<ComponentDto>>.Fail($"Hata oluştu: {ex.Message}");
            }
        }
    }
}
