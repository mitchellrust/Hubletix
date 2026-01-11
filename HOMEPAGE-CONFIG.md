# Homepage Content Configuration Guide

## Overview
Homepage content (feature cards, stats, services) can now be customized via tenant configuration stored in the database.

## Configuration Structure

The `TenantConfig.HomePage` property contains:

```json
{
  "HomePage": {
    "FeatureCards": [...],
    "Stats": [...],
    "Services": [...],
    "Visibility": {
      "ShowHero": true,
      "ShowAbout": true,
      "ShowServices": true
    }
  }
}
```

## Example Configuration

### Complete Example

```sql
UPDATE "TenantStore"."Tenants"
SET "ConfigJson" = '{
  "Theme": {
    "PrimaryColor": "#EF4444",
    "SecondaryColor": "#F97316",
    "LogoUrl": "/images/logo.png",
    "HeroHeading": "Welcome to Our Club",
    "HeroSubheading": "Train with the best",
    "AboutDescription": "We are a premier sports club focused on excellence."
  },
  "Features": {
    "EnableEventRegistration": true,
    "EnablePayments": true,
    "EnableMemberships": true
  },
  "HomePage": {
    "FeatureCards": [
      {
        "Title": "State-of-the-Art Facilities",
        "Description": "Modern equipment and pristine courts for optimal performance.",
        "Icon": "bi bi-building",
        "BackgroundColor": "#1F2937",
        "DisplayOrder": 1
      },
      {
        "Title": "Expert Coaching",
        "Description": "Learn from certified professionals with years of experience.",
        "Icon": "bi bi-person-badge",
        "BackgroundColor": "#3B82F6",
        "DisplayOrder": 2
      },
      {
        "Title": "Community Events",
        "Description": "Regular tournaments and social gatherings for all skill levels.",
        "Icon": "bi bi-people",
        "BackgroundColor": "#EF4444",
        "DisplayOrder": 3
      }
    ],
    "Stats": [
      {
        "Value": "500+",
        "Label": "Active Members",
        "Icon": "bi bi-people",
        "DisplayOrder": 1
      },
      {
        "Value": "95%",
        "Label": "Satisfaction Rate",
        "DisplayOrder": 2
      },
      {
        "Value": "20+",
        "Label": "Years of Excellence",
        "DisplayOrder": 3
      },
      {
        "Value": "50+",
        "Label": "Weekly Classes",
        "DisplayOrder": 4
      }
    ],
    "Services": [
      {
        "Title": "Membership Plans",
        "Subtitle": "Flexible Options",
        "Description": "Choose from monthly, quarterly, or annual memberships to fit your needs.",
        "Icon": "bi bi-card-checklist",
        "BackgroundColor": "#10B981",
        "LinkUrl": "/membershipplans",
        "LinkText": "View Plans",
        "DisplayOrder": 1
      },
      {
        "Title": "Private Lessons",
        "Subtitle": "One-on-One Training",
        "Description": "Personalized instruction tailored to your goals and skill level.",
        "Icon": "bi bi-person-video3",
        "BackgroundColor": "#8B5CF6",
        "LinkUrl": "/services/lessons",
        "LinkText": "Book Now",
        "DisplayOrder": 2
      },
      {
        "Title": "Tournament Play",
        "Subtitle": "Competitive Events",
        "Description": "Test your skills in our regular tournaments and leagues.",
        "Icon": "bi bi-trophy",
        "BackgroundColor": "#F59E0B",
        "LinkUrl": "/events",
        "LinkText": "See Schedule",
        "DisplayOrder": 3
      }
    ],
    "Visibility": {
      "ShowHero": true,
      "ShowAbout": true,
      "ShowServices": true
    }
  }
}'::jsonb
WHERE "Subdomain" = 'demo';
```

## Field Descriptions

### FeatureCard
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Title` | string | Yes | Feature card heading |
| `Description` | string | Yes | Feature card description |
| `Icon` | string | No | Bootstrap icon class (e.g., "bi bi-building") |
| `ImageUrl` | string | No | Image URL (use instead of icon) |
| `BackgroundColor` | string | No | Hex color for card background |
| `DisplayOrder` | int | Yes | Order in which cards appear (1, 2, 3...) |

### Stat
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Value` | string | Yes | Stat value (e.g., "500+", "95%") |
| `Label` | string | Yes | Stat label/description |
| `Icon` | string | No | Bootstrap icon class |
| `DisplayOrder` | int | Yes | Display order |

### ServiceCard
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Title` | string | Yes | Service name |
| `Subtitle` | string | Yes | Service tagline |
| `Description` | string | Yes | Service description |
| `Icon` | string | No | Bootstrap icon class |
| `ImageUrl` | string | No | Image URL (use instead of icon) |
| `BackgroundColor` | string | No | Hex color for card background |
| `LinkUrl` | string | No | CTA link URL |
| `LinkText` | string | No | CTA link text (defaults to "Learn more") |
| `DisplayOrder` | int | Yes | Display order |

## Adding/Updating Content

### Add a New Feature Card

```sql
UPDATE "TenantStore"."Tenants"
SET "ConfigJson" = jsonb_set(
    COALESCE("ConfigJson"::jsonb, '{}'::jsonb),
    '{HomePage,FeatureCards}',
    '[
      {
        "Title": "New Feature",
        "Description": "Description here",
        "Icon": "bi bi-star",
        "BackgroundColor": "#3B82F6",
        "DisplayOrder": 1
      }
    ]'::jsonb
)
WHERE "Subdomain" = 'demo';
```

### Update Stats

```sql
UPDATE "TenantStore"."Tenants"
SET "ConfigJson" = jsonb_set(
    COALESCE("ConfigJson"::jsonb, '{}'::jsonb),
    '{HomePage,Stats}',
    '[
      {"Value": "1000+", "Label": "Happy Members", "DisplayOrder": 1},
      {"Value": "98%", "Label": "Success Rate", "DisplayOrder": 2}
    ]'::jsonb
)
WHERE "Subdomain" = 'demo';
```

### Hide a Section

```sql
UPDATE "TenantStore"."Tenants"
SET "ConfigJson" = jsonb_set(
    COALESCE("ConfigJson"::jsonb, '{}'::jsonb),
    '{HomePage,Visibility,ShowServices}',
    'false'::jsonb
)
WHERE "Subdomain" = 'demo';
```

## Fallback Behavior

If `HomePage` configuration is null or empty, the system uses hardcoded defaults:

- **Feature Cards**: 3 default cards (Professional Facilities, Private Lessons, Pro Coaches)
- **Stats**: 4 default stats (12,000+ hours, 89% retention, 1,200+ members, 125+ tournaments)
- **Services**: 3 default services (Training Programs, Court Rental, Events & Tournaments)
- **Visibility**: All sections shown by default

This ensures the homepage always displays content even if configuration is not set.

## Tips

1. **DisplayOrder** - Use increments of 10 (10, 20, 30) to allow easy insertion of items later
2. **Icons** - Use Bootstrap Icons (https://icons.getbootstrap.com/)
3. **Colors** - Use hex format with # prefix (e.g., "#3B82F6")
4. **Images** - For ImageUrl, use relative paths (/images/...) or full URLs
5. **Links** - Use relative paths for internal links (/events, /membershipplans)

## Cache Invalidation

After updating configuration:
- **Development**: Changes appear immediately (cache disabled)
- **Production**: Call `/tenants/invalidate-cache` endpoint or wait up to 10 minutes

```bash
curl -X POST https://demo.yourdomain.com/tenants/invalidate-cache
```
