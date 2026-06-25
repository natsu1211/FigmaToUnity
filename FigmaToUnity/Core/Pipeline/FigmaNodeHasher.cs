using System;
using System.Collections.Generic;
using System.Linq;

namespace FigmaToUnity.Core
{
    public static class FigmaNodeHasher
    {
        public static void ComputeHashes(IReadOnlyList<DesignNode> rootNodes, HashOptions options)
        {
            foreach (DesignNode rootNode in rootNodes)
            {
                ComputeHashesRecursive(rootNode, options);
            }
        }

        public static int ComputeHash(DesignNode node, HashOptions options)
        {
            DeterministicHash hash = new();
            hash.Add(node.Type);
            hash.Add(node.ComponentId);
            hash.Add(node.LayoutMode);
            hash.Add(node.LayoutWrap);
            hash.Add(node.LayoutPositioning);
            hash.Add(node.LayoutSizingHorizontal);
            hash.Add(node.LayoutSizingVertical);
            hash.Add(node.PrimaryAxisSizingMode);
            hash.Add(node.CounterAxisSizingMode);
            hash.Add(node.PrimaryAxisAlignItems);
            hash.Add(node.CounterAxisAlignItems);
            hash.Add(node.CounterAxisAlignContent);
            hash.Add(node.LayoutAlign);
            hash.Add(node.OverflowDirection);
            hash.Add(node.StrokeAlign);
            hash.Add(node.Visible);
            hash.Add(node.Opacity);
            hash.Add(node.Rotation);
            hash.Add(node.StrokeWeight);
            hash.Add(node.CornerRadius);
            hash.Add(node.ItemSpacing);
            hash.Add(node.CounterAxisSpacing);
            hash.Add(node.PaddingLeft);
            hash.Add(node.PaddingRight);
            hash.Add(node.PaddingTop);
            hash.Add(node.PaddingBottom);
            hash.Add(node.LayoutGrow);
            hash.Add(node.MinWidth);
            hash.Add(node.MaxWidth);
            hash.Add(node.MinHeight);
            hash.Add(node.MaxHeight);
            hash.Add(node.ClipsContent);
            hash.Add(node.IsMask);
            hash.Add(node.PreserveRatio);
            hash.Add(node.ForceImage);
            hash.Add(node.ForceContainer);
            hash.Add(node.IgnoreNode);

            if (node.AbsoluteBoundingBox != null)
            {
                hash.Add(node.AbsoluteBoundingBox.X);
                hash.Add(node.AbsoluteBoundingBox.Y);
                hash.Add(node.AbsoluteBoundingBox.Width);
                hash.Add(node.AbsoluteBoundingBox.Height);
            }

            if (node.AbsoluteRenderBounds != null)
            {
                hash.Add(node.AbsoluteRenderBounds.X);
                hash.Add(node.AbsoluteRenderBounds.Y);
                hash.Add(node.AbsoluteRenderBounds.Width);
                hash.Add(node.AbsoluteRenderBounds.Height);
            }

            if (node.Constraints != null)
            {
                hash.Add(node.Constraints.Horizontal);
                hash.Add(node.Constraints.Vertical);
            }

            if (node.RectangleCornerRadii != null)
            {
                foreach (float cornerRadius in node.RectangleCornerRadii)
                {
                    hash.Add(cornerRadius);
                }
            }

            if (node.Characters != null)
            {
                hash.Add(node.Characters);
            }

            if (node.Style != null)
            {
                AppendTextStyle(ref hash, node.Style);
            }

            if (node.StyleOverrideTable != null)
            {
                foreach (KeyValuePair<string, TextStyle> pair in node.StyleOverrideTable.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    hash.Add(pair.Key);
                    AppendTextStyle(ref hash, pair.Value);
                }
            }

            if (node.CharacterStyleOverrides != null)
            {
                foreach (int styleIndex in node.CharacterStyleOverrides)
                {
                    hash.Add(styleIndex);
                }
            }

            if (node.Fills != null)
            {
                foreach (Paint fill in node.Fills)
                {
                    AppendPaint(ref hash, fill);
                }
            }

            if (node.Strokes != null)
            {
                foreach (Paint stroke in node.Strokes)
                {
                    AppendPaint(ref hash, stroke);
                }
            }

            if (node.Effects != null)
            {
                foreach (Effect effect in node.Effects)
                {
                    hash.Add(effect.Type);
                    hash.Add(effect.Visible);
                }
            }

            // Order tags deterministically so unordered HashSet iteration cannot destabilize the hash.
            foreach (NodeTag tag in node.Tags.OrderBy(t => (int)t))
            {
                hash.Add((int)tag);
            }

            if (node.Tags.Contains(NodeTag.Image))
            {
                hash.Add(options.ProceduralImageFalloff);
            }

            if (node.Children != null)
            {
                foreach (DesignNode child in node.Children)
                {
                    hash.Add(child.Id);
                }
            }

            return hash.ToHashCode();
        }

        private static void AppendTextStyle(ref DeterministicHash hash, TextStyle style)
        {
            hash.Add(style.FontFamily);
            hash.Add(style.FontWeight);
            hash.Add(style.FontSize);
            hash.Add(style.TextAlignHorizontal);
            hash.Add(style.TextAlignVertical);
            hash.Add(style.LetterSpacing);
            hash.Add(style.LineHeightPx);
            hash.Add(style.LineHeightPercentFontSize);
            hash.Add(style.TextAutoResize);
            hash.Add(style.TextCase);
            hash.Add(style.TextDecoration);
            hash.Add(style.Italic);
        }

        private static void AppendPaint(ref DeterministicHash hash, Paint paint)
        {
            hash.Add(paint.Type);
            hash.Add(paint.ScaleMode);
            hash.Add(paint.ImageRef);
            hash.Add(paint.Visible);
            hash.Add(paint.Opacity);
            if (paint.Color != null)
            {
                hash.Add(paint.Color.R);
                hash.Add(paint.Color.G);
                hash.Add(paint.Color.B);
                hash.Add(paint.Color.A);
            }

            if (paint.GradientStops != null)
            {
                foreach (GradientStop stop in paint.GradientStops)
                {
                    hash.Add(stop.Position);
                    if (stop.Color == null)
                    {
                        continue;
                    }

                    hash.Add(stop.Color.R);
                    hash.Add(stop.Color.G);
                    hash.Add(stop.Color.B);
                    hash.Add(stop.Color.A);
                }
            }
        }

        private static void ComputeHashesRecursive(DesignNode node, HashOptions options)
        {
            node.NodeHash = ComputeHash(node, options);
            if (node.Children == null)
            {
                return;
            }

            foreach (DesignNode child in node.Children)
            {
                ComputeHashesRecursive(child, options);
            }
        }
    }
}
