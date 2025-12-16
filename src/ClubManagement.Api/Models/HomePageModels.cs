namespace ClubManagement.Api.Models;

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
    public string? Heading { get; set; } = "Your Hero Heading Here.";
    public string? Subheading { get; set; } = "Your Hero Subheading here.";
    public string? BackgroundImageUrl { get; set; }
    public string? CtaText { get; set; } = "Get Started";
    public string? CtaUrl { get; set; } = "#";
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
}

/// <summary>
/// View model for the about section
/// </summary>
public class AboutSectionViewModel
{
    public string Heading { get; set; } = "About Us";
    public string Description { get; set; } = string.Empty;
    public List<StatItem> Stats { get; set; } = new();
    public List<FeatureCard> Features { get; set; } = new();
}

public class StatItem
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
}

public class FeatureCard
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? BackgroundColor { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// View model for the services section
/// </summary>
public class ServicesSectionViewModel
{
    public string Heading { get; set; } = "Our Services";
    public string Description { get; set; } = string.Empty;
    public List<ServiceCard> Services { get; set; } = new();
    public string? PrimaryColor { get; set; }
}

public class ServiceCard
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? BackgroundColor { get; set; }
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
    public HeroViewModel Hero { get; set; } = new();
    public AboutSectionViewModel About { get; set; } = new();
    public ServicesSectionViewModel Services { get; set; } = new();
    
    // Page sections visibility
    public bool ShowHero { get; set; } = true;
    public bool ShowAbout { get; set; } = true;
    public bool ShowServices { get; set; } = true;
}
