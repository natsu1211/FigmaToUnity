using System.Text.RegularExpressions;

namespace FigmaToUnity.Core
{
    public static class FigmaUrlParser
    {
        private static readonly Regex FileUrlRegex = new(
            @"figma\.com/(file|design)/(?<key>[A-Za-z0-9]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParseFileKey(string url, out string fileKey)
        {
            fileKey = string.Empty;

            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            Match match = FileUrlRegex.Match(url);
            if (!match.Success)
            {
                return false;
            }

            fileKey = match.Groups["key"].Value;
            return !string.IsNullOrWhiteSpace(fileKey);
        }
    }
}
