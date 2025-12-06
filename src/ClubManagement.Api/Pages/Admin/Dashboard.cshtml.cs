using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClubManagement.Api.Pages.Admin;

public class DashboardModel : PageModel
{
    public List<UpcomingEventDto> UpcomingEvents { get; set; } = new();

    public void OnGet()
    {
        // Placeholder data - will be replaced with database queries
        UpcomingEvents = new List<UpcomingEventDto>
        {
            new UpcomingEventDto
            {
                Id = "1",
                Name = "Stone and Chalk Melbourne, Docklands",
                Date = DateTime.Now.AddDays(3),
                Time = "7:30pm",
                Location = "Docklands, VIC",
                Registrations = 45
            },
            new UpcomingEventDto
            {
                Id = "2",
                Name = "All Hands Meetup with Stephan Livera",
                Date = DateTime.Now.AddDays(10),
                Time = "6:00pm",
                Location = "Docklands, VIC",
                Registrations = 82
            },
            new UpcomingEventDto
            {
                Id = "3",
                Name = "Introduction to Blockchain Workshop",
                Date = DateTime.Now.AddDays(17),
                Time = "6:00pm",
                Location = "Docklands, VIC",
                Registrations = 124
            },
            new UpcomingEventDto
            {
                Id = "4",
                Name = "Founder Meetup with Zedoop Co-founder Sandeep Goenka",
                Date = DateTime.Now.AddDays(25),
                Time = "7:00pm",
                Location = "Docklands, VIC",
                Registrations = 56
            },
            new UpcomingEventDto
            {
                Id = "5",
                Name = "5 Steps To Growth: Lessons from TransferWise",
                Date = DateTime.Now.AddDays(31),
                Time = "6:30pm",
                Location = "Docklands, VIC",
                Registrations = 93
            }
        };
    }
}

/// <summary>
/// DTO for displaying upcoming events on the dashboard.
/// </summary>
public class UpcomingEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Registrations { get; set; }
}
