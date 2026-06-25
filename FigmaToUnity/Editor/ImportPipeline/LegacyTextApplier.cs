using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.UguiPipeline;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class LegacyTextApplier
    {
        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            FigmaImportSettings settings = FigmaImportSettings.instance;
            List<string> bestFitConflictNodes = new();

            foreach (FigmaNode node in nodes)
            {
                if (!node.Tags.Contains(NodeTag.Text) || node.GameObject == null)
                {
                    continue;
                }

                RemoveTmpTextComponent(node.GameObject);

                Text text = node.GameObject.GetComponent<Text>();
                if (text == null)
                {
                    text = node.GameObject.AddComponent<Text>();
                }

                TextStyle? effectiveStyle = TextStyleUtility.GetEffectiveTextStyle(node);
                text.text = ApplyTextCase(node.Characters ?? string.Empty, effectiveStyle?.TextCase);
                text.fontSize = Mathf.RoundToInt(effectiveStyle?.FontSize > 0f ? effectiveStyle.FontSize : 14f);
                text.color = TextStyleUtility.GetTextColor(node);
                text.raycastTarget = false;
                text.alignment = GetAlignment(effectiveStyle);
                text.fontStyle = GetFontStyle(effectiveStyle);
                text.font = LegacyFontResolver.ResolveFont(effectiveStyle);
                LegacyFontResolver.WarnIfFontSubstituted(node, effectiveStyle, text.font);
                text.horizontalOverflow = settings.LegacyHorizontalWrapMode;
                text.verticalOverflow = settings.LegacyVerticalWrapMode;
                text.lineSpacing = TextLayoutUtility.GetLegacyLineSpacing(effectiveStyle, settings.LegacyLineSpacing);

                bool usesContentSizeFitter = TextLayoutUtility.UsesContentSizeFitter(node);
                bool enableBestFit = settings.LegacyBestFit && !usesContentSizeFitter;
                if (settings.LegacyBestFit && usesContentSizeFitter)
                {
                    bestFitConflictNodes.Add($"'{node.Name}' ({node.Id})");
                }

                text.resizeTextForBestFit = enableBestFit;
                if (enableBestFit)
                {
                    text.resizeTextMinSize = 1;
                    text.resizeTextMaxSize = Mathf.Max(1, text.fontSize);
                }

                WarnForUnsupportedLegacyStyling(node, effectiveStyle);
                TextLayoutUtility.ApplyContentSizeFitter(node, effectiveStyle);
            }

            if (bestFitConflictNodes.Count > 0)
            {
                string sample = bestFitConflictNodes[0];
                string summary = bestFitConflictNodes.Count == 1
                    ? sample
                    : $"{sample} and {bestFitConflictNodes.Count - 1} more";
                Debug.LogWarning($"[FigmaImporter] Legacy Best Fit was disabled for {bestFitConflictNodes.Count} text node(s) because they also use ContentSizeFitter. Example: {summary}.");
            }
        }

        private static TextAnchor GetAlignment(TextStyle? style)
        {
            string h = style?.TextAlignHorizontal ?? "LEFT";
            string v = style?.TextAlignVertical ?? "TOP";

            return (v, h) switch
            {
                ("CENTER", "CENTER") => TextAnchor.MiddleCenter,
                ("CENTER", "RIGHT") => TextAnchor.MiddleRight,
                ("CENTER", _) => TextAnchor.MiddleLeft,
                ("BOTTOM", "CENTER") => TextAnchor.LowerCenter,
                ("BOTTOM", "RIGHT") => TextAnchor.LowerRight,
                ("BOTTOM", _) => TextAnchor.LowerLeft,
                (_, "CENTER") => TextAnchor.UpperCenter,
                (_, "RIGHT") => TextAnchor.UpperRight,
                _ => TextAnchor.UpperLeft,
            };
        }

        private static FontStyle GetFontStyle(TextStyle? style)
        {
            bool isBold = style?.FontWeight >= 700;
            bool isItalic = style?.Italic == true;

            if (isBold && isItalic)
            {
                return FontStyle.BoldAndItalic;
            }

            if (isBold)
            {
                return FontStyle.Bold;
            }

            if (isItalic)
            {
                return FontStyle.Italic;
            }

            return FontStyle.Normal;
        }

        private static string ApplyTextCase(string value, string? textCase)
        {
            return textCase switch
            {
                "UPPER" => value.ToUpperInvariant(),
                "LOWER" => value.ToLowerInvariant(),
                _ => value,
            };
        }

        private static void WarnForUnsupportedLegacyStyling(FigmaNode node, TextStyle? style)
        {
            if (style?.LetterSpacing is float letterSpacing && Mathf.Abs(letterSpacing) > 0.001f)
            {
                Debug.LogWarning($"[FigmaImporter] Legacy Text does not support letter spacing for '{node.Name}' ({node.Id}).");
            }

            if (style?.TextDecoration is "UNDERLINE" or "STRIKETHROUGH")
            {
                Debug.LogWarning($"[FigmaImporter] Legacy Text does not support text decoration '{style.TextDecoration}' for '{node.Name}' ({node.Id}).");
            }

            if (style?.TextCase == "SMALL_CAPS")
            {
                Debug.LogWarning($"[FigmaImporter] Legacy Text does not apply text case '{style.TextCase}' for '{node.Name}' ({node.Id}).");
            }
        }

        private static void RemoveTmpTextComponent(GameObject gameObject)
        {
            TextMeshProUGUI tmpText = gameObject.GetComponent<TextMeshProUGUI>();
            if (tmpText != null)
            {
                Object.DestroyImmediate(tmpText);
            }
        }
    }
}
