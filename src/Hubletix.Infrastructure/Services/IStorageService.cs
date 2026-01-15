namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Service for managing cloud storage operations (Cloudflare R2)
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads an image to cloud storage with validation
    /// </summary>
    /// <param name="stream">The image file stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <param name="tenantId">Tenant ID for organizing storage</param>
    /// <returns>Public URL of the uploaded image</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, string tenantId);

    /// <summary>
    /// Deletes an image from cloud storage by its URL
    /// </summary>
    /// <param name="imageUrl">The full URL of the image to delete</param>
    Task DeleteImageAsync(string imageUrl);

    /// <summary>
    /// Extracts the storage key from a full image URL
    /// </summary>
    /// <param name="imageUrl">The full URL of the image</param>
    /// <returns>The storage key (path within the bucket)</returns>
    string ExtractKeyFromUrl(string imageUrl);
}
