using System;
using System.Linq;

namespace HotelAutomation.Core.Helpers;

public static class PhoneHelper
{
    /// <summary>
    /// Normalizes a phone number to standard E.164 format with country code (e.g. +919527891289).
    /// Always includes the '+' sign and '+91' for 10-digit or 91-prefixed Indian numbers.
    /// </summary>
    public static string NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Keep digits and '+'
        string cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) return string.Empty;

        if (cleaned.StartsWith('+'))
        {
            return cleaned;
        }

        // Indian 10-digit mobile numbers
        if (cleaned.Length == 10)
        {
            return "+91" + cleaned;
        }

        // Indian 11-digit numbers with leading 0 (e.g. 09527891289)
        if (cleaned.Length == 11 && cleaned.StartsWith("0"))
        {
            return "+91" + cleaned.Substring(1);
        }

        // Indian 12-digit numbers starting with 91 (e.g. 919527891289)
        if (cleaned.Length == 12 && cleaned.StartsWith("91"))
        {
            return "+" + cleaned;
        }

        return "+" + cleaned;
    }
}
