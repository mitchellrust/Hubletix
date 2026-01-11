using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Hubletix.Api.Conventions;

/// <summary>
/// Convention to strip specific folder prefixes from Razor Page routes.
/// This is executed on app startup to modify routes, so all routable pages
/// will not have that prefix at runtime.
/// </summary>
public class StripFolderPrefixConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        foreach (var selector in model.Selectors.ToList())
        {
            if (selector.AttributeRouteModel?.Template == null)
                continue;

            var template = selector.AttributeRouteModel.Template;

            // Strip /Tenant prefix
            if (template.StartsWith("Tenant/", StringComparison.OrdinalIgnoreCase))
            {
                template = template.Substring(7); // Remove "Tenant/"
            }
            // Strip /Platform prefix
            else if (template.StartsWith("Platform/", StringComparison.OrdinalIgnoreCase))
            {
                template = template.Substring(9); // Remove "Platform/"
            }

            selector.AttributeRouteModel.Template = template;
        }
    }
}
