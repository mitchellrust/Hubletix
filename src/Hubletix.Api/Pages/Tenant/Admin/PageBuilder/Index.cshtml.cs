using Microsoft.AspNetCore.Mvc;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Models;
using Hubletix.Core.Constants;
using Hubletix.Api.Utils;

namespace Hubletix.Api.Pages.Tenant.Admin.Homepage;

public class ComponentDto
{
    public string Type { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
    public List<CardDto>? Cards { get; set; }
}

public class CardDto
{
    public string Heading { get; set; } = string.Empty;
    public string Subheading { get; set; } = string.Empty;
}

public class IndexModel : TenantAdminPageModel
{
    [BindProperty]
    public List<ComponentDto> ComponentDtos { get; set; } = new();
    
    public List<HomePageComponentConfig> Components { get; set; } = new();
    
    public List<object> ComponentRenderContexts { get; set; } = new();
    
    public string PrimaryColor { get; set; } = ThemeDefaults.PrimaryColor;
    public string SecondaryColor { get; set; } = ThemeDefaults.SecondaryColor;

    public int MaxComponents => 5;
    public int MaxCardsPerComponent => 3;

    public IndexModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    {
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var tenant = await TenantConfigService.GetTenantAsync(CurrentTenantInfo.Id!);
        if (tenant != null)
        {
            TenantConfig = tenant.GetConfig();
            Components = TenantConfig.HomePage?.Components ?? new();
            
            // Load theme colors from tenant config with defaults
            PrimaryColor = !string.IsNullOrEmpty(TenantConfig.Theme?.PrimaryColor) 
                ? TenantConfig.Theme.PrimaryColor 
                : ThemeDefaults.PrimaryColor;
            SecondaryColor = !string.IsNullOrEmpty(TenantConfig.Theme?.SecondaryColor) 
                ? TenantConfig.Theme.SecondaryColor 
                : ThemeDefaults.SecondaryColor;
            
            // Create render contexts for initial preview
            ComponentRenderContexts = Components.Select(component => 
                (object)new ComponentRenderContext<HomePageComponentConfig>
                {
                    Component = component,
                    PrimaryColor = PrimaryColor,
                    SecondaryColor = SecondaryColor,
                    IsPreviewMode = true
                }
            ).ToList();
            
            // Convert to DTOs for display
            ComponentDtos = Components.Select(ToDto).ToList();
        }
        
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync([FromForm] string? PrimaryColor, [FromForm] string? SecondaryColor)
    {
        // Use provided theme colors or defaults
        var primaryColor = !string.IsNullOrEmpty(PrimaryColor) ? PrimaryColor : ThemeDefaults.PrimaryColor;
        var secondaryColor = !string.IsNullOrEmpty(SecondaryColor) ? SecondaryColor : ThemeDefaults.SecondaryColor;
        
        // Convert DTOs to components
        Components = ComponentDtos.Select(FromDto).Where(c => c != null).Cast<HomePageComponentConfig>().ToList();
        
        // Validate components
        var validationResult = ValidateComponents();
        if (!string.IsNullOrEmpty(validationResult))
        {
            return BadRequest(validationResult);
        }

        // Wrap components in render context for preview
        var renderContexts = Components.Select(component => 
            (object)new ComponentRenderContext<HomePageComponentConfig>
            {
                Component = component,
                PrimaryColor = primaryColor,
                SecondaryColor = secondaryColor,
                IsPreviewMode = true
            }
        ).ToList();

        // Return partial view for preview
        return Partial("_PreviewPanel", renderContexts);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Convert DTOs to components
        Components = ComponentDtos.Select(FromDto).Where(c => c != null).Cast<HomePageComponentConfig>().ToList();
        
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate components
        var validationResult = ValidateComponents();
        if (!string.IsNullOrEmpty(validationResult))
        {
            ModelState.AddModelError(string.Empty, validationResult);
            return Page();
        }

        // Update tenant config
        var tenant = await TenantConfigService.GetTenantAsync(CurrentTenantInfo.Id!);
        if (tenant != null)
        {
            tenant.UpdateConfig(config =>
            {
                config.HomePage = config.HomePage ?? new HomePageConfig();
                config.HomePage.Components = Components;
            });

            await DbContext.SaveChangesAsync();
            
            // Invalidate cache
            TenantConfigService.InvalidateCache(CurrentTenantInfo.Id!);
        }

        TempData["SuccessMessage"] = "Homepage saved successfully!";
        return RedirectToPage();
    }
    
    private ComponentDto ToDto(HomePageComponentConfig component)
    {
        var dto = new ComponentDto
        {
            Order = component.Order,
            Type = component is HeroComponentConfig ? "Hero" : "Cards"
        };

        if (component is HeroComponentConfig hero)
        {
            dto.Heading = hero.Heading;
            dto.Subheading = hero.Subheading;
            dto.CtaText = hero.CtaText;
            dto.CtaUrl = hero.CtaUrl;
        }
        else if (component is CardsComponentConfig cards)
        {
            dto.Heading = cards.Heading;
            dto.Subheading = cards.Subheading;
            dto.Cards = cards.Cards?.Select(c => new CardDto
            {
                Heading = c.Heading,
                Subheading = c.Subheading
            }).ToList();
        }

        return dto;
    }

    private HomePageComponentConfig? FromDto(ComponentDto dto)
    {
        if (dto.Type == "Hero")
        {
            return new HeroComponentConfig
            {
                Order = dto.Order,
                Heading = dto.Heading ?? string.Empty,
                Subheading = dto.Subheading ?? string.Empty,
                CtaText = dto.CtaText,
                CtaUrl = dto.CtaUrl
            };
        }
        else if (dto.Type == "Cards")
        {
            return new CardsComponentConfig
            {
                Order = dto.Order,
                Heading = dto.Heading,
                Subheading = dto.Subheading,
                Cards = dto.Cards?.Select(c => new CardConfig
                {
                    Heading = c.Heading,
                    Subheading = c.Subheading
                }).ToList() ?? new()
            };
        }

        return null;
    }

    private string? ValidateComponents()
    {
        if (Components == null)
        {
            Components = new();
        }

        // Max 5 components
        if (Components.Count > MaxComponents)
        {
            return $"Maximum of {MaxComponents} components allowed.";
        }

        // Validate each component
        for (int i = 0; i < Components.Count; i++)
        {
            var component = Components[i];
            component.Order = i;

            if (component is CardsComponentConfig cardsComponent)
            {
                if (cardsComponent.Cards.Count > MaxCardsPerComponent)
                {
                    return $"Maximum of {MaxCardsPerComponent} cards allowed per Cards component.";
                }
            }
        }

        return null;
    }
}
