using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Constants;

namespace ClubManagement.Api.Pages;

public class MembershipPlansModel : PublicPageModel
{
    public List<MembershipPlanDto> RecurringPlans { get; set; } = new();
    public List<MembershipPlanDto> OneTimePlans { get; set; } = new();

    public MembershipPlansModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    {}

    public async Task<IActionResult> OnGetAsync()
    {
        // Verify memberships are enabled
        if (!TenantConfig.Features.EnableMemberships)
        {
            return RedirectToPage("/Index");
        }

        // Fetch all active membership plans for this tenant
        var plans = await DbContext.MembershipPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.PriceInCents)
            .ToListAsync();

        // Separate into recurring and one-time plans
        RecurringPlans = plans
            .Where(p => p.BillingInterval != BillingIntervals.OneTime)
            .Select(p => new MembershipPlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PriceInDollars = p.PriceInDollars,
                BillingInterval = p.BillingInterval,
                DisplayPriceText = FormatPrice(p),
                DisplayIntervalText = FormatInterval(p.BillingInterval, p.IsPriceDisplayedMonthly),
                Features = ParseFeatures(p.Description)
            })
            .ToList();

        OneTimePlans = plans
            .Where(p => p.BillingInterval == BillingIntervals.OneTime)
            .Select(p => new MembershipPlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PriceInDollars = p.PriceInDollars,
                BillingInterval = p.BillingInterval,
                DisplayPriceText = FormatPrice(p),
                DisplayIntervalText = "One-time",
                Features = ParseFeatures(p.Description)
            })
            .ToList();

        return Page();
    }

    private string FormatPrice(Core.Entities.MembershipPlan plan)
    {
        if (plan.BillingInterval == BillingIntervals.Annually && plan.IsPriceDisplayedMonthly)
        {
            var monthlyEquivalent = plan.PriceInDollars / 12;
            return $"${monthlyEquivalent:N2}";
        }
        return $"${plan.PriceInDollars:N2}";
    }

    private string FormatInterval(string billingInterval, bool isDisplayedMonthly = false)
    {
        if (billingInterval == BillingIntervals.Monthly || (billingInterval == BillingIntervals.Annually && isDisplayedMonthly))
        {
            return "/ Month";
        }
        else if (billingInterval == BillingIntervals.Annually)
        {
            return "/ Year";
        }
        
        return "";
    }

    private List<string> ParseFeatures(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new List<string>();

        // Split description by line breaks, bullet points, or pipes
        return description
            .Split(['\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimStart('•', '-', '*').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}

public class MembershipPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceInDollars { get; set; }
    public string BillingInterval { get; set; } = string.Empty;
    public string DisplayPriceText { get; set; } = string.Empty;
    public string DisplayIntervalText { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
}
