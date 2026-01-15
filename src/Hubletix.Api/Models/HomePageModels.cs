namespace Hubletix.Api.Models;

/// <summary>
/// View model for the public navbar
/// </summary>
public class NavbarViewModel
{
    public string TenantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public List<NavItem> NavItems { get; set; } = new();
    public bool ShowLogInButton { get; set; } = false;
    public bool ShowMenuButton => NavItems.Count > 0 && ShowLogInButton;
    public string? UserEmail { get; set; }
    public bool IsUserAuthenticated { get; set; }
    public bool IsUserTenantAdmin { get; set; }
}

public class NavItem
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// View model for the hero section
/// </summary>
public class HeroViewModel
{
    public string? Heading { get; set; } = string.Empty;
    public string? Subheading { get; set; } = string.Empty;
    public string? BackgroundImageUrl { get; set; }
    public string? CtaText { get; set; } = string.Empty;
    public string? CtaUrl { get; set; } = string.Empty;
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
}

/// <summary>
/// View model for the about section
/// </summary>
public class AboutSectionViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FeatureCard> Features { get; set; } = new();
    public string? AccentColor { get; set; }
}

public class FeatureCard
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// View model for the services section
/// </summary>
public class ServicesSectionViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ServiceCard> Services { get; set; } = new();
    public string? AccentColor { get; set; }
}

public class ServiceCard
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Icon { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
}

/// <summary>
/// View model for the complete homepage
/// </summary>
public class HomePageViewModel
{
    public NavbarViewModel Navbar { get; set; } = new();
    public List<HomePageComponentViewModel> Components { get; set; } = new();
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
}

/// <summary>
/// Base class for homepage component view models
/// </summary>
public abstract class HomePageComponentViewModel
{
    public abstract string Type { get; }
    public int Order { get; set; }
}

/// <summary>
/// View model for hero component
/// </summary>
public class HeroComponentViewModel : HomePageComponentViewModel
{
    public override string Type => "Hero";
    public string Heading { get; set; } = string.Empty;
    public string Subheading { get; set; } = string.Empty;
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
}

/// <summary>
/// View model for cards component
/// </summary>
public class CardsComponentViewModel : HomePageComponentViewModel
{
    public override string Type => "Cards";
    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public List<CardViewModel> Cards { get; set; } = new();
}

/// <summary>
/// View model for individual card
/// </summary>
public class CardViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string Subheading { get; set; } = string.Empty;
}


