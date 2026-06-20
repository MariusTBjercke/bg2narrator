using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NarratorSvc.Voice
{
    internal static class PstTextNormalizer
    {
        private static readonly Regex QuotedSegments = new Regex(
            @"""([^""]*)""",
            RegexOptions.Compiled);
        private static readonly Regex DebugPrefix = new Regex(
            @"^\^G\[PST\]\^-\s*",
            RegexOptions.Compiled);

        private static readonly Regex SpokenLine = new Regex(
            @"^\^0x[0-9a-fA-F]{8}([A-Za-z][^%^]*)\^-\s*-\s*\^0x[0-9a-fA-F]{8}(.+?)(?:\^-)?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex SpokenLineCompact = new Regex(
            @"^\^0x[0-9a-fA-F]{8}([A-Za-z][^%^]*)\^-\s*\^0x[0-9a-fA-F]{8}(.+?)(?:\^-)?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex SpokenLineShort = new Regex(
            @"^\^0x[0-9a-fA-F]{2}([A-Za-z][^%^]*)\^-\s*-\s*\^0x[0-9a-fA-F]+(.+?)(?:\^-)?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex SpokenLineShortCompact = new Regex(
            @"^\^0x[0-9a-fA-F]{2}([A-Za-z][^%^]*)\^-\s*\^0x[0-9a-fA-F]+(.+?)(?:\^-)?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex SpokenLinePlainBody = new Regex(
            @"^\^0x[0-9a-fA-F]{2,8}([A-Za-z][^%^]*)\^-\s*-\s*(.+?)(?:\^-)?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex NarrationOpener = new Regex(
            @"^(You see |You notice |You hear |You watch |The [a-z]+ |A [a-z]+ |An [a-z]+ )",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParseSpeakLine(string rawText, out string speaker, out string speakText)
        {
            speaker = null;
            speakText = null;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return false;
            }

            string text = DebugPrefix.Replace(rawText, string.Empty).Trim();
            Match match = SpokenLine.Match(text);
            if (!match.Success)
            {
                match = SpokenLineCompact.Match(text);
            }
            if (!match.Success)
            {
                match = SpokenLineShort.Match(text);
            }
            if (!match.Success)
            {
                match = SpokenLineShortCompact.Match(text);
            }
            if (!match.Success)
            {
                match = SpokenLinePlainBody.Match(text);
            }
            if (!match.Success)
            {
                return false;
            }

            speaker = match.Groups[1].Value.Trim();
            speakText = match.Groups[2].Value.Trim();
            speakText = speakText.Replace("^-", string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(speakText))
            {
                return false;
            }

            if (IsPlayerSpeaker(speaker))
            {
                return false;
            }

            if (IsFarewellLine(speakText))
            {
                return false;
            }

            if (NarrationOpener.IsMatch(speakText))
            {
                return false;
            }

            return true;
        }

        public static string ExtractQuotedDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var parts = new List<string>();
            foreach (Match match in QuotedSegments.Matches(text))
            {
                string segment = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(segment))
                {
                    parts.Add(segment);
                }
            }

            if (parts.Count == 0)
            {
                return null;
            }

            return string.Join(" ", parts);
        }

        private static bool IsPlayerSpeaker(string speaker)
        {
            return string.Equals(speaker, "Nameless One", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(speaker, "The Nameless One", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFarewellLine(string text)
        {
            string trimmed = text.Trim().TrimEnd('.');
            return string.Equals(trimmed, "Leave", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Goodbye", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Farewell", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
