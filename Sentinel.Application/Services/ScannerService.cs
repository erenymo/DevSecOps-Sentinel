using Sentinel.Application.Abstractions;
using Sentinel.Domain.Entities;
using Sentinel.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Application.Services
{
    public class ScannerService : IScannerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEnumerable<IParserStrategy> _parsers;

        public ScannerService(IUnitOfWork unitOfWork, IEnumerable<IParserStrategy> parsers)
        {
            _unitOfWork = unitOfWork;
            _parsers = parsers;
        }

        public async Task<Result<Guid>> RunScanAsync(Guid moduleId, string fileName, string fileContent)
        {
            try
            {
                // S-SDLC: Dosya içeriği boş mu kontrolü
                if (string.IsNullOrWhiteSpace(fileContent))
                    return Result<Guid>.Failure("Geçersiz dosya formatı. Lütfen project.assets.json yükleyin.");

                var module = await _unitOfWork.Modules.GetByIdAsync(moduleId);
                if (module == null) return Result<Guid>.Failure("Modül bulunamadı.");
                var parser = _parsers.FirstOrDefault(p => p.Ecosystem == module.Ecosystem);
                if (parser == null) return Result<Guid>.Failure($"{module.Ecosystem} için uygur parser bulunamadı.");

                var scan = new Scan
                {
                    ModuleId = moduleId,
                    ScanDate = DateTime.UtcNow,
                    SbomOutput = "{}" // will be CycloneDX format.
                };
                await _unitOfWork.Scans.AddAsync(scan);

                List<Component> components = await parser.ParseAsync(fileContent, scan.Id);

                foreach (var comp in components)
                {
                    await _unitOfWork.Components.AddAsync(comp);
                }

                await _unitOfWork.SaveChangesAsync();

                return Result<Guid>.Success(scan.Id, "Tarama başarıyla tamamlandı.");
            } catch(Exception ex)
            {
                return Result<Guid>.Failure($"Tarama sırasında hata oluştu: {ex.Message}");
            }
            
        }
    }
}
