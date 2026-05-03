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
    /// <summary>
    /// npm ekosistemi için parser stratejisi.
    /// package.json (sadece direct) ve package-lock.json (direct + transitive) dosyalarını destekler.
    /// İçerik tabanlı routing: "lockfileVersion" property'si varsa package-lock.json olarak işlenir.
    /// </summary>
    public class NpmParserStrategy : IParserStrategy
    {
        public string Ecosystem => "npm";

        // JSON derinlik limiti — aşırı derin iç içe yapılarla yapılabilecek DoS saldırılarına karşı koruma
        private const int MaxJsonDepth = 128;

        public async Task<List<Component>> ParseAsync(Stream stream, string extension, Guid scanId)
        {
            // 1. İçeriğe göre formatı belirle (ikisi de .json uzantılı)
            // Stream'i bir kez parse edip hem tür tespiti hem veri çıkarımı yapacağız.
            var options = new JsonDocumentOptions
            {
                MaxDepth = MaxJsonDepth,
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            using var doc = await JsonDocument.ParseAsync(stream, options);
            var root = doc.RootElement;

            // "lockfileVersion" property'si varsa → package-lock.json
            if (root.TryGetProperty("lockfileVersion", out _))
            {
                return ParsePackageLockJson(root, scanId);
            }

            // Yoksa → package.json
            return ParsePackageJson(root, scanId);
        }

        /// <summary>
        /// package.json dosyasını parse eder.
        /// Bu dosya sadece doğrudan bağımlılıkları (direct dependencies) içerir.
        /// "dependencies" ve "devDependencies" okunur.
        /// </summary>
        private List<Component> ParsePackageJson(JsonElement root, Guid scanId)
        {
            var components = new List<Component>();

            // Proje adını "name" property'sinden oku
            string projectName = root.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? "UnknownProject"
                : "UnknownProject";

            // dependencies
            if (root.TryGetProperty("dependencies", out var deps))
            {
                AddDirectDependencies(deps, components, scanId, projectName);
            }

            // devDependencies
            if (root.TryGetProperty("devDependencies", out var devDeps))
            {
                AddDirectDependencies(devDeps, components, scanId, projectName);
            }

            return components;
        }

        /// <summary>
        /// Bir dependency bloğundaki tüm paketleri doğrudan bağımlılık olarak ekler.
        /// package.json'daki "dependencies" ve "devDependencies" için kullanılır.
        /// </summary>
        private void AddDirectDependencies(JsonElement depsNode, List<Component> components, Guid scanId, string projectName)
        {
            foreach (var dep in depsNode.EnumerateObject())
            {
                var name = dep.Name;
                var versionRange = dep.Value.GetString() ?? "0.0.0";

                // Semver range temizleme (^, ~, >=, vb. kaldır)
                var cleanVersion = CleanSemverRange(versionRange);

                // npm scoped paketler için purl: pkg:npm/%40scope/name@version
                var purl = BuildNpmPurl(name, cleanVersion);

                components.Add(new Component
                {
                    Id = Guid.NewGuid(),
                    ScanId = scanId,
                    Name = name,
                    Version = cleanVersion,
                    Purl = purl,
                    IsTransitive = false,
                    ParentName = null,
                    DependencyPath = $"{projectName} -> {name}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// package-lock.json dosyasını parse eder (lockfileVersion 2 ve 3 destekli).
        /// "packages" düğümünü kullanır. Root entry ("") içindeki dependencies
        /// doğrudan bağımlılıklardır; diğerleri transitive'dir.
        /// </summary>
        private List<Component> ParsePackageLockJson(JsonElement root, Guid scanId)
        {
            var components = new List<Component>();

            // Root proje adı
            string projectName = root.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? "UnknownProject"
                : "UnknownProject";

            // Doğrudan bağımlılık isimlerini topla (root "" entry'sinden)
            var directDependencyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Hangi paketi kimin getirdiğini tutan harita
            var parentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // lockfileVersion 2/3 → "packages" düğümünü kullan
            if (root.TryGetProperty("packages", out var packages))
            {
                // 1. ADIM: Root entry'den doğrudan bağımlılık isimlerini çıkar
                if (packages.TryGetProperty("", out var rootEntry))
                {
                    CollectDirectDependencyNames(rootEntry, directDependencyNames);
                }

                // 2. ADIM: Tüm node_modules entry'lerini tara
                foreach (var pkg in packages.EnumerateObject())
                {
                    var key = pkg.Name; // Örn: "node_modules/@tanstack/react-query"

                    // Root entry ("") → atla, bu projenin kendisi
                    if (string.IsNullOrEmpty(key))
                        continue;

                    // "node_modules/" prefix'ini kaldırarak paket adını çıkar
                    var packageName = ExtractPackageNameFromKey(key);
                    if (string.IsNullOrEmpty(packageName))
                        continue;

                    var version = pkg.Value.TryGetProperty("version", out var versionElement)
                        ? versionElement.GetString() ?? "0.0.0"
                        : "0.0.0";

                    bool isDirect = directDependencyNames.Contains(packageName);

                    // İç içe node_modules → parent tespiti
                    // Örn: "node_modules/A/node_modules/B" → B'nin parent'ı A
                    var parentName = ExtractParentFromKey(key);
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        parentMap[packageName] = parentName;
                    }
                    else if (!isDirect)
                    {
                        // Alt dependencyler → bu paketi çağıran üst paketi bulmaya çalış
                        // package-lock.json "packages" düğümünde doğrudan parent bilgisi yoktur,
                        // bu yüzden iç içe path'e güveniyoruz.
                    }

                    // Dependency path oluştur
                    string dependencyPath;
                    if (isDirect)
                    {
                        dependencyPath = $"{projectName} -> {packageName}";
                    }
                    else
                    {
                        dependencyPath = BuildDependencyPath(packageName, parentMap, projectName);
                    }

                    var purl = BuildNpmPurl(packageName, version);

                    components.Add(new Component
                    {
                        Id = Guid.NewGuid(),
                        ScanId = scanId,
                        Name = packageName,
                        Version = version,
                        Purl = purl,
                        IsTransitive = !isDirect,
                        ParentName = isDirect ? null : (parentMap.TryGetValue(packageName, out var parent) ? parent : null),
                        DependencyPath = dependencyPath,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            // lockfileVersion 1 fallback → eski "dependencies" düğümünü kullan
            else if (root.TryGetProperty("dependencies", out var depsNode))
            {
                ParseLockfileV1Dependencies(depsNode, components, scanId, projectName, directDependencyNames, isTopLevel: true);
            }

            return components;
        }

        /// <summary>
        /// Root entry içindeki "dependencies" ve "devDependencies" isimlerini toplar.
        /// Bu isimler doğrudan bağımlılıklar olarak işaretlenecektir.
        /// </summary>
        private void CollectDirectDependencyNames(JsonElement rootEntry, HashSet<string> directNames)
        {
            if (rootEntry.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateObject())
                    directNames.Add(dep.Name);
            }
            if (rootEntry.TryGetProperty("devDependencies", out var devDeps))
            {
                foreach (var dep in devDeps.EnumerateObject())
                    directNames.Add(dep.Name);
            }
        }

        /// <summary>
        /// "node_modules/..." key'inden paket adını çıkarır.
        /// Scoped paketleri de destekler: "node_modules/@scope/name" → "@scope/name"
        /// İç içe paketler: "node_modules/A/node_modules/B" → "B"
        /// </summary>
        private string ExtractPackageNameFromKey(string key)
        {
            // Son "node_modules/" segmentinden sonrasını al
            const string nodeModulesPrefix = "node_modules/";
            var lastIndex = key.LastIndexOf(nodeModulesPrefix, StringComparison.Ordinal);

            if (lastIndex < 0)
                return key;

            var afterPrefix = key.Substring(lastIndex + nodeModulesPrefix.Length);

            // Scoped paketler: @scope/name → sonraki "/" olup olmadığını kontrol et
            if (afterPrefix.StartsWith("@"))
            {
                // @scope/name → tamamı paket adı (sonraki node_modules'a kadar)
                return afterPrefix;
            }

            return afterPrefix;
        }

        /// <summary>
        /// İç içe node_modules yollarından parent paket adını çıkarır.
        /// Örn: "node_modules/A/node_modules/B" → A
        /// Örn: "node_modules/A" → null (root level, parent yok)
        /// </summary>
        private string? ExtractParentFromKey(string key)
        {
            const string nodeModulesPrefix = "node_modules/";

            // Kaç "node_modules/" segmenti var?
            var segments = key.Split(new[] { nodeModulesPrefix }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length < 2)
                return null;

            // Sondan bir önceki segment → parent
            var parentSegment = segments[segments.Length - 2];

            // Trailing "/" varsa temizle
            return parentSegment.TrimEnd('/');
        }

        /// <summary>
        /// lockfileVersion 1 için eski "dependencies" düğümünü recursive olarak parse eder.
        /// Her dependency'nin kendi "dependencies" alt düğümü olabilir (transitive).
        /// </summary>
        private void ParseLockfileV1Dependencies(
            JsonElement depsNode,
            List<Component> components,
            Guid scanId,
            string projectName,
            HashSet<string> directNames,
            bool isTopLevel,
            string? parentPackageName = null)
        {
            foreach (var dep in depsNode.EnumerateObject())
            {
                var name = dep.Name;
                var version = dep.Value.TryGetProperty("version", out var versionElement)
                    ? versionElement.GetString() ?? "0.0.0"
                    : "0.0.0";

                bool isDirect = isTopLevel;

                if (isTopLevel)
                    directNames.Add(name);

                var purl = BuildNpmPurl(name, version);
                var dependencyPath = isDirect
                    ? $"{projectName} -> {name}"
                    : $"{projectName} -> ... -> {parentPackageName} -> {name}";

                components.Add(new Component
                {
                    Id = Guid.NewGuid(),
                    ScanId = scanId,
                    Name = name,
                    Version = version,
                    Purl = purl,
                    IsTransitive = !isDirect,
                    ParentName = isDirect ? null : parentPackageName,
                    DependencyPath = dependencyPath,
                    CreatedAt = DateTime.UtcNow
                });

                // Recursive: Alt bağımlılıklar (transitive)
                if (dep.Value.TryGetProperty("dependencies", out var subDeps))
                {
                    ParseLockfileV1Dependencies(subDeps, components, scanId, projectName, directNames, isTopLevel: false, parentPackageName: name);
                }
            }
        }

        /// <summary>
        /// DependencyPath oluşturur.
        /// parentMap üzerinden üst ebeveynleri takip eder.
        /// </summary>
        private string BuildDependencyPath(string packageName, Dictionary<string, string> parentMap, string projectName)
        {
            var pathList = new List<string> { packageName };
            var current = parentMap.TryGetValue(packageName, out var p) ? p : null;

            // Sonsuz döngü koruması (maksimum 50 seviye)
            int safetyCounter = 0;
            const int maxDepth = 50;

            while (!string.IsNullOrEmpty(current) && safetyCounter < maxDepth)
            {
                pathList.Add(current);
                current = parentMap.TryGetValue(current, out var nextP) ? nextP : null;
                safetyCounter++;
            }

            if (!pathList.Contains(projectName))
                pathList.Add(projectName);

            pathList.Reverse();
            return string.Join(" -> ", pathList);
        }

        /// <summary>
        /// npm Package URL (purl) oluşturur.
        /// Scoped paketler için (@scope/name) URL encoding uygular.
        /// Spesifikasyon: https://github.com/package-url/purl-spec
        /// </summary>
        private string BuildNpmPurl(string name, string version)
        {
            if (name.StartsWith("@"))
            {
                // Scoped paketler: pkg:npm/%40scope/name@version
                // '@' karakteri '%40' olarak encode edilir (purl spec)
                var encodedName = name.Replace("@", "%40");
                return $"pkg:npm/{encodedName}@{version}";
            }

            return $"pkg:npm/{name}@{version}";
        }

        /// <summary>
        /// Semver range prefix'lerini temizler.
        /// Örn: "^1.2.3" → "1.2.3", "~2.0.0" → "2.0.0", ">=1.0.0" → "1.0.0"
        /// </summary>
        private string CleanSemverRange(string versionRange)
        {
            if (string.IsNullOrWhiteSpace(versionRange))
                return "0.0.0";

            // Range operatörlerini temizle
            var cleaned = versionRange
                .TrimStart('^', '~', '>', '<', '=', ' ');

            // "x" range desteği: "1.x" → "1.0.0"
            cleaned = cleaned.Replace(".x", ".0").Replace(".*", ".0");

            // Eğer hiçbir şey kalmadıysa
            if (string.IsNullOrWhiteSpace(cleaned))
                return "0.0.0";

            return cleaned;
        }
    }
}
