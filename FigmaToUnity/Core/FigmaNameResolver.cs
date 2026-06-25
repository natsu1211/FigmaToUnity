using System;
using System.Collections.Generic;
using System.Text;

namespace FigmaToUnity.Core
{
    public static class FigmaNameResolver
    {
        // Common Figma defaults that the designer never renamed. Treated as
        // placeholders so we substitute a more useful name (type+index, text
        // content, or ancestor path) instead of leaking them to GameObjects.
        private static readonly HashSet<string> PlaceholderBases = new(StringComparer.OrdinalIgnoreCase)
        {
            "Frame", "Group", "Rectangle", "Ellipse", "Line", "Vector", "Star", "Polygon",
            "Component", "Instance", "Text", "Slice", "Image", "Unnamed", "new"
        };

        public static bool IsPlaceholderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            string trimmed = name!.Trim();

            bool hasLetter = false;
            foreach (char c in trimmed)
            {
                if (char.IsLetter(c))
                {
                    hasLetter = true;
                    break;
                }
            }

            if (!hasLetter)
            {
                return true;
            }

            string baseName = StripCounterSuffix(trimmed);
            return PlaceholderBases.Contains(baseName);
        }

        public static string TypeLabel(string? type)
        {
            return type switch
            {
                "FRAME" => "Frame",
                "GROUP" => "Group",
                "RECTANGLE" => "Rectangle",
                "ELLIPSE" => "Ellipse",
                "LINE" => "Line",
                "VECTOR" => "Vector",
                "STAR" => "Star",
                "REGULAR_POLYGON" => "Polygon",
                "TEXT" => "Text",
                "COMPONENT" => "Component",
                "COMPONENT_SET" => "ComponentSet",
                "INSTANCE" => "Instance",
                "BOOLEAN_OPERATION" => "Boolean",
                "SLICE" => "Slice",
                _ => string.IsNullOrEmpty(type) ? "Node" : type!,
            };
        }

        public static string TextSummary(string? characters, int maxWords = 4, int maxLen = 30)
        {
            if (string.IsNullOrWhiteSpace(characters))
            {
                return string.Empty;
            }

            string trimmed = characters!.Trim();
            StringBuilder builder = new();
            int wordCount = 0;
            int i = 0;

            while (i < trimmed.Length && wordCount < maxWords)
            {
                while (i < trimmed.Length && char.IsWhiteSpace(trimmed[i]))
                {
                    i++;
                }

                if (i >= trimmed.Length)
                {
                    break;
                }

                int start = i;
                while (i < trimmed.Length && !char.IsWhiteSpace(trimmed[i]))
                {
                    i++;
                }

                int len = i - start;
                int separator = builder.Length > 0 ? 1 : 0;
                if (builder.Length + separator + len > maxLen)
                {
                    break;
                }

                if (separator > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(trimmed, start, len);
                wordCount++;
            }

            return builder.ToString();
        }

        private static string StripCounterSuffix(string value)
        {
            string current = value;
            while (true)
            {
                int copyIdx = LastIndexOfStandaloneWord(current, "copy");
                if (copyIdx > 0)
                {
                    string after = current.Substring(copyIdx + 4).TrimStart();
                    if (after.Length == 0 || IsAllDigits(after))
                    {
                        current = current.Substring(0, copyIdx).TrimEnd();
                        continue;
                    }
                }

                int spaceIdx = current.LastIndexOf(' ');
                if (spaceIdx > 0 && IsAllDigits(current.Substring(spaceIdx + 1)))
                {
                    current = current.Substring(0, spaceIdx);
                    continue;
                }

                return current;
            }
        }

        private static int LastIndexOfStandaloneWord(string s, string word)
        {
            int idx = s.LastIndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (idx <= 0)
            {
                return -1;
            }

            if (s[idx - 1] != ' ')
            {
                return -1;
            }

            return idx;
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
