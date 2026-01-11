using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Hubletix.Core.Constants;
using Hubletix.Core.Entities;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Utils;
using Hubletix.Infrastructure.Services;

namespace Hubletix.Api.Pages.Tenant.Admin;

public class CreatePlanModel : TenantAdminPageModel
{
    [BindProperty]
    public MembershipPlan Plan { get; set; } = new MembershipPlan();

    [BindProperty]
    public decimal PriceInDollars { get; set; }

    public List<SelectListItem> BillingIntervalOptions { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public CreatePlanModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    { }

    public IActionResult OnGet()
    {
        // Initialize with default values
        Plan.BillingInterval = BillingIntervals.Monthly;
        Plan.IsActive = true;
        Plan.DisplayOrder = 0;
        PriceInDollars = 0;

        PopulateBillingIntervalOptions();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(Plan.Name))
        {
            ErrorMessage = "Plan name is required.";
            PopulateBillingIntervalOptions();
            return Page();
        }

        // Validate price is positive
        if (PriceInDollars <= 0)
        {
            ErrorMessage = "Price must be greater than zero.";
            PopulateBillingIntervalOptions();
            return Page();
        }

        // Convert dollars to cents with proper rounding
        Plan.PriceInCents = (int)Math.Round(PriceInDollars * 100, MidpointRounding.AwayFromZero);

        // Set tenant ID
        Plan.TenantId = CurrentTenantInfo.Id;
        Plan.Id = Guid.NewGuid().ToString();

        try
        {
            DbContext.MembershipPlans.Add(Plan);
            await DbContext.SaveChangesAsync();
            return RedirectToPage("/Admin/Plans", new { message = "Plan created successfully." });
        }
        catch (Exception)
        {
            ErrorMessage = $"Uh-Oh! Something went wrong.";
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
