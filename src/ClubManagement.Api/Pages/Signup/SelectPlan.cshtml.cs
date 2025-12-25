using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClubManagement.Infrastructure.Services;

namespace ClubManagement.Api.Pages.Signup;

public class SelectPlanModel : PageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<SelectPlanModel> _logger;

    public SelectPlanModel(
        ITenantOnboardingService onboardingService,
        ILogger<SelectPlanModel> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    public List<PlanViewModel> Plans { get; set; } = new();

    public async Task OnGetAsync()
    {
        // TODO: Load from database (PlatformPlan table)
        // For now, use hardcoded plans
        Plans = new List<PlanViewModel>
        {
            new PlanViewModel
            {
                Id = "starter",
                Name = "Starter",
                Description = "Perfect for small gyms and studios getting started",
                MonthlyPrice = 29,
                AnnualPrice = 290,
                Features = new List<string>
                {
                    "Up to 100 members",
                    "1 location",
                    "Basic membership management",
                    "Event scheduling",
                    "Email support",
                    "Mobile app access"
                },
                IsPopular = false
            },
            new PlanViewModel
            {
                Id = "professional",
                Name = "Professional",
                Description = "For growing businesses with advanced needs",
                MonthlyPrice = 79,
                AnnualPrice = 790,
                Features = new List<string>
                {
                    "Up to 500 members",
                    "3 locations",
                    "Advanced membership plans",
                    "Automated billing",
                    "Custom branding",
                    "Priority email support",
                    "Analytics & reporting",
                    "Integrations"
                },
                IsPopular = true
            },
            new PlanViewModel
            {
                Id = "enterprise",
                Name = "Enterprise",
                Description = "For large organizations with custom requirements",
                MonthlyPrice = 199,
                AnnualPrice = 1990,
                Features = new List<string>
                {
                    "Unlimited members",
                    "Unlimited locations",
                    "All Professional features",
                    "Custom integrations",
                    "Dedicated account manager",
                    "24/7 phone support",
                    "Advanced security",
                    "SLA guarantee"
                },
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
