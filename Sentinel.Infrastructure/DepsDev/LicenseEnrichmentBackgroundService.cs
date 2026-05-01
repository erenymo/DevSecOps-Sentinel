using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Entities;

namespace Sentinel.Infrastructure.DepsDev
{
    /// <summary>
    /// Arka plan servisi: Kuyruktan Scan ID alır, bileşenlerin PURL'leri ile
    /// deps.dev'den lisans bilgisi çeker ve veritabanına yazar.
    /// </summary>
    public class LicenseEnrichmentBackgroundService : BackgroundService
    {
        private readonly ILicenseEnrichmentQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LicenseEnrichmentBackgroundService> _logger;

        // Aynı anda kaç paket için paralel istek atacağımız
        private const int MaxDegreeOfParallelism = 5;

        // Her istek arasında bekleme (rate-limiting koruması)
        private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(200);

        public LicenseEnrichmentBackgroundService(
            ILicenseEnrichmentQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<LicenseEnrichmentBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LicenseEnrichmentBackgroundService başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var scanId = await _queue.DequeueAsync(stoppingToken);
                    _logger.LogInformation("Lisans zenginleştirme başlıyor: ScanId={ScanId}", scanId);

                    await ProcessScanAsync(scanId, stoppingToken);

                    _logger.LogInformation("Lisans zenginleştirme tamamlandı: ScanId={ScanId}", scanId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Uygulama kapanıyor, normal çıkış
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lisans zenginleştirme sırasında beklenmeyen hata.");
                    // Hata olsa bile devam et, servisi çökertme
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("LicenseEnrichmentBackgroundService durduruluyor.");
        }

        private async Task ProcessScanAsync(Guid scanId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var depsDevClient = scope.ServiceProvider.GetRequiredService<IDepsDevClient>();

            // 1. Scan'deki tüm bileşenleri çek
            var components = await unitOfWork.Components.GetAllAsync(
                c => c.ScanId == scanId);

            var componentList = components.ToList();

            _logger.LogInformation("ScanId={ScanId}: {Count} bileşen işlenecek.", scanId, componentList.Count);

            // 2. Sadece PURL'ü olanları al
            var purls = componentList
                .Where(c => !string.IsNullOrWhiteSpace(c.Purl))
                .Select(c => c.Purl!)
                .Distinct()
                .ToList();

            // 3. Veritabanında bu PURL'lere ait lisanslar var mı kontrol et
            var existingPackageLicenses = await unitOfWork.PackageLicenses.GetAllAsync(
                pl => purls.Contains(pl.Purl));

            var processedPurls = existingPackageLicenses.Select(pl => pl.Purl).Distinct().ToHashSet();

            // Henüz lisansı olmayan PURL'leri bul
            var toProcessPurls = purls.Where(p => !processedPurls.Contains(p)).ToList();

            _logger.LogInformation("ScanId={ScanId}: {Count} farklı PURL lisans zenginleştirmesi bekliyor.", scanId, toProcessPurls.Count);

            // 4. Mevcut lisansları DB'den çekip in-memory cache'e al (deduplication)
            var existingLicenses = await unitOfWork.Licenses.GetAllAsync();
            var licenseCache = new System.Collections.Concurrent.ConcurrentDictionary<string, License>(
                existingLicenses.ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase));

            // 5. Semaphore ile kontrollü paralel işlem
            using var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);
            var tasks = toProcessPurls.Select(async purl =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await EnrichPurlAsync(purl, unitOfWork, depsDevClient, licenseCache, cancellationToken);
                    await Task.Delay(RequestDelay, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // 6. Tüm değişiklikleri tek seferde kaydet
            await unitOfWork.SaveChangesAsync();
        }

        private async Task EnrichPurlAsync(
            string purl,
            IUnitOfWork unitOfWork,
            IDepsDevClient depsDevClient,
            System.Collections.Concurrent.ConcurrentDictionary<string, License> licenseCache,
            CancellationToken cancellationToken)
        {
            try
            {
                var licenseNames = await depsDevClient.GetLicensesAsync(purl, cancellationToken);

                if (!licenseNames.Any())
                {
                    _logger.LogDebug("Lisans bulunamadı: {Purl}", purl);
                    return;
                }

                foreach (var licenseName in licenseNames)
                {
                    // ConcurrentDictionary ile deduplication: aynı isimde lisans sadece 1 kez oluşturulur
                    var license = licenseCache.GetOrAdd(licenseName, name =>
                    {
                        var newLicense = new License
                        {
                            Name = name,
                            Type = ClassifyLicenseType(name),
                            RiskLevel = ClassifyRiskLevel(name)
                        };
                        unitOfWork.Licenses.AddAsync(newLicense).GetAwaiter().GetResult();
                        return newLicense;
                    });

                    // PackageLicense ilişkisini oluştur
                    var packageLicense = new PackageLicense
                    {
                        Purl = purl,
                        LicenseId = license.Id
                    };
                    await unitOfWork.PackageLicenses.AddAsync(packageLicense);
                }

                _logger.LogDebug("Lisans eşleştirildi: {Purl} → [{Licenses}]",
                    purl, string.Join(", ", licenseNames));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PURL lisans zenginleştirme hatası: {Purl}", purl);
            }
        }

        /// <summary>
        /// Lisans adına göre temel risk seviyesi sınıflandırması.
        /// </summary>
        private static string ClassifyRiskLevel(string licenseName)
        {
            var upper = licenseName.ToUpperInvariant();

            // Copyleft lisanslar (yüksek risk)
            if (upper.Contains("GPL") && !upper.Contains("LGPL"))
                return "High";

            // Zayıf copyleft (orta risk)
            if (upper.Contains("LGPL") || upper.Contains("MPL") || upper.Contains("EPL") || upper.Contains("CDDL"))
                return "Medium";

            // Permissive lisanslar (düşük risk)
            return "Low";
        }

        /// <summary>
        /// Lisans türü sınıflandırması.
        /// </summary>
        private static string ClassifyLicenseType(string licenseName)
        {
            var upper = licenseName.ToUpperInvariant();

            if (upper.Contains("GPL") && !upper.Contains("LGPL"))
                return "Copyleft";
            if (upper.Contains("LGPL") || upper.Contains("MPL") || upper.Contains("EPL"))
                return "Weak Copyleft";
            if (upper.Contains("MIT") || upper.Contains("APACHE") || upper.Contains("BSD") || upper.Contains("ISC"))
                return "Permissive";

            return "Other";
        }
    }
}
