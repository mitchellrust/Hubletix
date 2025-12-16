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

        // Build navbar
        HomePage.Navbar = new NavbarViewModel
        {
            TenantName = CurrentTenantInfo.Name ?? "Club",
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor,
            NavItems = new List<NavItem>
            {
                new() { Text = "Memberships", Url = "/membership-plans", IsActive = false },
                new() { Text = "Events", Url = "/events", IsActive = false },
                new() { Text = "Contact Us", Url = "/contact", IsActive = false }
            },
            ShowLogInButton = TenantConfig?.Features?.EnableUserSignup ?? false
        };

        // Build hero section
        HomePage.Hero = new HeroViewModel
        {
            Heading = TenantConfig?.Theme?.HeroHeading ?? "Unleash Your Inner Champion Today. All In One Place.",
            Subheading = TenantConfig?.Theme?.HeroSubheading ?? "Join the ultimate sports experience. Train, compete, and connect with fellow athletes.",
            BackgroundImageUrl = TenantConfig?.Theme?.HeroImageUrl,
            CtaText = "Start your own journey",
            CtaUrl = "/events",
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor
        };

        // Build about section
        HomePage.About = new AboutSectionViewModel
        {
            Heading = $"About {CurrentTenantInfo.Name}",
            Description = TenantConfig?.Theme?.AboutDescription ?? $"At {CurrentTenantInfo.Name}, we don't just play — we live it. Since our founding, our club has been a home for players of all levels, from eager beginners to seasoned pros.",
            Features = new List<FeatureCard>
            {
                new()
                {
                    Title = "Professional Facilities",
                    Description = "With tournament-grade courts, lighting & climate control — play in perfect conditions, in any season.",
                    Icon = "bi bi-building",
                    BackgroundColor = "#1F2937"
                },
                new()
                {
                    Title = "Private & Group Lessons",
                    Description = "Expert coaching tailored to your skill level. Learn from the best and elevate your game.",
                    BackgroundColor = "#3B82F6"
                },
                new()
                {
                    Title = "Pro Coaches",
                    Description = "Our experienced coaching staff is ready to boost your game from first serve to tournament level.",
                    BackgroundColor = primaryColor ?? "#667eea"
                }
            },
            Stats = new List<StatItem>
            {
                new() { Value = "12,000+", Label = "Hours of play annually" },
                new() { Value = "89%", Label = "Player Retention Rate" },
                new() { Value = "1,200+", Label = "Active Members" },
                new() { Value = "125+", Label = "Annual Tournaments" }
            }
        };

        // Build services section
        HomePage.Services = new ServicesSectionViewModel
        {
            Heading = "Our Services",
            Description = "Explore our full range of coaching, training, and sports experiences. From first serve to match point — we've got the right program for you.",
            PrimaryColor = primaryColor,
            Services = new List<ServiceCard>
            {
                new()
                {
                    Title = "Training Programs",
                    Subtitle = "Personalized Coaching",
                    Description = "Expertly designed for all ages and abilities. From fundamentals to advanced techniques.",
                    BackgroundColor = "#F97316",
                    LinkUrl = "/services/training",
                    LinkText = "Explore More"
                },
                new()
                {
                    Title = "Court Rental",
                    Subtitle = "Hourly Court Rental",
                    Description = "Step into a space built for players — to grow, compete, and thrive.",
                    Icon = "bi bi-calendar-check",
                    LinkUrl = "/events",
                    LinkText = "Book Now"
                },
                new()
                {
                    Title = "Events & Tournaments",
                    Subtitle = "Competitive Play",
                    Description = "Join our tournaments, leagues, and social events. Connect with other players and test your skills.",
                    Icon = "bi bi-trophy",
                    LinkUrl = "/events",
                    LinkText = "View Events"
                }
            }
        };

        // Section visibility (can be controlled by tenant config in the future)
        HomePage.ShowHero = true;
        HomePage.ShowAbout = true;
        HomePage.ShowServices = true;
    }
}
