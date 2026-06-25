using System;
using FigmaToUnity.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal static class ImageStyleSupport
    {
        public static Color GetNodeColor(FigmaNode node)
        {
            if (node.Fills != null)
            {
                foreach (Paint fill in node.Fills)
                {
                    if (fill.Visible == false || fill.Color == null)
                    {
                        if (TryGetGradientColor(fill, out Color gradientColor))
                        {
                            return gradientColor;
                        }

                        continue;
                    }

                    float alpha = fill.Opacity ?? fill.Color.A;
                    return new Color(fill.Color.R, fill.Color.G, fill.Color.B, alpha);
                }
            }

            return new Color(1f, 1f, 1f, 0.15f);
        }

        public static bool TryGetStroke(FigmaNode node, out Color strokeColor, out float strokeWidth)
        {
            strokeColor = Color.clear;
            strokeWidth = 0f;

            if (!node.StrokeWeight.HasValue || node.StrokeWeight.Value <= 0f || node.Strokes == null)
            {
                return false;
            }

            foreach (Paint stroke in node.Strokes)
            {
                if (stroke.Visible == false || stroke.Color == null)
                {
                    continue;
                }

                if (!string.Equals(stroke.Type, "SOLID", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                float alpha = stroke.Opacity ?? stroke.Color.A;
                strokeColor = new Color(stroke.Color.R, stroke.Color.G, stroke.Color.B, alpha);
                strokeWidth = Mathf.Max(0f, node.StrokeWeight.Value);
                return true;
            }

            return false;
        }

        public static bool TryGetCornerRadii(FigmaNode node, out Vector4 cornerRadii)
        {
            cornerRadii = Vector4.zero;

            if (node.RectangleCornerRadii != null && node.RectangleCornerRadii.Count >= 4)
            {
                cornerRadii = new Vector4(
                    Mathf.Max(0f, node.RectangleCornerRadii[0]),
                    Mathf.Max(0f, node.RectangleCornerRadii[1]),
                    Mathf.Max(0f, node.RectangleCornerRadii[2]),
                    Mathf.Max(0f, node.RectangleCornerRadii[3]));
                return cornerRadii != Vector4.zero;
            }

            if (node.CornerRadius.HasValue && node.CornerRadius.Value > 0f)
            {
                float radius = Mathf.Max(0f, node.CornerRadius.Value);
                cornerRadii = new Vector4(radius, radius, radius, radius);
                return true;
            }

            return false;
        }

        public static Image.Type GetImageType(FigmaNode node)
        {
            if (node.Tags.Contains(NodeTag.NinePatch))
            {
                return Image.Type.Sliced;
            }

            Paint? imageFill = GetPrimaryImageFill(node);
            if (imageFill != null && string.Equals(imageFill.ScaleMode, "TILE", StringComparison.OrdinalIgnoreCase))
            {
                return Image.Type.Tiled;
            }

            return Image.Type.Simple;
        }

        public static bool ShouldPreserveAspect(FigmaNode node, Image.Type imageType)
        {
            if (imageType == Image.Type.Sliced || imageType == Image.Type.Tiled)
            {
                return false;
            }

            if (node.Tags.Contains(NodeTag.AspectRatio))
            {
                return true;
            }

            Paint? imageFill = GetPrimaryImageFill(node);
            if (imageFill == null)
            {
                return node.Sprite != null;
            }

            return string.Equals(imageFill.ScaleMode, "FIT", StringComparison.OrdinalIgnoreCase) || node.Sprite != null;
        }

        public static Paint? GetPrimaryImageFill(FigmaNode node)
        {
            if (node.Fills == null)
            {
                return null;
            }

            foreach (Paint fill in node.Fills)
            {
                if (fill.Visible == false)
                {
                    continue;
                }

                if (string.Equals(fill.Type, "IMAGE", StringComparison.OrdinalIgnoreCase))
                {
                    return fill;
                }
            }

            return null;
        }

        private static bool TryGetGradientColor(Paint fill, out Color color)
        {
            color = Color.clear;
            if (fill.GradientStops == null || fill.GradientStops.Count == 0)
            {
                return false;
            }

            float totalWeight = 0f;
            float r = 0f;
            float g = 0f;
            float b = 0f;
            float a = 0f;

            foreach (GradientStop stop in fill.GradientStops)
            {
                if (stop.Color == null)
                {
                    continue;
                }

                totalWeight += 1f;
                r += stop.Color.R;
                g += stop.Color.G;
                b += stop.Color.B;
                a += stop.Color.A;
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            float opacity = fill.Opacity ?? 1f;
            color = new Color(r / totalWeight, g / totalWeight, b / totalWeight, (a / totalWeight) * opacity);
            return true;
        }
    }
}
