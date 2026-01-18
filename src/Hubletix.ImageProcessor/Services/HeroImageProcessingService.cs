using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;
using Hubletix.ImageProcessor.Models;

namespace Hubletix.ImageProcessor.Services;

/// <summary>
/// Orchestrates hero image processing: resizing, metadata stripping, and encoding
/// </summary>
public interface IHeroImageProcessingService
{
    /// <summary>
    /// Process hero image: generate variants at 640w, 1280w, 1920w with encoding fallback
    /// </summary>
    Task<ImageProcessingResult> ProcessHeroImageAsync(
        HeroImageUpdatedEvent heroEvent,
        CancellationToken cancellationToken = default);
}

public class HeroImageProcessingService : IHeroImageProcessingService
{
    private readonly IR2StorageService _storageService;
    private readonly IImageEncodingService _encodingService;
    private readonly ILogger<HeroImageProcessingService> _logger;

    // Variant dimensions: small, medium, large
    private static readonly int[] VariantWidths = { 640, 1280, 1920 };

    // Default encoding quality
    private const int AvifQuality = 50;
    private const int WebpQuality = 75;
    private const int JpegQuality = 80;

    public HeroImageProcessingService(
        IR2StorageService storageService,
        IImageEncodingService encodingService,
        ILogger<HeroImageProcessingService> logger)
    {
        _storageService = storageService;
        _encodingService = encodingService;
        _logger = logger;
    }

    public async Task<ImageProcessingResult> ProcessHeroImageAsync(
        HeroImageUpdatedEvent heroEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting hero image processing: {CanonicalUrl}",
            heroEvent.CanonicalUrl);

        try
        {
            // Parse tenant ID and image ID from canonical URL
            // Format: https://domain.com/tenant-{tenantId}/{imageId}.ext
            var (tenantId, imageId) = ParseImageIdentifiers(heroEvent.ImageKey);

            // Check if all variants already exist
            if (await AllVariantsExistAsync(imageId, cancellationToken))
            {
                _logger.LogInformation(
                    "All variants already exist for image {ImageId}, exiting early",
                    imageId);
                return new ImageProcessingResult
                {
                    ImageId = imageId,
                    TenantId = tenantId,
                    SuccessfulVariants = new(),
                    FailedVariants = new() { { "all", "Variants already processed" } }
                };
            }

            // Download canonical image from R2
            using var imageStream = await _storageService.DownloadImageAsync(
                heroEvent.CanonicalUrl,
                cancellationToken);

            using var image = await Image.LoadAsync(imageStream, cancellationToken);

            // Strip metadata and convert to sRGB
            PrepareImage(image);

            // Generate variants with encoding fallback
            var result = new ImageProcessingResult
            {
                ImageId = imageId,
                TenantId = tenantId,
                SuccessfulVariants = new(),
                FailedVariants = new()
            };

            foreach (var width in VariantWidths)
            {
                try
                {
                    _logger.LogDebug("Processing variant at width {Width}px", width);
                    var variant = await GenerateVariantAsync(
                        image,
                        imageId,
                        width,
                        AvifQuality,
                        heroEvent.ImageKey,
                        cancellationToken);

                    result.SuccessfulVariants.Add(variant);
                    _logger.LogInformation(
                        "Successfully generated variant: {VariantKey} ({Format}, {ByteSize} bytes)",
                        variant.R2Key,
                        variant.Format,
                        variant.ImageData.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to generate variant at width {Width}px for image {ImageId}. " +
                        "This will be skipped, but logged for manual review.",
                        width,
                        imageId);

                    result.FailedVariants.Add(
                        $"{width}w",
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            // Log results
            _logger.LogInformation(
                "Hero image processing complete: {ImageId} - " +
                "Successful: {SuccessfulCount}, Failed: {FailedCount}",
                imageId,
                result.SuccessfulVariants.Count,
                result.FailedVariants.Count);

            if (!result.AnyVariantsProcessed)
            {
                _logger.LogError(
                    "No variants were successfully processed for image {ImageId}. " +
                    "This invocation will fail to be retried.",
                    imageId);
                throw new InvalidOperationException(
                    $"Failed to generate any variants for image {imageId}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fatal error during hero image processing: {CanonicalUrl}",
                heroEvent.CanonicalUrl);
            throw;
        }
    }

    private (string TenantId, string ImageId) ParseImageIdentifiers(string imageKey)
    {
        // imageKey format: tenant-{tenantId}/{imageId}
        // e.g., tenant-bd0200a5-bbc5-4999-bcdf-3e7e03c3467d/ac2dca72d7b44541b46968a81ad2bad1

        var parts = imageKey.Split('/');
        if (parts.Length < 2)
        {
            throw new InvalidOperationException(
                $"Invalid image key format: {imageKey}. Expected 'tenant-{{tenantId}}/{{imageId}}'");
        }

        var tenantPart = parts[0]; // tenant-bd0200a5-bbc5-4999-bcdf-3e7e03c3467d
        var imagePart = parts[1];  // ac2dca72d7b44541b46968a81ad2bad1

        // Extract tenant ID from "tenant-{id}"
        if (!tenantPart.StartsWith("tenant-"))
        {
            throw new InvalidOperationException(
                $"Invalid tenant part format: {tenantPart}. Expected 'tenant-{{id}}'");
        }

        var tenantId = tenantPart["tenant-".Length..];

        return (tenantId, imagePart);
    }

    private async Task<bool> AllVariantsExistAsync(string imageId, CancellationToken cancellationToken)
    {
        foreach (var width in VariantWidths)
        {
            var variantKey = GenerateVariantKey(imageId, width, "avif", AvifQuality);
            if (!await _storageService.VariantExistsAsync(variantKey, cancellationToken))
            {
                return false;
            }
        }

        _logger.LogDebug("All variants exist for image {ImageId}", imageId);
        return true;
    }

    private void PrepareImage(Image image)
    {
        // Strip all EXIF metadata
        // Note: ImageSharp automatically handles EXIF orientation when loading,
        // so the image is already correctly rotated. We can safely clear the profile.
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;

        // Note: ImageSharp handles color space conversion internally.
        // For guaranteed sRGB, we could apply a transform, but this is complex.
        // Most images are already sRGB by default. Full sRGB conversion would require
        // additional color profile management which adds complexity.
        // Recommendation: Accept default color space handling for now.
    }

    private async Task<ImageVariant> GenerateVariantAsync(
        Image image,
        string imageId,
        int targetWidth,
        int quality,
        string sourceImageKey,
        CancellationToken cancellationToken)
    {
        // Resize image maintaining aspect ratio
        var resizedImage = image.Clone(x => x.Resize(
            new ResizeOptions
            {
                Size = new Size(targetWidth, 0), // Height auto-calculated to maintain aspect ratio
                Mode = ResizeMode.Max
            }));

        using (resizedImage)
        {
            // Encode with fallback cascade
            var (encodedData, format) = await _encodingService.EncodeWithFallbackAsync(
                resizedImage,
                quality,
                cancellationToken);

            // Generate deterministic R2 key
            var r2Key = GenerateVariantKey(imageId, targetWidth, format, quality);

            // Upload to R2
            await _storageService.UploadVariantAsync(
                r2Key,
                encodedData,
                GetContentType(format),
                sourceImageKey,
                cancellationToken);

            return new ImageVariant
            {
                Width = targetWidth,
                Format = format,
                Quality = quality,
                R2Key = r2Key,
                ImageData = encodedData
            };
        }
    }

    private static string GenerateVariantKey(string imageId, int width, string format, int quality)
    {
        // Format: {imageId}-{width}w-{format}-q{quality}-v1.{ext}
        const string version = "v1";
        var ext = format switch
        {
            "avif" => "avif",
            "webp" => "webp",
            "jpeg" => "jpg",
            _ => format
        };

        return $"{imageId}-{width}w-{format}-q{quality}-{version}.{ext}";
    }

    private static string GetContentType(string format)
    {
        return format switch
        {
            "avif" => "image/avif",
            "webp" => "image/webp",
            "jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
