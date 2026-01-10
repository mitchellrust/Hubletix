using System.Text.RegularExpressions;
using System.Globalization;

namespace Hubletix.Api.Utils;

public static class StringExtensions
{
    /// <summary>
    /// Takes a poorly formatted string and makes it more human-readable, typically for display purposes.
    /// Handles camelCase, PascalCase, and snake_case formatting.
    /// </summary>
    /// <returns>A human-readable version of the input string</returns>
    public static string Humanize(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Handle snake_case: replace underscores with spaces
        string result = input.Replace("_", " ");

        // Handle camelCase and PascalCase: insert spaces before uppercase letters
        result = Regex.Replace(result, "([a-z])([A-Z])", "$1 $2");
        result = Regex.Replace(result, "([A-Z]+)([A-Z][a-z])", "$1 $2");

        // Capitalize the first letter of each word
        result = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower());

        return result.Trim();
    }
}