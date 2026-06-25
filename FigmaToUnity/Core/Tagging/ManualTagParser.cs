using System;
using System.Collections.Generic;

namespace FigmaToUnity.Core
{
    public static class ManualTagParser
    {
        private static readonly Dictionary<string, NodeTag> TagMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["prefab"] = NodeTag.Prefab,
            ["image"] = NodeTag.Image,
            ["slice9"] = NodeTag.NinePatch,
            ["sliced"] = NodeTag.NinePatch,
            ["ninepatch"] = NodeTag.NinePatch,
            ["container"] = NodeTag.Container,
            ["button"] = NodeTag.Button,
            ["scroll"] = NodeTag.Scroll,
            ["ignore"] = NodeTag.Ignore,
            ["mask"] = NodeTag.Mask,
        };

        // Keyword for value-bearing "use an existing prefab" tags, e.g.
        // "MyButton #use:Assets/UI/Btn.prefab" or "MyButton #use:Btn".
        private static readonly char[] TokenSeparators = { ' ', ',', ';' };
        private const string UsePrefix = "use:";

        public static IReadOnlyList<NodeTag> Parse(string nodeName)
        {
            List<NodeTag> result = new();
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return result;
            }

            string[] parts = nodeName.Split('#');
            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i].Trim().Split(TokenSeparators)[0];
                if (TagMap.TryGetValue(token, out NodeTag tag))
                {
                    result.Add(tag);
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts the prefab reference from a <c>#use:&lt;path-or-name&gt;</c> tag in the
        /// layer name. The value is everything after <c>#use:</c> up to the next whitespace,
        /// comma or semicolon, so it may be an asset path (containing <c>/</c>) or a bare name.
        /// Returns false when no such tag is present.
        /// </summary>
        public static bool TryParseExternalPrefab(string nodeName, out string reference)
        {
            reference = string.Empty;
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return false;
            }

            string[] parts = nodeName.Split('#');
            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (!token.StartsWith(UsePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = token.Substring(UsePrefix.Length);
                int end = value.IndexOfAny(TokenSeparators);
                if (end >= 0)
                {
                    value = value.Substring(0, end);
                }

                value = value.Trim();
                if (value.Length > 0)
                {
                    reference = value;
                    return true;
                }
            }

            return false;
        }
    }
}
