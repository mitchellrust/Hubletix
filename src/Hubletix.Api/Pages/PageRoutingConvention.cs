using Microsoft.AspNetCore.Mvc.ApplicationModels;

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
            
            var newPath = "/" + string.Join("/", segments.Skip(1));
            
            // Clear existing selectors and add new ones without the prefix
            model.Selectors.Clear();
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = newPath == "/" ? "/" : newPath.ToLower()
                }
            });
        }
    }
}
