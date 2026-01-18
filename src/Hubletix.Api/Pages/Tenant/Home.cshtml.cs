using Hubletix.Api.Models;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Tenant;

public class HomeModel : TenantPageModel
{
    public HomePageViewModel HomePage { get; set; } = new();

    public HomeModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ILogger<HomeModel> logger
    ) : base(
        multiTenantContextAccessor,
        logger,
        tenantConfigService,
        dbContext
    )
    { }

    public async Task<IActionResult> OnGetAsync()
    {
        var primaryColor = TenantConfig.Theme?.PrimaryColor;
        var secondaryColor = TenantConfig.Theme?.SecondaryColor;

        HomePage.PrimaryColor = primaryColor;
        HomePage.SecondaryColor = secondaryColor;

        // Build components from config
        if (TenantConfig.HomePage?.Components != null)
        {
            HomePage.Components = TenantConfig.HomePage.Components
                .OrderBy(c => c.Order)
                .Select(MapComponentToViewModel)
                .Where(vm => vm != null)
                .Cast<HomePageComponentViewModel>()
                .ToList();
        }

        return Page();
    }

    private HomePageComponentViewModel? MapComponentToViewModel(HomePageComponentConfig config)
    {
        return config switch
        {
            HeroComponentConfig hero => new HeroComponentViewModel
            {
                Order = hero.Order,
                Heading = hero.Heading,
                Subheading = hero.Subheading,
                CtaText = hero.CtaText,
                CtaUrl = hero.CtaUrl,
                BackgroundImageUrl = hero.BackgroundImageUrl
            },
            CardsComponentConfig cards => new CardsComponentViewModel
            {
                Order = cards.Order,
                Heading = cards.Heading,
                Subheading = cards.Subheading,
                Cards = cards.Cards?.Select(c => new CardViewModel
                {
                    Heading = c.Heading,
                    Subheading = c.Subheading
                }).ToList() ?? new()
            },
            _ => null
        };
    }
}

