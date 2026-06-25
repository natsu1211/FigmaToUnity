using FigmaToUnity.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal static class TextLayoutUtility
    {
        public static void ApplyContentSizeFitter(FigmaNode node, TextStyle? style)
        {
            if (!node.Tags.Contains(NodeTag.HugContents) || node.GameObject == null)
            {
                return;
            }

            ContentSizeFitter fitter = node.GameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = node.GameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = style?.TextAutoResize == "WIDTH_AND_HEIGHT"
                ? ContentSizeFitter.FitMode.PreferredSize
                : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static bool UsesContentSizeFitter(FigmaNode node)
        {
            return node.Tags.Contains(NodeTag.HugContents);
        }

        public static float GetLegacyLineSpacing(TextStyle? style, float fallbackLineSpacing)
        {
            if (style?.LineHeightPx is float lineHeightPx && lineHeightPx > 0f && style.FontSize > 0f)
            {
                return lineHeightPx / style.FontSize;
            }

            return fallbackLineSpacing;
        }
    }
}
