using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Constants;
using ClubManagement.Api.Utils;
using ClubManagement.Infrastructure.Services;

namespace ClubManagement.Api.Pages.Admin;

public class PlanDetailModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;

    [BindProperty]
    public MembershipPlan? Plan { get; set; }

    [BindProperty]
    public decimal PriceInDollars { get; set; }

    public List<SelectListItem> BillingIntervalOptions { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public PlanDetailModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        // ID wasn't provided, 404 not found feels right.
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        Plan = await _dbContext.MembershipPlans
            .FirstOrDefaultAsync(p => p.Id == id);

        // If plan not found, should return better UI than 404.
        if (Plan == null)
        {
            return Page();
        }

        PriceInDollars = Plan.PriceInDollars;

        PopulateBillingIntervalOptions();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Plan == null || string.IsNullOrEmpty(Plan.Id))
        {
            return BadRequest();
        }

        // Verify plan still exists in DB
        var existingPlan = await _dbContext.MembershipPlans
            .FirstOrDefaultAsync(p => p.Id == Plan.Id);

        // If event not found, should return better UI than 404.
        if (existingPlan == null)
        {
            Plan = null;
            return Page();
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(Plan.Name))
        {
            ErrorMessage = "Plan name is required.";
            PopulateBillingIntervalOptions();
            Plan = existingPlan;
            PriceInDollars = existingPlan.PriceInDollars;
            return Page();
        }

        // Validate price is positive
        if (PriceInDollars <= 0)
        {
            ErrorMessage = "Price must be greater than zero.";
            PopulateBillingIntervalOptions();
            Plan = existingPlan;
            PriceInDollars = existingPlan.PriceInDollars;
            return Page();
        }

        // Convert dollars to cents with proper rounding
        int priceInCents = (int)Math.Round(PriceInDollars * 100, MidpointRounding.AwayFromZero);

        // Check if any fields have actually changed
        bool hasChanges =
            existingPlan.Name != Plan.Name ||
            existingPlan.Description != Plan.Description ||
            existingPlan.PriceInCents != priceInCents ||
            existingPlan.BillingInterval != Plan.BillingInterval ||
            existingPlan.IsPriceDisplayedMonthly != Plan.IsPriceDisplayedMonthly ||
            existingPlan.DisplayOrder != Plan.DisplayOrder ||
            existingPlan.IsActive != Plan.IsActive;

        if (!hasChanges)
        {
            StatusMessage = "No changes were made.";
        }
        else
        {
            // Update allowed fields
            existingPlan.Name = Plan.Name;
            existingPlan.Description = Plan.Description;
            existingPlan.PriceInCents = priceInCents;
            existingPlan.BillingInterval = Plan.BillingInterval;
            existingPlan.IsPriceDisplayedMonthly = Plan.IsPriceDisplayedMonthly;
            existingPlan.DisplayOrder = Plan.DisplayOrder;
            existingPlan.IsActive = Plan.IsActive;

            try
            {
                _dbContext.MembershipPlans.Update(existingPlan);
                await _dbContext.SaveChangesAsync();
                StatusMessage = "Plan updated successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating plan: {ex.Message}";
            }
        }

        // Repopulate form with current values
        PopulateBillingIntervalOptions();
        Plan = existingPlan;
        PriceInDollars = existingPlan.PriceInDollars;

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest();
        }

        // Verify plan exists
        var planToDelete = await _dbContext.MembershipPlans
            .FirstOrDefaultAsync(p => p.Id == id);

        // If plan was not found, must have already been deleted.
        if (planToDelete == null)
        {
            return RedirectToPage("/Admin/Plans", new { message = "Plan had already been deleted." });
        }

        try
        {
            _dbContext.MembershipPlans.Remove(planToDelete);
            await _dbContext.SaveChangesAsync();
            return RedirectToPage("/Admin/Plans", new { message = "Plan deleted successfully." });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting plan: {ex.Message}";
            Plan = planToDelete;
            PriceInDollars = planToDelete.PriceInDollars;
            PopulateBillingIntervalOptions();
            return Page();
        }
    }

    private void PopulateBillingIntervalOptions()
    {
        BillingIntervalOptions = new List<SelectListItem>
        {
            new SelectListItem
            {
                Value = BillingIntervals.Monthly,
                Text = BillingIntervals.Monthly.Humanize(),
                Selected = Plan?.BillingInterval == BillingIntervals.Monthly
            },
            new SelectListItem
            {
                Value = BillingIntervals.Annually,
                Text = BillingIntervals.Annually.Humanize(),
                Selected = Plan?.BillingInterval == BillingIntervals.Annually
            },
            new SelectListItem
            {
                Value = BillingIntervals.OneTime,
                Text = BillingIntervals.OneTime.Humanize(),
                Selected = Plan?.BillingInterval == BillingIntervals.OneTime
            }
        };
    }
}
