using System;
using System.Collections.Generic;
using FigmaToUnity.Core;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal static class TextStyleUtility
    {
        public static TextStyle? GetEffectiveTextStyle(FigmaNode node)
        {
            TextStyle? overrideStyle = GetPrimaryStyleOverride(node);
            if (overrideStyle == null)
            {
                return node.Style;
            }

            if (node.Style == null)
            {
                return overrideStyle;
            }

            return MergeTextStyles(node.Style, overrideStyle);
        }

        public static Color GetTextColor(FigmaNode node)
        {
            if (node.Fills != null)
            {
                foreach (Paint fill in node.Fills)
                {
                    if (fill.Visible == false || fill.Color == null)
                    {
                        continue;
                    }

                    float alpha = fill.Opacity ?? fill.Color.A;
                    return new Color(fill.Color.R, fill.Color.G, fill.Color.B, alpha);
                }
            }

            return Color.white;
        }

        public static string BuildRawFontKey(string? family, float? weight, bool italic, bool includeWeight, bool includeItalic)
        {
            string fullName = family ?? string.Empty;

            if (includeWeight)
            {
                fullName += $"-{FontWeightToString(weight)}";
            }

            if (includeItalic && italic)
            {
                fullName += "-Italic";
            }

            return fullName;
        }

        public static string NormalizeFontName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "null";
            }

            Dictionary<string, string> weightSynonyms = new(StringComparer.OrdinalIgnoreCase)
            {
                ["hairline"] = "thin",
                ["ultralight"] = "extralight",
                ["light"] = "light",
                ["normal"] = "regular",
                ["medium"] = "medium",
                ["demibold"] = "semibold",
                ["bold"] = "bold",
                ["ultrabold"] = "extrabold",
                ["heavy"] = "black",
                ["ultrablack"] = "extrablack",
            };

            string formatted = value!
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .ToLowerInvariant();

            bool hasWeight = false;
            foreach (KeyValuePair<string, string> pair in weightSynonyms)
            {
                if (formatted.IndexOf(pair.Key, StringComparison.Ordinal) >= 0 || formatted.IndexOf(pair.Value, StringComparison.Ordinal) >= 0)
                {
                    hasWeight = true;
                    break;
                }
            }

            bool hasItalic = formatted.IndexOf("italic", StringComparison.Ordinal) >= 0;
            if (hasWeight)
            {
                foreach (KeyValuePair<string, string> pair in weightSynonyms)
                {
                    if (formatted.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                    {
                        formatted = formatted.Replace(pair.Key, pair.Value);
                    }
                }
            }
            else if (hasItalic)
            {
                formatted = formatted.Replace("italic", "regularitalic");
            }

            return formatted;
        }

        public static string FontWeightToString(float? weight)
        {
            int roundedWeight = Mathf.RoundToInt(weight ?? 400f);
            if (roundedWeight <= 199)
            {
                return "Thin";
            }

            if (roundedWeight <= 299)
            {
                return "ExtraLight";
            }

            if (roundedWeight <= 399)
            {
                return "Light";
            }

            if (roundedWeight <= 499)
            {
                return "Regular";
            }

            if (roundedWeight <= 599)
            {
                return "Medium";
            }

            if (roundedWeight <= 699)
            {
                return "SemiBold";
            }

            if (roundedWeight <= 799)
            {
                return "Bold";
            }

            if (roundedWeight <= 899)
            {
                return "ExtraBold";
            }

            if (roundedWeight <= 999)
            {
                return "Black";
            }

            return "ExtraBlack";
        }

        private static TextStyle? GetPrimaryStyleOverride(FigmaNode node)
        {
            if (node.StyleOverrideTable == null || node.StyleOverrideTable.Count == 0)
            {
                return null;
            }

            if (node.CharacterStyleOverrides != null && node.CharacterStyleOverrides.Count > 0)
            {
                Dictionary<int, int> styleUsage = new();
                foreach (int styleIndex in node.CharacterStyleOverrides)
                {
                    if (styleIndex <= 0)
                    {
                        continue;
                    }

                    if (!styleUsage.TryAdd(styleIndex, 1))
                    {
                        styleUsage[styleIndex]++;
                    }
                }

                if (styleUsage.Count > 0)
                {
                    int dominantStyleIndex = 0;
                    int dominantStyleUsage = -1;
                    foreach (KeyValuePair<int, int> pair in styleUsage)
                    {
                        if (pair.Value > dominantStyleUsage)
                        {
                            dominantStyleIndex = pair.Key;
                            dominantStyleUsage = pair.Value;
                        }
                    }

                    string dominantKey = dominantStyleIndex.ToString();
                    if (node.StyleOverrideTable.TryGetValue(dominantKey, out TextStyle? dominantStyle) && dominantStyle != null)
                    {
                        return dominantStyle;
                    }
                }
            }

            foreach (KeyValuePair<string, TextStyle> pair in node.StyleOverrideTable)
            {
                if (pair.Value != null)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private static TextStyle MergeTextStyles(TextStyle baseStyle, TextStyle overrideStyle)
        {
            return new TextStyle
            {
                FontFamily = string.IsNullOrWhiteSpace(overrideStyle.FontFamily) ? baseStyle.FontFamily : overrideStyle.FontFamily,
                FontWeight = overrideStyle.FontWeight ?? baseStyle.FontWeight,
                FontSize = overrideStyle.FontSize > 0f ? overrideStyle.FontSize : baseStyle.FontSize,
                TextAlignHorizontal = string.IsNullOrWhiteSpace(overrideStyle.TextAlignHorizontal) ? baseStyle.TextAlignHorizontal : overrideStyle.TextAlignHorizontal,
                TextAlignVertical = string.IsNullOrWhiteSpace(overrideStyle.TextAlignVertical) ? baseStyle.TextAlignVertical : overrideStyle.TextAlignVertical,
                LetterSpacing = overrideStyle.LetterSpacing ?? baseStyle.LetterSpacing,
                LineHeightPx = overrideStyle.LineHeightPx ?? baseStyle.LineHeightPx,
                LineHeightPercentFontSize = overrideStyle.LineHeightPercentFontSize ?? baseStyle.LineHeightPercentFontSize,
                TextAutoResize = string.IsNullOrWhiteSpace(overrideStyle.TextAutoResize) ? baseStyle.TextAutoResize : overrideStyle.TextAutoResize,
                TextCase = string.IsNullOrWhiteSpace(overrideStyle.TextCase) ? baseStyle.TextCase : overrideStyle.TextCase,
                TextDecoration = string.IsNullOrWhiteSpace(overrideStyle.TextDecoration) ? baseStyle.TextDecoration : overrideStyle.TextDecoration,
                Italic = overrideStyle.Italic ?? baseStyle.Italic,
            };
        }
    }
}
