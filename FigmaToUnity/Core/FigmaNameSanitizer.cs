using System.Text.RegularExpressions;

namespace FigmaToUnity.Core
{
    public static class FigmaNameSanitizer
    {
        // Strip only characters that are illegal in Windows/macOS asset paths plus
        // control chars. Unicode letters/digits (CJK, kana, accents) are preserved
        // so 「インゲーム画面」survives instead of collapsing to "Unnamed".
        private static readonly Regex InvalidFileNameRegex = new(
            @"[<>:""/\\|?*\x00-\x1F]+",
            RegexOptions.Compiled);

        private static readonly Regex WhitespaceRunRegex = new(
            @"\s+",
            RegexOptions.Compiled);

        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            int tagStart = value.IndexOf('#');
            string withoutTags = tagStart >= 0 ? value.Substring(0, tagStart) : value;
            string sanitized = InvalidFileNameRegex.Replace(withoutTags, " ").Trim();
            sanitized = WhitespaceRunRegex.Replace(sanitized, " ");
            sanitized = sanitized.Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(sanitized) || !HasLetterOrDigit(sanitized))
            {
                return "Unnamed";
            }

            return sanitized;
        }

        private static bool HasLetterOrDigit(string value)
        {
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
