using System.Linq;
using System.Text;
using UnityEngine;


public class StringHelper
{
    // The core filtering function
    public static string FilterAsterisks(string input)
    {
        // Prevent NullReferenceException if the string is empty or null
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        
        // Replaces all occurrences of '*' with an empty string
        return input.Replace("*", "").Replace("#", "");
    }

    // The core filtering function
    public static string FilterNewline(string input)
    {
        // Prevent NullReferenceException if the string is empty or null
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        
        // Replaces all occurrences of '*' with an empty string
        return input.Replace("\\n", "").Replace("\\r", "").Replace("\n", "").Replace("\\", "").Replace("  ", " ");
    }

    public static string RemoveNonVisibleChars(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Filters out control chars, non-breaking spaces, zero-width chars, and general white spaces
        return new string(input.Where(c => !char.IsControl(c) 
                                        && c != '\u200B'   // Zero-width space
                                        && c != '\uFEFF')  // BOM / Zero-width no-break space
                               .ToArray());
    }
}