using System.Globalization;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor.UitkPipeline
{
    // Pure functions that turn DesignNode-shaped values into USS literals.
    // No FigmaNode / Unity asset access — keeps the emitter testable.
    internal static class UitkStyleMapper
    {
        public static string ToCssColor(FigmaColor color, float? paintOpacity = null)
        {
            float a = color.A * (paintOpacity ?? 1f);
            int r = ToByte(color.R);
            int g = ToByte(color.G);
            int b = ToByte(color.B);
            return $"rgba({r}, {g}, {b}, {a.ToString("0.###", CultureInfo.InvariantCulture)})";
        }

        public static string ToPx(float value)
        {
            // F3 guarantees fixed-point output regardless of magnitude — the "0.###"
            // custom format has been observed to flip to scientific notation for very
            // large floats (Figma occasionally serializes "pill" corner radii as 1e7+
            // sentinel values), and USS rejects e-notation lengths outright.
            string formatted = value.ToString("F3", CultureInfo.InvariantCulture);
            int dot = formatted.IndexOf('.');
            if (dot >= 0)
            {
                int end = formatted.Length - 1;
                while (end > dot && formatted[end] == '0')
                {
                    end--;
                }

                if (end == dot)
                {
                    end--;
                }

                formatted = formatted.Substring(0, end + 1);
            }

            return formatted + "px";
        }

        public static string ToOpacity(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string? ToFlexDirection(string? figmaLayoutMode)
        {
            return figmaLayoutMode switch
            {
                "HORIZONTAL" => "row",
                "VERTICAL" => "column",
                _ => null,
            };
        }

        public static string? ToJustifyContent(string? figmaPrimaryAxisAlign)
        {
            return figmaPrimaryAxisAlign switch
            {
                "MIN" => "flex-start",
                "CENTER" => "center",
                "MAX" => "flex-end",
                "SPACE_BETWEEN" => "space-between",
                _ => null,
            };
        }

        public static string? ToAlignItems(string? figmaCounterAxisAlign)
        {
            return figmaCounterAxisAlign switch
            {
                "MIN" => "flex-start",
                "CENTER" => "center",
                "MAX" => "flex-end",
                "BASELINE" => "flex-start", // USS has no baseline align; treat as start.
                _ => null,
            };
        }

        public static string? ToTextAnchor(string? horizontal, string? vertical)
        {
            string h = horizontal switch
            {
                "LEFT" => "left",
                "CENTER" => "center",
                "RIGHT" => "right",
                _ => "left",
            };
            string v = vertical switch
            {
                "TOP" => "upper",
                "CENTER" => "middle",
                "BOTTOM" => "lower",
                _ => "upper",
            };
            return $"{v}-{h}";
        }

        // Figma node IDs look like "1:23". USS '#' selectors disallow ':' — translate.
        public static string ToUssId(string figmaNodeId)
        {
            return "fig-" + figmaNodeId.Replace(':', '-').Replace(';', '-');
        }

        private static int ToByte(float channel)
        {
            int v = (int)(channel * 255f + 0.5f);
            if (v < 0)
            {
                return 0;
            }

            if (v > 255)
            {
                return 255;
            }

            return v;
        }
    }
}
