using Sentinel.Application.Abstractions.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sentinel.Infrastructure.Security.Validators
{
    /// <summary>
    /// npm ekosistemi için içerik doğrulama servisi.
    /// package.json ve package-lock.json dosyalarının yapısal bütünlüğünü kontrol eder.
    /// Güvenlik odaklı: Aşırı derin JSON yapıları, geçersiz formatlar ve zararlı içerikler engellenir.
    /// </summary>
    public class NpmContentValidator : IContentValidator
    {
        public string Ecosystem => "npm";
        public HashSet<string> SupportedExtension => new() { ".json" };
        public HashSet<string>? SupportedFileNames => new(StringComparer.OrdinalIgnoreCase) { "package.json", "package-lock.json" };

        // Güvenlik limitleri
        private const int MaxJsonDepth = 128;
        private const long MaxAllowedStreamSize = 10 * 1024 * 1024; // 10 MB

        public async Task<ValidationResult> ValidateAsync(Stream stream, CancellationToken ct)
        {
            // Stream başa alınmalı
            if (stream.CanSeek)
                stream.Position = 0;

            // 1. Boyut kontrolü (stream boyutu biliniyorsa)
            if (stream.CanSeek && stream.Length > MaxAllowedStreamSize)
                return ValidationResult.Failure("File size exceeds the maximum allowed limit (10 MB).");

            // 2. İlk karakteri oku (lightweight magic check)
            var buffer = new byte[1];
            var bytesRead = await stream.ReadAsync(buffer, 0, 1, ct);

            if (bytesRead == 0)
                return ValidationResult.Failure("File is empty.");

            if (stream.CanSeek)
                stream.Position = 0;

            char firstChar = (char)buffer[0];

            // JSON dosyası '{' ile başlamalı
            if (firstChar != '{')
                return ValidationResult.Failure("Invalid npm manifest file. Expected a JSON object.");

            try
            {
                // 3. JSON yapısal doğrulama (derinlik limitli)
                var options = new JsonDocumentOptions
                {
                    MaxDepth = MaxJsonDepth,
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                using var jsonDoc = await JsonDocument.ParseAsync(stream, options, ct);
                var root = jsonDoc.RootElement;

                // 4. Root element bir JSON nesnesi olmalı
                if (root.ValueKind != JsonValueKind.Object)
                    return ValidationResult.Failure("Invalid JSON structure. Root element must be an object.");

                // 5. İçerik tabanlı doğrulama: npm manifest dosyası mı?
                bool hasName = root.TryGetProperty("name", out _);
                bool hasVersion = root.TryGetProperty("version", out _);
                bool hasLockfileVersion = root.TryGetProperty("lockfileVersion", out _);
                bool hasDependencies = root.TryGetProperty("dependencies", out _);
                bool hasDevDependencies = root.TryGetProperty("devDependencies", out _);
                bool hasPackages = root.TryGetProperty("packages", out _);

                // package-lock.json → lockfileVersion zorunlu
                if (hasLockfileVersion)
                {
                    // lockfileVersion var → package-lock.json olarak kabul et
                    // "packages" veya "dependencies" düğümlerinden biri olmalı
                    if (!hasPackages && !hasDependencies)
                        return ValidationResult.Failure("Invalid package-lock.json: missing 'packages' or 'dependencies' node.");

                    return ValidationResult.Success();
                }

                // package.json → name veya dependencies/devDependencies bekleniyor
                if (hasName || hasDependencies || hasDevDependencies)
                {
                    return ValidationResult.Success();
                }

                return ValidationResult.Failure(
                    "Uploaded file does not appear to be a valid package.json or package-lock.json. " +
                    "Expected 'name', 'dependencies', 'devDependencies', or 'lockfileVersion' properties.");
            }
            catch (JsonException ex)
            {
                return ValidationResult.Failure($"Malformed JSON content: {ex.Message}");
            }
            catch (Exception)
            {
                return ValidationResult.Failure("Invalid file structure or malicious content detected.");
            }
        }
    }
}
