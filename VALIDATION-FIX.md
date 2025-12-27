# Validation Scripts Fix

## Problem
The `CreateAccount.cshtml` and `SetupOrganization.cshtml` pages were using `<partial name="_ValidationScriptsPartial" />` which wasn't working, resulting in no client-side validation on the signup forms.

## Solution
Replaced the partial validation scripts with inline JavaScript validation, following the same pattern used in the `Login.cshtml` page.

## Changes Made

### 1. CreateAccount.cshtml
**Removed:**
- `<partial name="_ValidationScriptsPartial" />`
- Duplicate Tailwind CSS content (kept only Bootstrap 5 version)

**Added:**
- Password confirmation validation
- Terms acceptance validation
- Basic password strength check (minimum 8 characters)
- Real-time password match indicator with Bootstrap validation classes

### 2. SetupOrganization.cshtml
**Removed:**
- `<partial name="_ValidationScriptsPartial" />`
- Duplicate Tailwind CSS content (kept only Bootstrap 5 version)

**Added:**
- Subdomain format validation (lowercase letters, numbers, hyphens only)
- Real-time subdomain sanitization (removes invalid characters as you type)
- Subdomain length validation (3-63 characters)
- Organization name required validation
- Bootstrap validation classes for visual feedback

## Features

### CreateAccount Page Validation
1. **Password Match**: Validates that password and confirm password fields match before submission
2. **Terms Acceptance**: Ensures user has checked the Terms of Service checkbox
3. **Password Length**: Validates minimum 8 characters
4. **Real-time Feedback**: Shows `is-invalid` class on confirm password field if it doesn't match

### SetupOrganization Page Validation
1. **Subdomain Sanitization**: Automatically converts to lowercase and removes invalid characters
2. **Format Enforcement**: Only allows `[a-z0-9-]` characters
3. **No Leading/Trailing Hyphens**: Automatically removes them
4. **No Consecutive Hyphens**: Replaces `--` with `-`
5. **Length Validation**: 3-63 characters (DNS subdomain standards)
6. **Required Field Validation**: Organization name and subdomain

## Testing
Both pages now have working client-side validation that:
- Prevents form submission when validation fails
- Shows user-friendly alert messages
- Provides real-time feedback using Bootstrap validation classes
- Works consistently with the existing Bootstrap 5 styling

## Technical Details
- Uses vanilla JavaScript (no jQuery dependency)
- Uses optional chaining (`?.`) for safe DOM access
- Follows same pattern as `Login.cshtml` for consistency
- Integrates with Bootstrap 5 validation classes (`is-invalid`)
