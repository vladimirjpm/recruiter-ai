using System.Security.Cryptography;
using System.Text;

namespace RecruiterAi.Infrastructure.Logging;

/// <summary>
/// Safe logging helpers — personal data must never appear in log output.
///
/// Prohibited in logs: CV raw_text, email, phone, full prompts, OpenAI responses,
/// API keys, connection strings.
///
/// Usage:
///   _logger.LogInformation("CV parsed. {Fp}", PiiSafe.Fingerprint(rawText));
///   _logger.LogInformation("Candidate: {Email}", PiiSafe.MaskEmail(email));
/// </summary>
public static class PiiSafe
{
    /// <summary>
    /// Masks an email address, exposing only the first character and the domain.
    /// "john.doe@example.com" → "j***@example.com"
    /// Returns null for null/empty input.
    /// </summary>
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var at = email.IndexOf('@');
        if (at <= 0) return "***";

        return $"{email[0]}***{email[at..]}";
    }

    /// <summary>
    /// Returns a stable fingerprint (length + 6-char SHA256 prefix) instead of the text.
    /// Identical inputs always produce identical fingerprints; different inputs almost never collide.
    ///
    /// SHA256 is used instead of GetHashCode() because GetHashCode() is randomised per process
    /// in .NET, making cross-run correlation impossible.
    ///
    /// "Hello, world!" → "len=13,h=315f5b"
    /// null / ""       → "len=0,h=empty"
    /// </summary>
    public static string Fingerprint(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "len=0,h=empty";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        // 3 bytes → 6 hex chars — short enough to scan in logs, long enough to be distinct
        var shortHash = Convert.ToHexString(bytes[..3]).ToLowerInvariant();
        return $"len={value.Length},h={shortHash}";
    }

    /// <summary>
    /// Masks a phone number, keeping only the last 4 digits.
    /// "+7 (999) 123-45-67" → "***4567"
    /// Returns null for null/empty input.
    /// </summary>
    public static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = phone.Where(char.IsDigit).ToArray();
        if (digits.Length == 0) return "***";

        var tail = new string(digits[^Math.Min(4, digits.Length)..]);
        return $"***{tail}";
    }
}
