using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Services;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Constants;

namespace ClubManagement.Api.Pages.Signup;

public class SelectPlanModel : PageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<SelectPlanModel> _logger;
    private readonly AppDbContext _dbContext;

    public SelectPlanModel(
        ITenantOnboardingService onboardingService,
        ILogger<SelectPlanModel> logger,
        AppDbContext dbContext)
    {
        _onboardingService = onboardingService;
        _logger = logger;
        _dbContext = dbContext;
    }

    public List<PlanViewModel> Plans { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Load plans from database
        var platformPlans = await _dbContext.PlatformPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        if (platformPlans.Any())
        {
            // Map database plans to view models
            Plans = platformPlans.Select(p => new PlanViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                MonthlyPrice = p.PriceInDollars,
                AnnualPrice = CalculateAnnualPrice(p),
                IsPopular = p.IsFeatured
            }).ToList();
        }
        else
        {
            // Fallback to hardcoded plans if database is empty
            _logger.LogWarning("No platform plans found in database, using fallback plans");
            Plans = GetFallbackPlans();
        }
    }

    private decimal CalculateAnnualPrice(Core.Entities.PlatformPlan plan)
    {
        // If plan is already annual, return its price
        if (plan.BillingInterval == BillingIntervals.Annually)
        {
            return plan.PriceInDollars;
        }
        
        // If monthly, calculate annual with 10% discount
        if (plan.BillingInterval == BillingIntervals.Monthly)
        {
            return plan.PriceInDollars * 12 * 0.9m; // 10% discount
        }
        
        return 0;
    }

    private List<PlanViewModel> GetFallbackPlans()
    {
        return new List<PlanViewModel>
        {
            new PlanViewModel
            {
                Id = "starter",
                Name = "Starter",
                Description = "Perfect for small gyms and studios getting started",
                MonthlyPrice = 29,
                AnnualPrice = 290,
                IsPopular = false
            },
            new PlanViewModel
            {
                Id = "professional",
                Name = "Professional",
                Description = "For growing businesses with advanced needs",
                MonthlyPrice = 79,
                AnnualPrice = 790,
                IsPopular = true
            },
            new PlanViewModel
            {
                Id = "enterprise",
                Name = "Enterprise",
                Description = "For large organizations with custom requirements",
                MonthlyPrice = 199,
                AnnualPrice = 1990,
                IsPopular = false
            }
        };
    }

    public async Task<IActionResult> OnPostAsync(string planId)
    {
        if (string.IsNullOrEmpty(planId))
        {
            ModelState.AddModelError("", "Please select a plan");
            await OnGetAsync();
            return Page();
        }

        try
        {
            // Start signup session
            var session = await _onboardingService.StartSignupSessionAsync(
                planId,
                string.Empty // Email will be collected in next step
            );
            
            _logger.LogInformation(
                "Started signup session: SessionId={SessionId}, PlanId={PlanId}",
                session.Id,
                planId
            );

            // Redirect to account creation
            return RedirectToPage("/Signup/CreateAccount", new { sessionId = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start signup session for plan: {PlanId}", planId);
            ModelState.AddModelError("", "An error occurred. Please try again.");
            await OnGetAsync();
            return Page();
        }
    }

    public class PlanViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public decimal AnnualPrice { get; set; }
        public List<string> Features { get; set; } = new();
        public bool IsPopular { get; set; }
    }
}
