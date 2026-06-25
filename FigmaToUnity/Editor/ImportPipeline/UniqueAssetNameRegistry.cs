using System;
using System.Collections.Generic;

namespace FigmaToUnity.Editor.ImportPipeline
{
    // Scoped per import pass. The first node to claim a given base name gets it
    // verbatim; subsequent collisions are suffixed `-1`, `-2`, ... so two
    // unrelated Figma nodes that happen to share a name don't fight over the
    // same asset path. Comparison is case-insensitive because macOS/Windows
    // asset paths are case-insensitive.
    internal sealed class UniqueAssetNameRegistry
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public string GetUnique(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Unnamed";
            }

            if (_counts.TryGetValue(baseName, out int n))
            {
                _counts[baseName] = n + 1;
                return $"{baseName}-{n + 1}";
            }

            _counts[baseName] = 0;
            return baseName;
        }
    }
}
