using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Hubletix.Api.Conventions;

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
