using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;

namespace Hubletix.Api.Middleware;

/// <summary>
/// Middleware for enforcing subdomain-based routing restrictions.
/// 
/// Rules:
/// - Requests to root domain (platform domain) can only access /Platform/* routes
///   (routes without folder prefix like /login, /signup, /)
/// - Requests to subdomain can only access /Tenant/* routes
///   (routes without folder prefix like /events, /admin/dashboard)
/// - Requests trying to access restricted routes are redirected to /login with returnUrl
/// </summary>
public class SubdomainRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubdomainRoutingMiddleware> _logger;

    // Routes that are part of the Tenant folder structure
    private static readonly HashSet<string> TenantRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", // For tenant-specific home page
        "/events",
        "/membershipplans",
        "/admin",
        "/noaccess",
        "/index"
    };

    // Routes that are part of the Platform folder structure (publicly accessible)
    private static readonly HashSet<string> PlatformRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", // For platform home page
        "/login",
        "/logout",
        "/unauthorized",
        "/signup",
        "/index"
    };

    public SubdomainRoutingMiddleware(RequestDelegate next, ILogger<SubdomainRoutingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IMultiTenantContextAccessor<ClubTenantInfo> tenantAccessor)
    {
        var host = context.Request.Host.Host;
        var path = context.Request.Path.Value ?? "/";
        var isSubdomain = IsSubdomain(host);

        _logger.LogInformation("Request to host: {Host}, path: {Path}, isSubdomain: {IsSubdomain}",
            host, path, isSubdomain);

        // Normalize path for comparison (lowercase, remove trailing slash except for root)
        var normalizedPath = path.ToLower();
        if (normalizedPath.Length > 1 && normalizedPath.EndsWith("/"))
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        // Check if accessing a tenant route
        var isAccessingTenantRoute = IsTenantRoute(normalizedPath);

        // Check if accessing a platform route
        var isAccessingPlatformRoute = IsPlatformRoute(normalizedPath);

        if (isSubdomain && isAccessingPlatformRoute && !isAccessingTenantRoute)
        {
            // Subdomain trying to access platform-only route (except login) - redirect to tenant path
            _logger.LogWarning("Subdomain {Host} tried to access platform route {Path}. Redirecting to tenant home page.",
                host, path);
            context.Response.Redirect("/");
            return;
        }

        if (!isSubdomain && !isAccessingPlatformRoute && isAccessingTenantRoute)
        {
            // Root domain trying to access tenant-only route - redirect to login with returnUrl
            _logger.LogWarning("Root domain tried to access tenant route {Path}. Redirecting to login.",
                path);
            var returnUrl = Uri.EscapeDataString(path);
            context.Response.Redirect($"/login?returnUrl={returnUrl}");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Determines if the host is a subdomain (not the root domain)
    /// </summary>
    private static bool IsSubdomain(string host)
    {
        // Remove port if present
        var hostOnly = host.Split(':')[0];

        // Root domains: localhost, hubletix.com, *.hubletix.local
        // Subdomains: anything.hubletix.com, demo.localhost:5000
        var segments = hostOnly.Split('.');

        // If it's localhost with subdomain (e.g., demo.localhost) -> it's a subdomain
        if (hostOnly.Contains("localhost") && segments.Length > 1)
        {
            return true;
        }

        // If it's a root domain like hubletix.com or hubletix.local -> not a subdomain
        if (hostOnly.EndsWith("hubletix.com") || hostOnly.EndsWith("hubletix.local"))
        {
            // Count segments: hubletix.com = 2, demo.hubletix.com = 3
            return segments.Length > 2;
        }

        // For other domains, consider 3+ segments as subdomains
        return segments.Length > 2;
    }

    /// <summary>
    /// Determines if the request path is a tenant route
    /// </summary>
    private static bool IsTenantRoute(string path)
    {
        // Check if path starts with any tenant route
        foreach (var route in TenantRoutes)
        {
            if (path == route || path.StartsWith(route + "/"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if the request path is a platform route
    /// </summary>
    private static bool IsPlatformRoute(string path)
    {
        // Check if path starts with any platform route
        foreach (var route in PlatformRoutes)
        {
            if (path == route || path.StartsWith(route + "/"))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Extension method to register the subdomain routing middleware
/// </summary>
public static class SubdomainRoutingMiddlewareExtensions
{
    public static IApplicationBuilder UseSubdomainRouting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubdomainRoutingMiddleware>();
    }
}
