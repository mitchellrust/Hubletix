# Public Homepage Documentation

## Overview
The public-facing homepage for club management sites is built with a modular, tenant-configurable architecture using Razor partial views.

## Architecture

### Page Structure
- **Index.cshtml** - Main page that orchestrates all sections
- **Index.cshtml.cs** - Page model that populates view models from tenant config

### Partial Views
Located in `/Pages/Shared/`:

1. **_Navbar.cshtml** - Site navigation with logo, menu items, and CTA button
2. **_Hero.cshtml** - Hero section with background image, heading, and call-to-action
3. **_AboutSection.cshtml** - About section with description, feature cards, and stats
4. **_ServicesSection.cshtml** - Services section with service cards

### View Models
Located in `/Models/HomePageModels.cs`:

- `HomePageViewModel` - Main container for all sections
- `NavbarViewModel` - Navbar configuration
- `HeroViewModel` - Hero section configuration
- `AboutSectionViewModel` - About section with features and stats
- `ServicesSectionViewModel` - Services section with service cards

## Multi-Tenant Configuration

### Database-Driven Settings
The homepage pulls configuration from `TenantConfig.Theme`:

```csharp
public class ThemeConfig
{
    public string? PrimaryColor { get; set; }        // Brand primary color
    public string? SecondaryColor { get; set; }      // Brand secondary color
    public string? LogoUrl { get; set; }             // Logo image URL
    public string? HeroImageUrl { get; set; }        // Hero background image
    public string? HeroHeading { get; set; }         // Custom hero heading
    public string? HeroSubheading { get; set; }      // Custom hero subheading
    public string? AboutDescription { get; set; }    // About section text
}
```

### Section Visibility
Control which sections display via `HomePageViewModel` flags:
- `ShowHero`
- `ShowAbout`
- `ShowServices`

These can be extended to pull from database config in the future.

## Customization

### Adding New Sections
1. Create view model class in `HomePageModels.cs`
2. Create partial view in `/Pages/Shared/`
3. Add property to `HomePageViewModel`
4. Populate in `Index.cshtml.cs` OnGet method
5. Add `<partial>` tag to `Index.cshtml`

### Styling
- Global styles in `/wwwroot/css/site.css`
- Tenant-specific colors injected via inline styles
- Bootstrap 5 for responsive layout
- Bootstrap Icons for iconography

### Example: Adding a Testimonials Section

**1. View Model:**
```csharp
public class TestimonialsSectionViewModel
{
    public string Heading { get; set; } = "What Our Members Say";
    public List<Testimonial> Testimonials { get; set; } = new();
}

public class Testimonial
{
    public string Quote { get; set; }
    public string AuthorName { get; set; }
    public string? AuthorImage { get; set; }
}
```

**2. Partial View:** `_TestimonialsSection.cshtml`

**3. Update HomePage Model:**
```csharp
public class HomePageViewModel
{
    // ... existing properties
    public TestimonialsSectionViewModel Testimonials { get; set; } = new();
    public bool ShowTestimonials { get; set; } = true;
}
```

**4. Populate in Index.cshtml.cs:**
```csharp
HomePage.Testimonials = new TestimonialsSectionViewModel
{
    Heading = "What Our Members Say",
    Testimonials = new List<Testimonial> { /* ... */ }
};
```

**5. Render in Index.cshtml:**
```html
@if (Model.HomePage.ShowTestimonials)
{
    <section id="testimonials">
        <partial name="_TestimonialsSection" model="Model.HomePage.Testimonials" />
    </section>
}
```

## Future Enhancements

### Database-Driven Content
- Store feature cards, stats, and services in database tables
- Allow admins to manage content via admin panel
- Support for multiple languages/localization

### Section Ordering
- Add `DisplayOrder` property to control section sequence
- Store in tenant config or database

### Dynamic Section Loading
- Store section configurations in JSON in database
- Render sections dynamically based on config
- Allow drag-and-drop section ordering in admin panel

### Enhanced Media Support
- Image upload functionality
- Video backgrounds for hero
- Image galleries for services/features

## Tenant-Specific Examples

### Tennis Club
```json
{
  "theme": {
    "primaryColor": "#2D5F3F",
    "logoUrl": "/images/tennis-logo.png",
    "heroHeading": "Unleash Your Inner Champion Today",
    "heroImageUrl": "/images/tennis-hero.jpg"
  }
}
```

### Yoga Studio
```json
{
  "theme": {
    "primaryColor": "#8B7355",
    "logoUrl": "/images/yoga-logo.png",
    "heroHeading": "Find Your Balance, Transform Your Life",
    "heroImageUrl": "/images/yoga-hero.jpg"
  }
}
```

## Notes
- All sections are responsive (mobile-first design)
- Smooth scroll enabled for anchor links
- Card hover effects for better UX
- Accessibility features included (alt text, ARIA labels)
