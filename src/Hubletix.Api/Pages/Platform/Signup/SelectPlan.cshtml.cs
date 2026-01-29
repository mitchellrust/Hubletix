using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hubletix.Infrastructure.Services;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Core.Constants;
using Hubletix.Api.Models;
using Finbuckle.MultiTenant.Abstractions;

namespace Hubletix.Api.Pages.Platform.Signup;

/// <summary>
/// TODO: This page and all the platform signup pages need some review and probably rewrite, didn't check copilot here.
/// </summary>

public class SelectPlanModel : PlatformPageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<SelectPlanModel> _logger;
    private readonly AppDbContext _dbContext;

    public SelectPlanModel(
        ITenantOnboardingService onboardingService,
        ILogger<SelectPlanModel> logger,
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
        : base(multiTenantContextAccessor)
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
            _logger.LogWarning("No platform plans found in database");
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
            return RedirectToPage("/Platform/Signup/CreateAccount", new { sessionId = session.Id });
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
