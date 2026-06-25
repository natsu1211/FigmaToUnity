using System;
using System.Collections.Generic;

namespace FigmaToUnity.Core
{
    public enum UitkElementKind
    {
        VisualElement,
        Label,
        ScrollView
    }

    public readonly struct UitkSliceBorder
    {
        public UitkSliceBorder(int left, int bottom, int right, int top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public int Left { get; }

        public int Bottom { get; }

        public int Right { get; }

        public int Top { get; }
    }

    public readonly struct UitkElementBox
    {
        public UitkElementBox(float width, float height, float leftOffset, float topOffset)
        {
            Width = width;
            Height = height;
            LeftOffset = leftOffset;
            TopOffset = topOffset;
        }

        public float Width { get; }

        public float Height { get; }

        public float LeftOffset { get; }

        public float TopOffset { get; }
    }

    public static class UitkRenderPolicy
    {
        public static bool ShouldEmitElement(DesignNode node)
        {
            return !node.Tags.Contains(NodeTag.Mask);
        }

        public static UitkElementKind ResolveElementKind(ISet<NodeTag> tags)
        {
            if (tags.Contains(NodeTag.Text))
            {
                return UitkElementKind.Label;
            }

            if (tags.Contains(NodeTag.Scroll))
            {
                return UitkElementKind.ScrollView;
            }

            return UitkElementKind.VisualElement;
        }

        public static UitkElementKind ResolveElementKind(DesignNode node)
        {
            if (ShouldRenderTextAsRasterImage(node))
            {
                return UitkElementKind.VisualElement;
            }

            return ResolveElementKind(node.Tags);
        }

        public static bool ShouldRenderTextAsRasterImage(DesignNode node)
        {
            if (!node.Tags.Contains(NodeTag.Text))
            {
                return false;
            }

            return HasVisiblePaint(node.Strokes) || HasUnsupportedTextFill(node) || HasVisibleEffect(node);
        }

        public static bool ShouldEmitRasterBackgroundImage(DesignNode node, bool hasResolvedSprite)
        {
            if (!ShouldEmitElement(node))
            {
                return false;
            }

            return hasResolvedSprite &&
                   (ShouldRenderTextAsRasterImage(node) ||
                    node.Tags.Contains(NodeTag.NinePatch) ||
                    HasVisibleImagePaint(node));
        }

        public static string? ResolveRasterBackgroundScaleMode(DesignNode node)
        {
            if (ShouldRenderTextAsRasterImage(node))
            {
                return "stretch-to-fill";
            }

            if (node.Tags.Contains(NodeTag.NinePatch) || HasVisibleImagePaint(node))
            {
                return "stretch-to-fill";
            }

            return null;
        }

        public static bool ShouldSuppressSolidBackground(DesignNode node, bool hasResolvedSprite)
        {
            return ShouldEmitRasterBackgroundImage(node, hasResolvedSprite);
        }

        public static bool ShouldEmitStrokeBorder(DesignNode node)
        {
            return !node.Tags.Contains(NodeTag.Text);
        }

        public static string ResolveTextWhiteSpace(DesignNode node)
        {
            if (!node.Tags.Contains(NodeTag.Text))
            {
                return "normal";
            }

            if (!string.IsNullOrEmpty(node.Characters) &&
                (node.Characters.Contains("\n") || node.Characters.Contains("\r")))
            {
                return "normal";
            }

            if (HasMultiLineTextBox(node))
            {
                return "normal";
            }

            if (string.Equals(node.Style?.TextAutoResize, "WIDTH_AND_HEIGHT", StringComparison.OrdinalIgnoreCase))
            {
                return "nowrap";
            }

            return "normal";
        }

        public static UitkElementBox ResolveElementBox(
            DesignNode node,
            float? spritePixelWidth,
            float? spritePixelHeight,
            float spriteScale)
        {
            BoundingBox? box = node.AbsoluteBoundingBox;
            if (box == null)
            {
                return new UitkElementBox(0f, 0f, 0f, 0f);
            }

            if (!ShouldRenderTextAsRasterImage(node) ||
                !spritePixelWidth.HasValue ||
                !spritePixelHeight.HasValue ||
                spritePixelWidth.Value <= 0f ||
                spritePixelHeight.Value <= 0f)
            {
                return ResolveDefaultElementBox(node, box);
            }

            float scale = Math.Max(0.01f, spriteScale);
            float width = spritePixelWidth.Value / scale;
            float height = spritePixelHeight.Value / scale;
            float left = ResolveHorizontalOffset(box.Width, width, node.Style?.TextAlignHorizontal);
            float top = ResolveVerticalOffset(box.Height, height, node.Style?.TextAlignVertical);
            return new UitkElementBox(width, height, left, top);
        }

        private static UitkElementBox ResolveDefaultElementBox(DesignNode node, BoundingBox box)
        {
            if (!IsAutoLayout(node) || node.Children == null || node.Children.Count == 0)
            {
                return new UitkElementBox(box.Width, box.Height, 0f, 0f);
            }

            float width = box.Width;
            float height = box.Height;
            if (string.Equals(node.LayoutMode, "HORIZONTAL", StringComparison.OrdinalIgnoreCase))
            {
                float contentWidth = SumAutoLayoutChildWidths(node);
                float contentHeight = MaxAutoLayoutChildHeight(node);
                width = Math.Max(width, contentWidth + PaddingHorizontal(node));
                height = Math.Max(height, contentHeight + PaddingVertical(node));
            }
            else if (string.Equals(node.LayoutMode, "VERTICAL", StringComparison.OrdinalIgnoreCase))
            {
                float contentWidth = MaxAutoLayoutChildWidth(node);
                float contentHeight = SumAutoLayoutChildHeights(node);
                width = Math.Max(width, contentWidth + PaddingHorizontal(node));
                height = Math.Max(height, contentHeight + PaddingVertical(node));
            }

            return new UitkElementBox(width, height, 0f, 0f);
        }

        public static bool ShouldPositionAbsolutely(DesignNode node, DesignNode? parent)
        {
            if (parent?.Tags.Contains(NodeTag.Scroll) == true)
            {
                return false;
            }

            if (parent == null)
            {
                return true;
            }

            if (string.Equals(node.LayoutPositioning, "ABSOLUTE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !IsAutoLayout(parent);
        }

        public static bool ShouldParticipateInAutoLayout(DesignNode node, DesignNode? parent)
        {
            return IsAutoLayout(parent) &&
                   !string.Equals(node.LayoutPositioning, "ABSOLUTE", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldClipContent(DesignNode node)
        {
            if (node.Tags.Contains(NodeTag.ClipBounds) || node.Tags.Contains(NodeTag.Mask))
            {
                return true;
            }

            if (node.Children == null)
            {
                return false;
            }

            foreach (DesignNode child in node.Children)
            {
                if (child.Tags.Contains(NodeTag.Mask))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldEmitStyleSliceBorder(DesignNode node, bool hasImportedSpriteBorder)
        {
            if (!node.Tags.Contains(NodeTag.NinePatch))
            {
                return false;
            }

            // Generated UITK sprites are Figma-sized snapshots. Style slices
            // resample rounded artwork and can sharpen pill/gauge ends; when a
            // caller intentionally imports a borderless sprite, keep USS from
            // adding a fallback slice border.
            return false;
        }

        public static UitkSliceBorder ResolveNinePatchSliceBorder(DesignNode node, float spriteScale)
        {
            if (node.Children == null || node.Children.Count != 9 || node.AbsoluteBoundingBox == null)
            {
                return new UitkSliceBorder(0, 0, 0, 0);
            }

            BoundingBox? topLeft = node.Children[0].AbsoluteBoundingBox;
            BoundingBox? topRight = node.Children[2].AbsoluteBoundingBox;
            BoundingBox? bottomLeft = node.Children[6].AbsoluteBoundingBox;
            if (topLeft == null || topRight == null || bottomLeft == null)
            {
                return new UitkSliceBorder(0, 0, 0, 0);
            }

            float scale = Math.Max(0.01f, spriteScale);
            return NormalizeSliceBorder(
                node.AbsoluteBoundingBox.Width * scale,
                node.AbsoluteBoundingBox.Height * scale,
                topLeft.Width * scale,
                bottomLeft.Height * scale,
                topRight.Width * scale,
                topLeft.Height * scale);
        }

        public static UitkSliceBorder NormalizeSliceBorder(
            float width,
            float height,
            float left,
            float bottom,
            float right,
            float top)
        {
            int maxHorizontal = Math.Max(0, (int)Math.Floor(Math.Max(0f, width) * 0.5f));
            int maxVertical = Math.Max(0, (int)Math.Floor(Math.Max(0f, height) * 0.5f));

            int normalizedLeft = ClampToInt(left, maxHorizontal);
            int normalizedBottom = ClampToInt(bottom, maxVertical);
            int normalizedRight = ClampToInt(right, maxHorizontal);
            int normalizedTop = ClampToInt(top, maxVertical);

            bool horizontalDegenerate = normalizedLeft + normalizedRight >= width;
            bool verticalDegenerate = normalizedBottom + normalizedTop >= height;
            if (horizontalDegenerate && verticalDegenerate)
            {
                return new UitkSliceBorder(0, 0, 0, 0);
            }

            return new UitkSliceBorder(
                normalizedLeft,
                normalizedBottom,
                normalizedRight,
                normalizedTop);
        }

        private static bool IsAutoLayout(DesignNode? node)
        {
            return node?.LayoutMode is "HORIZONTAL" or "VERTICAL";
        }

        private static float SumAutoLayoutChildWidths(DesignNode node)
        {
            float sum = 0f;
            int count = 0;
            foreach (DesignNode child in node.Children ?? new List<DesignNode>())
            {
                if (!ShouldParticipateInAutoLayout(child, node) || child.AbsoluteBoundingBox == null)
                {
                    continue;
                }

                sum += child.AbsoluteBoundingBox.Width;
                count++;
            }

            return sum + Math.Max(0, count - 1) * Math.Max(0f, node.ItemSpacing ?? 0f);
        }

        private static float SumAutoLayoutChildHeights(DesignNode node)
        {
            float sum = 0f;
            int count = 0;
            foreach (DesignNode child in node.Children ?? new List<DesignNode>())
            {
                if (!ShouldParticipateInAutoLayout(child, node) || child.AbsoluteBoundingBox == null)
                {
                    continue;
                }

                sum += child.AbsoluteBoundingBox.Height;
                count++;
            }

            return sum + Math.Max(0, count - 1) * Math.Max(0f, node.ItemSpacing ?? 0f);
        }

        private static float MaxAutoLayoutChildWidth(DesignNode node)
        {
            float max = 0f;
            foreach (DesignNode child in node.Children ?? new List<DesignNode>())
            {
                if (ShouldParticipateInAutoLayout(child, node) && child.AbsoluteBoundingBox != null)
                {
                    max = Math.Max(max, child.AbsoluteBoundingBox.Width);
                }
            }

            return max;
        }

        private static float MaxAutoLayoutChildHeight(DesignNode node)
        {
            float max = 0f;
            foreach (DesignNode child in node.Children ?? new List<DesignNode>())
            {
                if (ShouldParticipateInAutoLayout(child, node) && child.AbsoluteBoundingBox != null)
                {
                    max = Math.Max(max, child.AbsoluteBoundingBox.Height);
                }
            }

            return max;
        }

        private static float PaddingHorizontal(DesignNode node)
        {
            return Math.Max(0f, node.PaddingLeft ?? 0f) + Math.Max(0f, node.PaddingRight ?? 0f);
        }

        private static float PaddingVertical(DesignNode node)
        {
            return Math.Max(0f, node.PaddingTop ?? 0f) + Math.Max(0f, node.PaddingBottom ?? 0f);
        }

        private static bool HasMultiLineTextBox(DesignNode node)
        {
            if (node.AbsoluteBoundingBox == null || node.Style == null || node.Style.FontSize <= 0f)
            {
                return false;
            }

            float lineHeight = node.Style.LineHeightPx ?? node.Style.FontSize;
            return node.AbsoluteBoundingBox.Height >= lineHeight * 1.8f;
        }

        private static float ResolveHorizontalOffset(float containerWidth, float contentWidth, string? align)
        {
            if (string.Equals(align, "RIGHT", StringComparison.OrdinalIgnoreCase))
            {
                return containerWidth - contentWidth;
            }

            if (string.Equals(align, "CENTER", StringComparison.OrdinalIgnoreCase))
            {
                return (containerWidth - contentWidth) * 0.5f;
            }

            return 0f;
        }

        private static float ResolveVerticalOffset(float containerHeight, float contentHeight, string? align)
        {
            if (string.Equals(align, "BOTTOM", StringComparison.OrdinalIgnoreCase))
            {
                return containerHeight - contentHeight;
            }

            if (string.Equals(align, "CENTER", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(align, "MIDDLE", StringComparison.OrdinalIgnoreCase))
            {
                return (containerHeight - contentHeight) * 0.5f;
            }

            return 0f;
        }

        private static bool HasVisibleImagePaint(DesignNode node)
        {
            if (node.Fills == null)
            {
                return false;
            }

            foreach (Paint fill in node.Fills)
            {
                if (fill.Visible == false)
                {
                    continue;
                }

                if (string.Equals(fill.Type, "IMAGE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasUnsupportedTextFill(DesignNode node)
        {
            if (node.Fills == null)
            {
                return false;
            }

            foreach (Paint fill in node.Fills)
            {
                if (!IsVisiblePaint(fill))
                {
                    continue;
                }

                if (!string.Equals(fill.Type, "SOLID", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasVisiblePaint(List<Paint>? paints)
        {
            if (paints == null)
            {
                return false;
            }

            foreach (Paint paint in paints)
            {
                if (IsVisiblePaint(paint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasVisibleEffect(DesignNode node)
        {
            if (node.Effects == null)
            {
                return false;
            }

            foreach (Effect effect in node.Effects)
            {
                if (effect.Visible != false)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVisiblePaint(Paint paint)
        {
            if (paint.Visible == false || paint.Opacity == 0f)
            {
                return false;
            }

            return paint.Color == null || paint.Color.A > 0f;
        }

        private static int ClampToInt(float value, int max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            int rounded = (int)Math.Round(Math.Max(0f, value), MidpointRounding.AwayFromZero);
            return Math.Min(rounded, max);
        }
    }
}
