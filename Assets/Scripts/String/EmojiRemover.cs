using System.Text.RegularExpressions;
using UnityEngine;

public class EmojiRemover
{
    public static string RemoveEmojis(string input)
    {
        if (string.IsNullOrEmpty(input)) 
            return input;

        // Matches surrogate pairs and common emoji Unicode ranges
        string pattern = @"[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2600-\u27BF]";
        
        return Regex.Replace(input, pattern, string.Empty);
    }
}