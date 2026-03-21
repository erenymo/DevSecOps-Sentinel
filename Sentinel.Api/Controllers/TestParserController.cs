using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Abstractions;

namespace Sentinel.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestParserController : ControllerBase
    {
        private readonly IEnumerable<IParserStrategy> _parsers;

        public TestParserController(IEnumerable<IParserStrategy> parsers)
        {
            // NuGetAssetsParserStrategy'yi seçiyoruz
            _parsers = parsers;
        }

        [HttpPost("nuget-assets")]
        public async Task<IActionResult> TestNuGetAssets(IFormFile file)
        {
            // Güvenlik Kontrolü: Dosya boş mu veya çok mu büyük?
            if (file == null || file.Length == 0) return BadRequest("Lütfen geçerli bir project.assets.json yükleyin.");

            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            // NuGet stratejimizi seçiyoruz
            var strategy = _parsers.First(p => p.Ecosystem == "NuGet");

            // Test amaçlı rastgele bir ScanId
            var components = await strategy.ParseAsync(content, Guid.NewGuid());

            // Postman için özet rapor
            var result = new
            {
                TotalFound = components.Count,
                DirectDependencies = components.Count(c => !c.IsTransitive),
                TransitiveDependencies = components.Count(c => c.IsTransitive),
                // İlk 10 örneği listele (Kontrol amaçlı)
                SampleComponents = components.Select(c => new
                {
                    c.Name,
                    c.Version,
                    IsTransitive = c.IsTransitive ? "⚠️ YES" : "✅ NO (Direct)"
                })
            };

            return Ok(result);
        }
    }
}