using System.Text.RegularExpressions;

namespace NarratorSvc.Voice
{
    internal static class Bg2TextNormalizer
    {
        private static readonly Regex ColorBlock = new Regex(
            @"\^0x[0-9a-fA-F]+\s*(.*?)\^-\s*",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            string text = rawText;
            var matches = ColorBlock.Matches(text);
            if (matches.Count > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (Match match in matches)
                {
                    string segment = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(segment))
                    {
                        parts.Add(segment);
                    }
                }

                if (parts.Count > 1)
                {
                    text = string.Join(" ", parts.GetRange(1, parts.Count - 1));
                }
                else if (parts.Count == 1)
                {
                    text = parts[0];
                }
                else
                {
                    text = ColorBlock.Replace(text, " ");
                }
            }
            else
            {
                text = text.Replace("^-", " ");
            }

            text = Whitespace.Replace(text, " ");
            return text.Trim();
        }
    }
}
