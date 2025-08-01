using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IHashCalculationService
    {
        Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default);
        Task CalculateHashesForImagesAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default);
    }

    public class HashCalculationService : IHashCalculationService
    {
        private readonly ILogger<HashCalculationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPhotoPathService _photoPathService;

        public HashCalculationService(
            ILogger<HashCalculationService> logger,
            IServiceProvider serviceProvider,
            IPhotoPathService photoPathService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _photoPathService = photoPathService;
        }

        public async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                
                var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating hash for file: {filePath}");
                throw;
            }
        }

        public async Task CalculateHashesForImagesAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            // Get total count of non-deleted images
            var totalImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .CountAsync(cancellationToken);

            // Get count of images that already have hashes
            var imagesWithHash = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && 
                             img.IsDeleted == 0 && 
                             img.FileHash != null && 
                             img.FileHash != "")
                .CountAsync(cancellationToken);

            // Get images without hashes
            var imagesWithoutHash = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && 
                             img.IsDeleted == 0 && 
                             (img.FileHash == null || img.FileHash == ""))
                .ToListAsync(cancellationToken);

            _logger.LogInformation($"Hash calculation: {imagesWithHash} completed, {imagesWithoutHash.Count} remaining, {totalImages} total");

            var phaseProgress = new PhaseProgress
            {
                Phase = ScanPhase.Phase4_Hashing,
                TotalItems = totalImages,
                ProcessedItems = imagesWithHash,
                StartTime = DateTime.UtcNow
            };

            progress?.Report(phaseProgress);

            const int BATCH_SIZE = 50;
            var processedInBatch = 0;
            var currentIndex = 0;

            foreach (var image in imagesWithoutHash)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                currentIndex++;

                try
                {
                    var fullPath = await _photoPathService.GetFullPathForImageAsync(image);
                    if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                    {
                        _logger.LogWarning($"File not found for hash calculation: {image.RelativePath}");
                        phaseProgress.ErrorCount++;
                    }
                    else
                    {
                        phaseProgress.CurrentItem = image.FileName;
                        
                        var hash = await CalculateFileHashAsync(fullPath, cancellationToken);
                        image.FileHash = hash;
                        
                        processedInBatch++;
                        phaseProgress.SuccessCount++;

                        // Save in batches
                        if (processedInBatch >= BATCH_SIZE)
                        {
                            await dbContext.SaveChangesAsync(cancellationToken);
                            processedInBatch = 0;
                            _logger.LogDebug($"Saved batch of {BATCH_SIZE} hashes");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error calculating hash for image {image.ImageId}: {image.FileName}");
                    phaseProgress.ErrorCount++;
                }

                phaseProgress.ProcessedItems = imagesWithHash + currentIndex;
                progress?.Report(phaseProgress);
            }

            // Save any remaining
            if (processedInBatch > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            phaseProgress.EndTime = DateTime.UtcNow;
            progress?.Report(phaseProgress);

            _logger.LogInformation($"Hash calculation completed. Success: {phaseProgress.SuccessCount}, Errors: {phaseProgress.ErrorCount}");
        }
    }
}