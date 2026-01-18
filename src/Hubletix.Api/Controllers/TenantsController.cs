using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using SixLabors.ImageSharp;

namespace Hubletix.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TenantsController : ControllerBase
{
    private readonly IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor;
    private readonly ITenantConfigService _tenantConfigService;
    private readonly IStorageService _storageService;
    private readonly IEventBridgeService _eventBridgeService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        IStorageService storageService,
        IEventBridgeService eventBridgeService,
        ILogger<TenantsController> logger
    )
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _tenantConfigService = tenantConfigService;
        _storageService = storageService;
        _eventBridgeService = eventBridgeService;
        _logger = logger;
    }

    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrentTenant()
    {
        if (_multiTenantContextAccessor.MultiTenantContext.TenantInfo == null)
        {
            return NotFound("No tenant context set");
        }

        var tenant = await _tenantConfigService.GetTenantAsync(
            _multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id);

        if (tenant == null)
        {
            return NotFound("Tenant configuration not found");
        }

        return Ok(new
        {
            tenant!.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.Status,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            tenant.ConfigJson
        });
    }

    [HttpPost("invalidate-cache")]
    [AllowAnonymous] // TODO: Secure this endpoint with admin authorization
    public IActionResult InvalidateCache()
    {
        if (_multiTenantContextAccessor.MultiTenantContext.TenantInfo == null)
        {
            return NotFound("No tenant context set");
        }

        var tenantId = _multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id;
        _tenantConfigService.InvalidateCache(tenantId);

        return Ok(new { 
            message = "Cache invalidated successfully", 
            tenantId,
            timestamp = DateTime.UtcNow 
        });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Uploads a hero background image to Cloudflare R2 storage
    /// </summary>
    /// <param name="file">The image file to upload</param>
    /// <returns>JSON response with the image URL or error message</returns>
    [HttpPost("upload-hero-image")]
    [Authorize(Policy = "TenantAdmin")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB limit
    public async Task<IActionResult> UploadHeroImageAsync(IFormFile file)
    {
        try
        {
            // Validate tenant context
            if (_multiTenantContextAccessor.MultiTenantContext?.TenantInfo == null)
            {
                return BadRequest(new { success = false, error = "No tenant context found" });
            }

            var tenantId = _multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id;

            // Validate file presence
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, error = "No file uploaded" });
            }

            // Validate file size (10 MB max)
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { success = false, error = "File size exceeds 10 MB limit" });
            }

            // Validate content type
            var supportedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!supportedTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest(new 
                { 
                    success = false, 
                    error = "Unsupported file type. Only JPEG, PNG, and WebP are supported" 
                });
            }

            // Upload to R2 storage
            using var stream = file.OpenReadStream();
            var imageUrl = await _storageService.UploadImageAsync(
                stream, 
                file.FileName, 
                file.ContentType, 
                tenantId);

            _logger.LogInformation(
                "Successfully uploaded hero image for tenant {TenantId}: {ImageUrl}", 
                tenantId, 
                imageUrl);

            // Publish HeroImageUpdated event to EventBridge for background processing
            // Fire-and-forget: don't await to avoid blocking the response
            _ = Task.Run(async () =>
            {
                try
                {
                    // Extract image key from URL path
                    // URL format: https://domain.com/tenant-{tenantId}/{imageId}.ext
                    var uri = new Uri(imageUrl);
                    var imageKey = uri.AbsolutePath.TrimStart('/');

                    await _eventBridgeService.PublishHeroImageUpdatedAsync(
                        imageUrl,
                        imageKey);

                    _logger.LogInformation(
                        "Published HeroImageUpdated event for image: {ImageKey}",
                        imageKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to publish HeroImageUpdated event for image: {ImageUrl}",
                        imageUrl);
                    // Don't re-throw in background task - log only
                }
            });

            return Ok(new { success = true, imageUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error during hero image upload");
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading hero image");
            return StatusCode(500, new 
            { 
                success = false, 
                error = "An error occurred while uploading the image. Please try again." 
            });
        }
    }
}
