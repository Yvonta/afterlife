using System.Text.RegularExpressions;

public static class TextSanitizer
{
    public static string CleanForTTS(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Skip internal control signals
        if (input == "[PARAGRAPH_BREAK]") return string.Empty;

        // Remove Emojis and non-printable Unicode characters
        string cleaned = Regex.Replace(input, @"\p{Cs}|\p{So}|\p{Cn}", "");

        // Trim remaining whitespace and structural symbols
        return cleaned.Trim();
    }
}