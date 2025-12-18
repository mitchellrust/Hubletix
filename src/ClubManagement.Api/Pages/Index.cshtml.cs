using ClubManagement.Api.Models;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using ClubManagement.Api.Utils;
using ClubManagement.Core.Models;

namespace ClubManagement.Api.Pages;

public class IndexModel : TenantPageModel
{
    public HomePageViewModel HomePage { get; set; } = new();
    private readonly AppDbContext _dbContext;
    private readonly ClubTenantInfo _currentTenantInfo;
    private readonly ITenantConfigCacheService _tenantConfigCache;
    public TenantConfig? TenantConfig { get; set; }

    public IndexModel(
        AppDbContext dbContext,
        ITenantConfigCacheService tenantConfigCache,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
        _tenantConfigCache = tenantConfigCache;
        _currentTenantInfo = multiTenantContextAccessor.MultiTenantContext.TenantInfo!;
    }

    public async Task OnGetAsync()
    {
       // Fetch tenant for config
        var tenant = await _tenantConfigCache.GetTenantConfigAsync(_currentTenantInfo.Id);
        if (tenant == null)
        {
            throw new InvalidOperationException("Tenant configuration not found.");
        }

        // Get tenant config
        TenantConfig = tenant.GetConfig();
        var primaryColor = TenantConfig?.Theme?.PrimaryColor;
        var secondaryColor = TenantConfig?.Theme?.SecondaryColor;
        var logoUrl = TenantConfig?.Theme?.LogoUrl;

        // Build navbar, conditionally adding items based on feature flags
        var navItems = new List<NavItem>();
        
        if (TenantConfig?.Features?.EnableMemberships ?? false)
        {
            navItems.Add(new() { Text = "Memberships", Url = "/membership-plans", IsActive = false });
        }
        if (TenantConfig?.Features?.EnableEventRegistration ?? false)
        {
            navItems.Add(new() { Text = "Events", Url = "/events", IsActive = false });
        }
        navItems.Add(new() { Text = "Contact Us", Url = "/contact", IsActive = false });

        HomePage.Navbar = new NavbarViewModel
        {
            TenantName = CurrentTenantInfo.Name ?? CurrentTenantInfo.Identifier,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor,
            NavItems = navItems,
            ShowLogInButton = TenantConfig?.Features?.EnableUserSignup ?? false
        };

        // Build hero section
        if (TenantConfig?.HomePage.Visibility.ShowHero ?? true)
        {
            HomePage.Hero = new HeroViewModel
            {
                Heading = TenantConfig?.HomePage.Hero?.Heading,
                Subheading = TenantConfig?.HomePage.Hero?.Subheading,
                BackgroundImageUrl = TenantConfig?.HomePage.Hero?.ImageUrl,
                CtaText = TenantConfig?.HomePage.Hero?.CtaText,
                CtaUrl =  TenantConfig?.HomePage.Hero?.CtaUrl,
                PrimaryColor = primaryColor,
                SecondaryColor = secondaryColor
            };
        }

        // Build about section
        if (TenantConfig?.HomePage.Visibility.ShowAbout ?? true)
        {
            HomePage.About = new AboutSectionViewModel
            {
                Heading = TenantConfig?.HomePage.About?.Heading ?? string.Empty,
                Description = TenantConfig?.HomePage.About?.Description ?? string.Empty,
                AccentColor = primaryColor,
                Features = GetFeatureCards(TenantConfig?.HomePage.About?.FeatureCards, primaryColor),
            };
        }

        // Build services section
        if (TenantConfig?.HomePage.Visibility.ShowServices ?? true)
        {
            HomePage.Services = new ServicesSectionViewModel
            {
                Heading = TenantConfig?.HomePage.Services?.Heading ?? string.Empty,
                Description = TenantConfig?.HomePage.Services?.Description ?? string.Empty,
                AccentColor = secondaryColor,
                Services = GetServiceCards(TenantConfig?.HomePage?.Services?.ServiceCards, primaryColor)
            };
        }

        // Section visibility
        HomePage.ShowHero = TenantConfig?.HomePage?.Visibility.ShowHero ?? true;
        HomePage.ShowAbout = TenantConfig?.HomePage?.Visibility.ShowAbout ?? true;
        HomePage.ShowServices = TenantConfig?.HomePage?.Visibility.ShowServices ?? true;
        
        // Pass data to layout via ViewData
        ViewData["TenantConfig"] = TenantConfig;
        ViewData["Navbar"] = HomePage.Navbar;
        ViewData["TenantName"] = CurrentTenantInfo.Name;
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
