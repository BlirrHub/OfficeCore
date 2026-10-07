using System.Globalization;
using System.Text.RegularExpressions;

namespace OfficeCore.Api.Services;

public static class TextHelper
{
    /// <summary>
    /// Converts a name or string into Proper Case (Title Case).
    /// Handles multiple spaces, hyphens, and common name prefixes.
    /// Example: "john doe" -> "John Doe", "MARY-JANE" -> "Mary-Jane", "de la cruz" -> "De La Cruz"
    /// </summary>
    public static string ToProperCase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Normalize multiple whitespaces into a single space
        var trimmed = Regex.Replace(input.Trim(), @"\s+", " ");

        // Convert all characters to lowercase first, then apply Culture TitleCase
        var titleCased = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());

        // Handle hyphenated names (e.g. Mary-jane -> Mary-Jane)
        titleCased = Regex.Replace(titleCased, @"\b([a-zA-Z])", m => m.Value.ToUpperInvariant());

        // Handle apostrophes in names (e.g. O'connor -> O'Connor, D'angelo -> D'Angelo)
        titleCased = Regex.Replace(titleCased, @"(\b[A-Za-z]')([a-z])", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());

        // Handle Irish/Scottish Mc prefix (e.g. Mcdonald -> McDonald)
        titleCased = Regex.Replace(titleCased, @"\bMc([a-z])", m => "Mc" + m.Groups[1].Value.ToUpperInvariant());

        return titleCased;
    }
}
