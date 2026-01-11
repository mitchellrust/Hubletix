namespace Hubletix.Api.Middleware;

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

        // Handle root path redirects
        if (path == "/" && !isRootDomain)
        {
            _logger.LogDebug("Redirecting subdomain / to /Home");
            context.Response.Redirect("/Home");
            return;
        }

        // Check if path is a tenant route
        var isTenantRoute = path.StartsWith("/home", StringComparison.OrdinalIgnoreCase) ||
                           path.StartsWith("/events", StringComparison.OrdinalIgnoreCase) ||
                           path.StartsWith("/eventdetail", StringComparison.OrdinalIgnoreCase) ||
                           path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        // Check if path is a platform route
        var isPlatformRoute = path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("/tenantselector", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("/signup", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("/index", StringComparison.OrdinalIgnoreCase);

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
        // For localhost, must be exact match (localhost or localhost:port)
        if (rootWithoutPort.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
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
