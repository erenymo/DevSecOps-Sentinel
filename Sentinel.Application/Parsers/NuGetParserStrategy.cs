using Sentinel.Application.Abstractions;
using Sentinel.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sentinel.Application.Parsers
{
    public class NuGetParserStrategy : IParserStrategy
    {
        // NUGET roject.assets.json dosyası yeterlidir çünkü bu dosya tüm bağımlılık ağacını içerir.
        // Bunun yanında .csproj dosyası da okunabilir ancak bu dosya sadece doğrudan bağımlılıkları içerir, transitive bağımlılıkları içermez.
        public string Ecosystem => "NuGet";

        public async Task<List<Component>> ParseAsync(string fileContent, Guid scanId)
        {
            if (!fileContent.Contains("\"libraries\"") || !fileContent.Contains("\"targets\""))
            {
                throw new ArgumentException("Yüklenen dosya geçerli bir project.assets.json değil. Lütfen geçerli bir dosya yükleyin.");
            }

            var components = new List<Component>();
            var directDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var internalProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var doc = JsonDocument.Parse(fileContent);
            var root = doc.RootElement;

            // Extraction of Internal Project Names from project.assets.json
            if (root.TryGetProperty("libraries", out var libraries))
            {
                foreach (var lib in libraries.EnumerateObject())
                {
                    if (lib.Value.GetProperty("type").GetString() == "project") internalProjectNames.Add(lib.Name.Split('/')[0]);
                }
            }

            // DIRECT DEPENDENCIES : targets -> package -> internalProjectName -> dependencies
            if (root.TryGetProperty("targets", out var targets))
            {
                foreach (var framework in targets.EnumerateObject())
                {
                    foreach (var item in framework.Value.EnumerateObject())
                    {
                        var itemName = item.Name.Split('/')[0];

                        if (internalProjectNames.Contains(itemName) &&
                            item.Value.TryGetProperty("dependencies", out var deps))
                        {
                            foreach (var dep in deps.EnumerateObject())
                            {
                                if (!internalProjectNames.Contains(dep.Name))
                                    directDependencies.Add(dep.Name);
                            }
                        }
                    }
                }
            }

            // Creation of Component entity
            if (root.TryGetProperty("libraries", out var libs))
            {
                foreach (var library in libs.EnumerateObject())
                {
                    if (library.Value.GetProperty("type").GetString() == "package")
                    {
                        var parts = library.Name.Split('/');
                        var name = parts[0];
                        var version = parts[1];

                        components.Add(new Component
                        {
                            ScanId = scanId,
                            Name = name,
                            Version = version,
                            Purl = $"pkg:nuget/{name}@{version}",
                            IsTransitive = !directDependencies.Contains(name), // Direct setinde yoksa Transitive'dir
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            return components;
        }
    }
}
