namespace ClubManagement.Api.Models;

public class EventRegistrationDto
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public required string  EventId { get; set; }
    public string? EventName { get; set; }
    public DateTime? EventStartTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SignedUpAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class EventRegistrationsTableViewModel
{
    public string Title { get; set; } = "Event Registrations";
    public string ContainerClass { get; set; } = "col";
    public string EmptyMessage { get; set; } = "No registrations found.";
    
    public List<EventRegistrationDto> Registrations { get; set; } = new();
    public int PageNum { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "date";
    public string SortDirection { get; set; } = "desc";
    public string StatusFilter { get; set; } = "all";
    
    public bool ShowEventColumn { get; set; } = true;
    public bool ShowTopFilters { get; set; } = false;
    public bool ShowSideFacets { get; set; } = false;
    public bool ShowFilterModal { get; set; } = false;
    public bool HasActiveFilters { get; set; } = false;
    
    // Additional filter properties for facets
    public string? TimeFilter { get; set; }
    
    // Route building parameters
    public string PageName { get; set; } = "";
    public Dictionary<string, string> RouteValues { get; set; } = new();
    
    public string BuildRouteUrl(string status)
    {
        var values = new Dictionary<string, string>(RouteValues)
        {
            ["sort"] = SortField,
            ["dir"] = SortDirection
        };
        
        if (RouteValues.ContainsKey("id"))
        {
            values["regStatus"] = status;
        }
        else
        {
            values["status"] = status;
        }
        
        return BuildUrl(values);
    }
    
    public string BuildSortUrl(string field)
    {
        var newDirection = (SortField == field && SortDirection == "asc") ? "desc" : "asc";
        var values = new Dictionary<string, string>(RouteValues)
        {
            ["sort"] = field,
            ["dir"] = newDirection
        };
        
        if (RouteValues.ContainsKey("id"))
        {
            values["regStatus"] = StatusFilter;
        }
        else
        {
            values["status"] = StatusFilter;
        }
        
        return BuildUrl(values);
    }
    
    public string BuildPageUrl(int pageNum)
    {
        var values = new Dictionary<string, string>(RouteValues)
        {
            ["pageNum"] = pageNum.ToString(),
            ["pageSize"] = PageSize.ToString(),
            ["sort"] = SortField,
            ["dir"] = SortDirection
        };
        
        if (RouteValues.ContainsKey("id"))
        {
            values["regStatus"] = StatusFilter;
        }
        else
        {
            values["status"] = StatusFilter;
        }
        
        return BuildUrl(values);
    }
    
    private string BuildUrl(Dictionary<string, string> values)
    {
        var queryString = string.Join("&", values
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return string.IsNullOrEmpty(queryString) ? PageName : $"{PageName}?{queryString}";
    }
}
