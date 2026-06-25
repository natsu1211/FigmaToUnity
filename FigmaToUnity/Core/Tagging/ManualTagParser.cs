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
                string token = parts[i].Trim().Split(' ', ',', ';')[0];
                if (TagMap.TryGetValue(token, out NodeTag tag))
                {
                    result.Add(tag);
                }
            }

            return result;
        }
    }
}
