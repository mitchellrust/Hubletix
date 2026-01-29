namespace Hubletix.Api.Middleware;

/// <summary>
/// Middleware to enforce routing rules based on hostname (root domain vs subdomains).
/// Ensures that platform routes are only accessible via the root domain,
/// and tenant routes are only accessible via subdomains.
/// </summary>
public class HostnameRouteMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HostnameRouteMiddleware> _logger;
    private readonly string _rootDomain;

    public HostnameRouteMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<HostnameRouteMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _rootDomain = configuration["AppSettings:RootDomain"]
            ?? throw new InvalidOperationException("AppSettings:RootDomain configuration is required");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? string.Empty;

        // Skip processing for static files and API routes
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var host = request.Host.Value ?? string.Empty;
        var isRootDomain = IsRootDomain(host);
        var pathAndQuery = request.Path + request.QueryString;

        // Handle root path redirect for subdomains only
        // Root domain will use default routing (Platform/Index mapped to "")
        if (path == "/" && !isRootDomain)
        {
            _logger.LogDebug("Redirecting subdomain / to /home");
            context.Response.Redirect("/home");
            return;
        }

        // Get the endpoint to determine the page path
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            // No endpoint found, let it continue (will likely 404)
            await _next(context);
            return;
        }

        // Check if this is a Razor Page by looking at the endpoint metadata
        var routeEndpoint = endpoint as RouteEndpoint;
        if (routeEndpoint == null)
        {
            // Not a route endpoint (could be a controller), let it continue
            await _next(context);
            return;
        }

        // Get the page path from the endpoint metadata
        var pageMetadata = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor>();
        if (pageMetadata == null)
        {
            // Not a Razor Page, let it continue
            await _next(context);
            return;
        }

        // Determine if this is a tenant or platform route based on the page file location
        var relativePath = pageMetadata.RelativePath;
        var isTenantRoute = relativePath.StartsWith("/Pages/Tenant/", StringComparison.OrdinalIgnoreCase);
        var isPlatformRoute = relativePath.StartsWith("/Pages/Platform/", StringComparison.OrdinalIgnoreCase);

        // Root domain accessing tenant route - redirect to tenant selector
        if (isRootDomain && isTenantRoute)
        {
            var returnUrl = Uri.EscapeDataString(pathAndQuery);
            var redirectUrl = $"/TenantSelector?returnUrl={returnUrl}";

            _logger.LogWarning(
                "Root domain attempted to access tenant route. Path: {Path}, Redirecting to: {RedirectUrl}",
                path, redirectUrl);

            context.Response.Redirect(redirectUrl);
            return;
        }

        // Subdomain accessing platform route - redirect to root domain
        if (!isRootDomain && isPlatformRoute)
        {
            var scheme = request.Scheme;
            var redirectUrl = $"{scheme}://{_rootDomain}{pathAndQuery}";

            _logger.LogWarning(
                "Subdomain attempted to access platform route. Host: {Host}, Path: {Path}, Redirecting to: {RedirectUrl}",
                host, path, redirectUrl);

            context.Response.Redirect(redirectUrl, permanent: true);
            return;
        }

        await _next(context);
    }

    private bool IsRootDomain(string host)
    {
        // Exact match with root domain (including port if specified)
        if (host.Equals(_rootDomain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Normalize for comparison (remove port if present for matching)
        var hostWithoutPort = host.Split(':')[0];
        var rootWithoutPort = _rootDomain.Split(':')[0];

        // Check if it's the root domain without subdomain
        // For localhost, must be exact match (hubletix.home or hubletix.home:port)
        if (rootWithoutPort.Equals("hubletix.home", StringComparison.OrdinalIgnoreCase) ||
            rootWithoutPort.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return hostWithoutPort.Equals(rootWithoutPort, StringComparison.OrdinalIgnoreCase);
        }

        // For production domains (e.g., hubletix.com), check if host matches root domain
        // If host is "hubletix.com" or "hubletix.com:port", it's root domain
        // If host is "demo.hubletix.com", it's a subdomain
        return hostWithoutPort.Equals(rootWithoutPort, StringComparison.OrdinalIgnoreCase);
    }
}
