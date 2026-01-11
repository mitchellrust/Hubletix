using Hubletix.Api.Models;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Tenant;

public class HomeModel : PublicPageModel
{
    public HomePageViewModel HomePage { get; set; } = new();

    public HomeModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    { }

    public async Task<IActionResult> OnGetAsync()
    {
        // If no tenant context (root domain), redirect based on authentication
        if (!HasTenantContext)
        {
            // Unauthenticated users see the platform landing page
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToPage("/Platform/Index");
            }

            // Authenticated users go to tenant selector
            return RedirectToPage("/Platform/TenantSelector");
        }

        // We have tenant context, show tenant home page
        var primaryColor = TenantConfig.Theme?.PrimaryColor;
        var secondaryColor = TenantConfig.Theme?.SecondaryColor;

        // Build hero section
        if (TenantConfig.HomePage.Visibility.ShowHero)
        {
            HomePage.Hero = new HeroViewModel
            {
                Heading = TenantConfig.HomePage.Hero?.Heading,
                Subheading = TenantConfig.HomePage.Hero?.Subheading,
                BackgroundImageUrl = TenantConfig.HomePage.Hero?.ImageUrl,
                CtaText = TenantConfig.HomePage.Hero?.CtaText,
                CtaUrl =  TenantConfig.HomePage.Hero?.CtaUrl,
                PrimaryColor = primaryColor,
                SecondaryColor = secondaryColor
            };
        }

        // Build about section
        if (TenantConfig.HomePage.Visibility.ShowAbout)
        {
            HomePage.About = new AboutSectionViewModel
            {
                Heading = TenantConfig.HomePage.About?.Heading ?? string.Empty,
                Description = TenantConfig.HomePage.About?.Description ?? string.Empty,
                AccentColor = primaryColor,
                Features = GetFeatureCards(TenantConfig.HomePage.About?.FeatureCards, primaryColor),
            };
        }

        // Build services section
        if (TenantConfig.HomePage.Visibility.ShowServices)
        {
            HomePage.Services = new ServicesSectionViewModel
            {
                Heading = TenantConfig.HomePage.Services?.Heading ?? string.Empty,
                Description = TenantConfig.HomePage.Services?.Description ?? string.Empty,
                AccentColor = secondaryColor,
                Services = GetServiceCards(TenantConfig.HomePage?.Services?.ServiceCards, primaryColor)
            };
        }

        // Section visibility
        HomePage.ShowHero = TenantConfig.HomePage?.Visibility.ShowHero ?? true;
        HomePage.ShowAbout = TenantConfig.HomePage?.Visibility.ShowAbout ?? true;
        HomePage.ShowServices = TenantConfig.HomePage?.Visibility.ShowServices ?? true;

        return Page();
    }

    private List<FeatureCard> GetFeatureCards(List<FeatureCardConfig>? configs, string? primaryColor)
    {
        // If config exists, use it
        if (configs != null && configs.Any())
        {
            return configs
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new FeatureCard
                {
                    Title = c.Title,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    Icon = c.Icon,
                })
                .ToList();
        }

        // Otherwise, empty list
        return [];
    }

    private List<ServiceCard> GetServiceCards(List<ServiceCardConfig>? configs, string? primaryColor)
    {
        // If config exists, use it
        if (configs != null && configs.Any())
        {
            return configs
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new ServiceCard
                {
                    Title = c.Title,
                    Subtitle = c.Subtitle,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    Icon = c.Icon,
                    LinkUrl = c.LinkUrl,
                    LinkText = c.LinkText
                })
                .ToList();
        }

        // Otherwise use empty list
        return [];
    }
}
