namespace ClubManagement.Api.Models;

public class EventDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public string? LocationDetails { get; set; }
    public int Registrations { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
    public bool IsHappening { get; set; }
}

public class EventsTableViewModel
{
    public string Title { get; set; } = "Events";
    public string ContainerClass { get; set; } = "row";
    public string EmptyMessage { get; set; } = "No events found. Create your first event to get started.";
    
    public List<EventDto> Events { get; set; } = new();
    public int PageNum { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "date";
    public string SortDirection { get; set; } = "desc";
    public string StatusFilter { get; set; } = "all"; // all, active, inactive
    public string DateFilter { get; set; } = "upcoming"; // all, upcoming, past
    
    public bool ShowFilterFacets { get; set; } = true;
    public bool HasActiveFilters => (StatusFilter != "all") || (DateFilter != "all");
    
    // Route building parameters
    public string PageName { get; set; } = "/admin/events";
    public Dictionary<string, string> RouteValues { get; set; } = new();
    
    public string BuildStatusUrl(string status)
    {
        var values = new Dictionary<string, string>
        {
            ["status"] = status,
            ["date"] = DateFilter,
            ["sort"] = SortField,
            ["dir"] = SortDirection
        };
        
        return BuildUrl(values);
    }
    
    public string BuildDateUrl(string date)
    {
        var values = new Dictionary<string, string>
        {
            ["status"] = StatusFilter,
            ["date"] = date,
            ["sort"] = SortField,
            ["dir"] = SortDirection
        };
        
        return BuildUrl(values);
    }
    
    public string BuildSortUrl(string field)
    {
        var newDirection = (SortField == field && SortDirection == "asc") ? "desc" : "asc";
        var values = new Dictionary<string, string>
        {
            ["status"] = StatusFilter,
            ["date"] = DateFilter,
            ["sort"] = field,
            ["dir"] = newDirection
        };
        
        return BuildUrl(values);
    }
    
    public string BuildPageUrl(int pageNum)
    {
        var values = new Dictionary<string, string>
        {
            ["pageNum"] = pageNum.ToString(),
            ["pageSize"] = PageSize.ToString(),
            ["status"] = StatusFilter,
            ["date"] = DateFilter,
            ["sort"] = SortField,
            ["dir"] = SortDirection
        };
        
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
