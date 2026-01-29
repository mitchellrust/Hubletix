namespace Hubletix.Api.Utils;

/// <summary>
/// Extension methods for working with timezones.
/// </summary>
public static class TimeZoneExtensions
{
    /// <summary>
    /// Converts a UTC DateTime to the specified timezone.
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to convert.</param>
    /// <param name="timeZoneId">The IANA timezone identifier (e.g., "America/Denver").</param>
    /// <returns>The DateTime in the specified timezone.</returns>
    public static DateTime ToTimeZone(this DateTime utcDateTime, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
    }

    /// <summary>
    /// Gets the abbreviated timezone name (e.g., "MST" for "Mountain Standard Time").
    /// </summary>
    /// <param name="timeZoneId">The IANA timezone identifier (e.g., "America/Denver").</param>
    /// <param name="dateTime">The DateTime to check for daylight saving time.</param>
    /// <returns>The abbreviated timezone name.</returns>
    public static string GetAbbreviation(this string timeZoneId, DateTime dateTime)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var tzName = timeZone.IsDaylightSavingTime(dateTime)
            ? timeZone.DaylightName
            : timeZone.StandardName;

        // Extract first letters of each word (e.g., "Mountain Standard Time" -> "MST")
        return string.Concat(tzName.Split(' ').Select(word => word[0]));
    }

    /// <summary>
    /// Gets the abbreviated timezone name based on UTC DateTime.
    /// Converts to local time first to determine DST status.
    /// </summary>
    /// <param name="timeZoneId">The IANA timezone identifier (e.g., "America/Denver").</param>
    /// <param name="utcDateTime">The UTC DateTime.</param>
    /// <returns>The abbreviated timezone name.</returns>
    public static string GetAbbreviationFromUtc(this string timeZoneId, DateTime utcDateTime)
    {
        var localDateTime = utcDateTime.ToTimeZone(timeZoneId);
        return timeZoneId.GetAbbreviation(localDateTime);
    }
}
