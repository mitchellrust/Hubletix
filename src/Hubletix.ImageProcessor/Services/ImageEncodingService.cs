using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Extensions.Logging;
using Hubletix.ImageProcessor.Models;

namespace Hubletix.ImageProcessor.Services;

/// <summary>
/// Handles image encoding with format fallback cascade (AVIF → WebP → JPEG)
/// </summary>
public interface IImageEncodingService
{
    /// <summary>
    /// Encode image to best-effort format with cascade fallback
    /// </summary>
    Task<(byte[] Data, string Format)> EncodeWithFallbackAsync(
        Image image,
        int quality,
        CancellationToken cancellationToken = default);
}

public class ImageEncodingService : IImageEncodingService
{
    private readonly ILogger<ImageEncodingService> _logger;

    public ImageEncodingService(ILogger<ImageEncodingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempt to encode image in AVIF (q50) → WebP (q75) → JPEG (q80) order.
    /// Returns first successful encoding.
    /// Note: AVIF support may not be available in all ImageSharp versions.
    /// </summary>
    public async Task<(byte[] Data, string Format)> EncodeWithFallbackAsync(
        Image image,
        int quality,
        CancellationToken cancellationToken = default)
    {
        // Attempt WebP encoding first (AVIF may not be available)
        try
        {
            var webpQuality = Math.Min(quality + 25, 80); // WebP quality typically higher than AVIF
            _logger.LogDebug("Attempting WebP encoding with quality {Quality}", webpQuality);
            var webpData = await EncodeWebpAsync(image, webpQuality);
            _logger.LogInformation("Successfully encoded image to WebP");
            return (webpData, "webp");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebP encoding failed, attempting JPEG fallback");
        }

        // Fallback to JPEG encoding
        try
        {
            var jpegQuality = Math.Min(quality + 30, 85); // JPEG quality adjusted for compatibility
            _logger.LogDebug("Attempting JPEG encoding with quality {Quality}", jpegQuality);
            var jpegData = await EncodeJpegAsync(image, jpegQuality);
            _logger.LogInformation("Successfully encoded image to JPEG");
            return (jpegData, "jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All image encoding formats failed (WebP, JPEG)");
            throw new InvalidOperationException("Unable to encode image in any supported format", ex);
        }
    }

    private async Task<byte[]> EncodeWebpAsync(Image image, int quality)
    {
        using var ms = new MemoryStream();
        var encoder = new WebpEncoder { Quality = Math.Clamp(quality, 0, 100) };
        await image.SaveAsWebpAsync(ms, encoder);
        return ms.ToArray();
    }

    private async Task<byte[]> EncodeJpegAsync(Image image, int quality)
    {
        using var ms = new MemoryStream();
        var encoder = new JpegEncoder { Quality = Math.Clamp(quality, 0, 100) };
        await image.SaveAsJpegAsync(ms, encoder);
        return ms.ToArray();
    }
}
