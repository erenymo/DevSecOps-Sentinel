using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Application.Abstractions.Validation
{
    public interface IContentValidator
    {
        string Ecosystem { get; }
        HashSet<string> SupportedExtension { get; }

        /// <summary>
        /// Desteklenen dosya adları (opsiyonel).
        /// Aynı uzantıyı (.json gibi) birden fazla validator desteklediğinde,
        /// dosya adına göre doğru validator'ı seçmek için kullanılır.
        /// Null veya boş dönerse sadece uzantı kontrolü yapılır.
        /// Örn: { "package.json", "package-lock.json" } → npm
        /// Örn: { "project.assets.json" } → NuGet
        /// </summary>
        HashSet<string>? SupportedFileNames => null;

        Task<ValidationResult> ValidateAsync(Stream stream, CancellationToken ct);
    }
}
