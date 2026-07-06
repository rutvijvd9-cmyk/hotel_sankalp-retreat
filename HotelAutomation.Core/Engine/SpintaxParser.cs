namespace HotelAutomation.Core.Engine;

using System;
using System.Text.RegularExpressions;

public static class SpintaxParser
{
    private static readonly Regex SpintaxRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);
    private static readonly Random Random = new();

    /// <summary>
    /// Parses Spintax formatting. Example: "{Hi|Hello|Hey} {Name}" -> "Hi {Name}"
    /// Supports nested spintax by looping until no more matches are found.
    /// </summary>
    public static string Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var result = input;
        while (SpintaxRegex.IsMatch(result))
        {
            result = SpintaxRegex.Replace(result, match =>
            {
                var options = match.Groups[1].Value.Split('|');
                var selected = options[Random.Next(options.Length)];
                return selected;
            });
        }

        return result;
    }
}
