using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Abstractions;
using Sentinel.Application.Abstractions.Validation;
using Sentinel.Application.DTOs.Responses;

namespace Sentinel.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestParserController : ControllerBase
    {
        private readonly IEnumerable<IParserStrategy> _parsers;
        private readonly IEnumerable<IContentValidator> _validators;
        private readonly IFileValidationService _fileValidationService;

        public TestParserController(
            IEnumerable<IParserStrategy> parsers,
            IEnumerable<IContentValidator> validators,
            IFileValidationService fileValidationService)
        {
            _parsers = parsers;
            _validators = validators;
            _fileValidationService = fileValidationService;
        }

        [HttpPost("parse")]
        public async Task<IActionResult> Parse(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is empty.");

            // --- 1. VALIDATION ---
            var validationResult = await _fileValidationService.ValidateFileAsync(file, ct);

            if (!validationResult.IsSuccess)
                return BadRequest(BaseResponse<object>.Fail(validationResult.ErrorMessage ?? "Validation failed."));

            // --- 2. VALIDATOR BUL ---
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            var validator = _validators
                .FirstOrDefault(v => v.SupportedExtension
                    .Contains(extension, StringComparer.OrdinalIgnoreCase));

            if (validator is null)
                return BadRequest(BaseResponse<object>.Fail($"No validator found for extension: {extension}"));

            // 🔥 3. ECOSYSTEM (string)
            var ecosystem = validator.Ecosystem;

            // 🔥 4. PARSER SEÇ
            var parser = _parsers
                .FirstOrDefault(p => p.Ecosystem.Equals(ecosystem, StringComparison.OrdinalIgnoreCase));

            if (parser is null)
                return StatusCode(500, BaseResponse<object>.Fail($"Parser implementation missing for ecosystem: {ecosystem}"));

            // --- 5. FILE OKUMA ---
            using var stream = file.OpenReadStream();

            // --- 6. PARSE ---
            var components = await parser.ParseAsync(stream, extension, Guid.NewGuid());

            // --- 7. RESPONSE ---
            var result = new
            {
                Ecosystem = ecosystem,
                TotalFound = components.Count,
                DirectDependencies = components.Count(c => !c.IsTransitive),
                TransitiveDependencies = components.Count(c => c.IsTransitive),
                SampleComponents = components.Take(20).Select(c => new
                {
                    c.Name,
                    c.Version,
                    c.Purl,
                    IsTransitive = c.IsTransitive ? "⚠️ YES" : "✅ NO (Direct)",
                    c.ParentName,
                    c.DependencyPath
                })
            };

            return Ok(BaseResponse<object>.Ok(result, "SBOM analysis completed successfully."));
        }
    }
}