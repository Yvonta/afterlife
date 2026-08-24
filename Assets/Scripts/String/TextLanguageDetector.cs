using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class TextLanguageDetector
{
    private Dictionary<string, HashSet<string>> languageStopwords = new Dictionary<string, HashSet<string>>();

    public TextLanguageDetector()
    {
        InitializeStopwords();
    }

    private void InitializeStopwords()
    {
        // English
        languageStopwords.Add("en", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "the", "and", "that", "have", "for", "not", "with", "you", "this", "but", "his", "from", "they" });

        // Spanish
        languageStopwords.Add("es", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "el", "la", "los", "las", "un", "una", "por", "para", "con", "que", "como", "mas", "pero", "este" });

        // French
        languageStopwords.Add("fr", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "le", "la", "les", "une", "des", "pour", "dans", "avec", "est", "que", "pas", "plus", "sur", "par" });
            
        // German
        languageStopwords.Add("de", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "der", "die", "das", "und", "ist", "zu", "den", "von", "mit", "auf", "für", "ein", "eine", "im", "nicht" });
    
        // Dutch
        languageStopwords.Add("nl", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "het", "een", "van", "ik", "te", "dat", "op", "met", "voor", "niet", "zijn", "op", "om", "ook" });

        // Tagalog / Filipino
        languageStopwords.Add("tl", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "ang", "mga", "sa", "ng", "na", "at", "ako", "ikaw", "siya", "kami", "tayo", "sila", "ito", "iyon", "dito", "doon", "hindi", "din", "rin", "para", "dahil" });
    }

    public string DetectLanguage(string text, string defaultFallback = "en")
    {
        if (string.IsNullOrWhiteSpace(text)) return defaultFallback;

        // Clean text preserving Unicode characters (accents, umlauts, diacritics)
        string cleanText = Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\s]", "");
        
        string[] words = cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return defaultFallback;

        // Quick character heuristic bonuses
        Dictionary<string, float> languageScores = new Dictionary<string, float>();
        foreach (string lang in languageStopwords.Keys)
        {
            languageScores[lang] = 0f;
        }

        // Apply character features (boost score for unique characters)
        if (Regex.IsMatch(cleanText, @"[ßäöü]")) languageScores["de"] += 2.0f;
        if (Regex.IsMatch(cleanText, @"[ij]")) languageScores["nl"] += 0.5f;
        if (Regex.IsMatch(cleanText, @"[áéíóú]")) languageScores["es"] += 1.5f;
        if (Regex.IsMatch(cleanText, @"[àâçéèêëîïôûùüÿœæ]")) languageScores["fr"] += 1.5f;
        
        // Both Spanish and Tagalog share 'ñ', so assign partial weights or use standalone 'ng' as a Tagalog indicator
        if (Regex.IsMatch(cleanText, @"[ñ]")) 
        {
            languageScores["es"] += 1.0f;
            languageScores["tl"] += 1.0f;
        }
        if (Regex.IsMatch(cleanText, @"\bng\b")) languageScores["tl"] += 1.5f;

        // Match stopwords
        foreach (string word in words)
        {
            foreach (var kvp in languageStopwords)
            {
                if (kvp.Value.Contains(word))
                {
                    languageScores[kvp.Key] += 1.0f;
                }
            }
        }

        string bestLanguage = defaultFallback;
        float highestScore = 0f;

        foreach (var score in languageScores)
        {
            if (score.Value > highestScore)
            {
                highestScore = score.Value;
                bestLanguage = score.Key;
            }
        }

        return highestScore > 0f ? bestLanguage : defaultFallback;
    }
}