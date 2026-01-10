using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;

namespace Hubletix.Api.Pages;

/// <summary>
/// Custom page routing convention that strips /Platform and /Tenant folder prefixes
/// from page routes, allowing them to be routed without the folder name in the URL.
/// 
/// For example:
/// - /Pages/Platform/Index.cshtml routes to /
/// - /Pages/Platform/Login.cshtml routes to /login
/// - /Pages/Tenant/Events.cshtml routes to /events
/// - /Pages/Tenant/Admin/Dashboard.cshtml routes to /admin/dashboard
/// </summary>
public class PageRoutingConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        var segments = model.ViewEnginePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (segments.Length < 2)
        {
            // No Platform/Tenant prefix to strip
            return;
        }
    
        // Check if the first segment is "Platform" or "Tenant"
        if (segments[0].Equals("Platform", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Equals("Tenant", StringComparison.OrdinalIgnoreCase))
        {
            // Reconstruct the route without the Platform/Tenant prefix
            // For example: /Platform/Index -> /
            // For example: /Platform/Login -> /login
            // For example: /Tenant/Events -> /events
            // For example: /Tenant/Admin/Dashboard -> /admin/dashboard

            var pathSegments = segments.Skip(1).ToArray();
            var lastSegment = pathSegments.LastOrDefault();
            var newPath = "/";

            // Map Index pages to root "/"; other pages to their lowercase path
            if (!string.Equals(lastSegment, "Index", StringComparison.OrdinalIgnoreCase))
            {
                newPath = "/" + string.Join("/", pathSegments).ToLower();
            }

            // Clear existing selectors and add new ones without the prefix
            model.Selectors.Clear();

            var selector = new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = newPath
                }
            };

            // Add host constraints to avoid route collisions:
            // - Platform pages: root domain only (hubletix.com, localhost)
            // - Tenant pages: subdomains only (*.hubletix.com, *.localhost)
            if (segments[0].Equals("Platform", StringComparison.OrdinalIgnoreCase))
            {
                selector.EndpointMetadata.Add(new HostAttribute("hubletix.com", "localhost"));
            }
            else
            {
                selector.EndpointMetadata.Add(new HostAttribute("*.hubletix.com", "*.localhost"));
            }

            model.Selectors.Add(selector);
        }
    }
}
