using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.UguiPipeline;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class TextApplier
    {
        private readonly TmpTextApplier _tmpTextApplier = new();
        private readonly LegacyTextApplier _legacyTextApplier = new();

        public static void ClearCaches()
        {
            TmpTextApplier.ClearCaches();
            LegacyFontResolver.ClearCaches();
        }

        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            if (FigmaImportSettings.instance.TextComponent == TextComponentKind.Legacy)
            {
                _legacyTextApplier.Apply(nodes);
                return;
            }

            _tmpTextApplier.Apply(nodes);
        }
    }
}
