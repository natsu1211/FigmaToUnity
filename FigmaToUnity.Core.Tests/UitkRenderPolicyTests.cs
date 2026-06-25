using FigmaToUnity.Core;
using Xunit;

namespace FigmaToUnity.Core.Tests
{
    public class UitkRenderPolicyTests
    {
        [Fact]
        public void NormalizeSliceBorderKeepsPanelSlicesWhenOnlyOneAxisDegenerates()
        {
            UitkSliceBorder border = UitkRenderPolicy.NormalizeSliceBorder(
                width: 610,
                height: 120,
                left: 116,
                bottom: 116,
                right: 116,
                top: 116);

            Assert.Equal(116, border.Left);
            Assert.Equal(116, border.Right);
            Assert.Equal(60, border.Bottom);
            Assert.Equal(60, border.Top);
        }

        [Fact]
        public void NormalizeSliceBorderSuppressesSlicesWhenBothAxesDegenerate()
        {
            UitkSliceBorder border = UitkRenderPolicy.NormalizeSliceBorder(
                width: 100,
                height: 108,
                left: 96,
                bottom: 116,
                right: 96,
                top: 96);

            Assert.Equal(0, border.Left);
            Assert.Equal(0, border.Right);
            Assert.Equal(0, border.Bottom);
            Assert.Equal(0, border.Top);
        }

        [Fact]
        public void TextNodesDoNotEmitStrokeAsElementBorder()
        {
            DesignNode node = new();
            node.Tags.Add(NodeTag.Text);

            Assert.False(UitkRenderPolicy.ShouldEmitStrokeBorder(node));
        }

        [Fact]
        public void StrokedTextNodesEmitAsRasterImages()
        {
            DesignNode node = new();
            node.Tags.Add(NodeTag.Text);
            node.Strokes = new()
            {
                new Paint
                {
                    Type = "SOLID",
                    Color = new FigmaColor { R = 1f, G = 1f, B = 1f, A = 1f },
                },
            };

            Assert.True(UitkRenderPolicy.ShouldRenderTextAsRasterImage(node));
            Assert.True(UitkRenderPolicy.ShouldEmitRasterBackgroundImage(node, hasResolvedSprite: true));
            Assert.Equal(UitkElementKind.VisualElement, UitkRenderPolicy.ResolveElementKind(node));
            Assert.Equal("stretch-to-fill", UitkRenderPolicy.ResolveRasterBackgroundScaleMode(node));
        }

        [Fact]
        public void FixedWidthTextUsesNormalWhiteSpace()
        {
            DesignNode node = new()
            {
                Characters = "Matters\nUnless otherwise prohibited by applicable law, Content may not be refunded.",
                Style = new TextStyle
                {
                    TextAutoResize = "HEIGHT",
                },
            };
            node.Tags.Add(NodeTag.Text);

            Assert.Equal("normal", UitkRenderPolicy.ResolveTextWhiteSpace(node));
        }

        [Fact]
        public void AutoWidthTextUsesNoWrap()
        {
            DesignNode node = new()
            {
                Characters = "9999",
                Style = new TextStyle
                {
                    TextAutoResize = "WIDTH_AND_HEIGHT",
                },
            };
            node.Tags.Add(NodeTag.Text);

            Assert.Equal("nowrap", UitkRenderPolicy.ResolveTextWhiteSpace(node));
        }

        [Fact]
        public void TallSingleLineTextBoxUsesNormalWhiteSpace()
        {
            DesignNode node = new()
            {
                Characters = "You will lose any rewards obtained during play.",
                AbsoluteBoundingBox = new BoundingBox { Width = 600, Height = 130 },
                Style = new TextStyle
                {
                    FontSize = 40,
                    TextAutoResize = "WIDTH_AND_HEIGHT",
                },
            };
            node.Tags.Add(NodeTag.Text);

            Assert.Equal("normal", UitkRenderPolicy.ResolveTextWhiteSpace(node));
        }

        [Fact]
        public void RasterTextUsesNaturalSpriteSizeCenteredInFigmaTextBox()
        {
            DesignNode node = new()
            {
                AbsoluteBoundingBox = new BoundingBox { X = 0, Y = 0, Width = 750, Height = 150 },
                Style = new TextStyle
                {
                    TextAlignHorizontal = "CENTER",
                    TextAlignVertical = "CENTER",
                },
            };
            node.Tags.Add(NodeTag.Text);
            node.Strokes = new()
            {
                new Paint
                {
                    Type = "SOLID",
                    Color = new FigmaColor { R = 1f, G = 1f, B = 1f, A = 1f },
                },
            };

            UitkElementBox box = UitkRenderPolicy.ResolveElementBox(
                node,
                spritePixelWidth: 965,
                spritePixelHeight: 172,
                spriteScale: 2);

            Assert.Equal(482.5f, box.Width, 1);
            Assert.Equal(86f, box.Height, 1);
            Assert.Equal(133.75f, box.LeftOffset, 1);
            Assert.Equal(32f, box.TopOffset, 1);
        }

        [Fact]
        public void AbsoluteChildrenOfAutoLayoutParentsUseAbsolutePositioning()
        {
            DesignNode parent = new()
            {
                LayoutMode = "HORIZONTAL",
            };
            DesignNode child = new()
            {
                LayoutPositioning = "ABSOLUTE",
            };

            Assert.True(UitkRenderPolicy.ShouldPositionAbsolutely(child, parent));
            Assert.False(UitkRenderPolicy.ShouldParticipateInAutoLayout(child, parent));
        }

        [Fact]
        public void NormalChildrenOfAutoLayoutParentsParticipateInFlexLayout()
        {
            DesignNode parent = new()
            {
                LayoutMode = "HORIZONTAL",
            };
            DesignNode child = new();

            Assert.False(UitkRenderPolicy.ShouldPositionAbsolutely(child, parent));
            Assert.True(UitkRenderPolicy.ShouldParticipateInAutoLayout(child, parent));
        }

        [Fact]
        public void ScrollViewContentChildDoesNotPreserveCapturedScrollOffset()
        {
            DesignNode parent = new();
            parent.Tags.Add(NodeTag.Scroll);

            DesignNode child = new()
            {
                LayoutPositioning = "ABSOLUTE",
            };

            Assert.False(UitkRenderPolicy.ShouldPositionAbsolutely(child, parent));
        }

        [Fact]
        public void HorizontalAutoLayoutContainerUsesChildrenMinimumWidth()
        {
            DesignNode node = new()
            {
                LayoutMode = "HORIZONTAL",
                ItemSpacing = 20,
                AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 },
                Children = new()
                {
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 } },
                },
            };

            UitkElementBox box = UitkRenderPolicy.ResolveElementBox(node, null, null, spriteScale: 1);

            Assert.Equal(572, box.Width);
            Assert.Equal(128, box.Height);
        }

        [Fact]
        public void AutoLayoutMinimumSizeIgnoresAbsoluteChildren()
        {
            DesignNode node = new()
            {
                LayoutMode = "HORIZONTAL",
                ItemSpacing = 20,
                AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 },
                Children = new()
                {
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 128, Height = 128 } },
                    new DesignNode
                    {
                        LayoutPositioning = "ABSOLUTE",
                        AbsoluteBoundingBox = new BoundingBox { Width = 512, Height = 512 },
                    },
                },
            };

            UitkElementBox box = UitkRenderPolicy.ResolveElementBox(node, null, null, spriteScale: 1);

            Assert.Equal(128, box.Width);
            Assert.Equal(128, box.Height);
        }

        [Fact]
        public void NonTextRasterImagesStretchRenderedNodeSnapshot()
        {
            DesignNode node = new();
            node.Tags.Add(NodeTag.Image);
            node.Fills = new()
            {
                new Paint
                {
                    Type = "IMAGE",
                    ScaleMode = "FILL",
                },
            };

            Assert.True(UitkRenderPolicy.ShouldEmitRasterBackgroundImage(node, hasResolvedSprite: true));
            Assert.Equal("stretch-to-fill", UitkRenderPolicy.ResolveRasterBackgroundScaleMode(node));
        }

        [Fact]
        public void MaskNodesAreNotEmittedAsVisibleUitkElements()
        {
            DesignNode node = new();
            node.Tags.Add(NodeTag.Mask);
            node.Tags.Add(NodeTag.Image);

            Assert.False(UitkRenderPolicy.ShouldEmitElement(node));
            Assert.False(UitkRenderPolicy.ShouldEmitRasterBackgroundImage(node, hasResolvedSprite: true));
        }

        [Fact]
        public void ParentsWithMaskChildrenClipContent()
        {
            DesignNode parent = new();
            parent.Children = new()
            {
                new DesignNode
                {
                    Annotations =
                    {
                        Tags = { NodeTag.Mask },
                    },
                },
            };

            Assert.True(UitkRenderPolicy.ShouldClipContent(parent));
        }

        [Fact]
        public void UitkNinePatchSpritesUseImportedSpriteBorder()
        {
            DesignNode node = new();
            node.Tags.Add(NodeTag.NinePatch);

            Assert.False(UitkRenderPolicy.ShouldEmitStyleSliceBorder(node, hasImportedSpriteBorder: true));
        }

        [Fact]
        public void UitkNinePatchSpritesDoNotEmitFallbackStyleSlices()
        {
            DesignNode node = new();
            node.Tags.Add(NodeTag.NinePatch);

            Assert.False(UitkRenderPolicy.ShouldEmitStyleSliceBorder(node, hasImportedSpriteBorder: false));
        }

        [Fact]
        public void NinePatchStyleSlicesUseTextureSpaceChildBounds()
        {
            DesignNode node = new()
            {
                AbsoluteBoundingBox = new BoundingBox { Width = 240, Height = 110 },
                Children = new()
                {
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 30, Height = 31 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 180, Height = 31 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 30, Height = 31 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 30, Height = 32 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 180, Height = 32 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 30, Height = 32 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 30, Height = 47 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 180, Height = 47 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 30, Height = 47 } },
                },
            };
            node.Tags.Add(NodeTag.NinePatch);

            UitkSliceBorder border = UitkRenderPolicy.ResolveNinePatchSliceBorder(node, spriteScale: 2);

            Assert.Equal(60, border.Left);
            Assert.Equal(60, border.Right);
            Assert.Equal(94, border.Bottom);
            Assert.Equal(62, border.Top);
        }

        [Fact]
        public void UitkNinePatchSlicesCanUseDesignSpaceChildBounds()
        {
            DesignNode node = new()
            {
                AbsoluteBoundingBox = new BoundingBox { Width = 100, Height = 24 },
                Children = new()
                {
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 11, Height = 11 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 78, Height = 11 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 11, Height = 11 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 11, Height = 2 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 78, Height = 2 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 11, Height = 2 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 11, Height = 11 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 78, Height = 11 } },
                    new DesignNode { AbsoluteBoundingBox = new BoundingBox { Width = 11, Height = 11 } },
                },
            };
            node.Tags.Add(NodeTag.NinePatch);

            UitkSliceBorder border = UitkRenderPolicy.ResolveNinePatchSliceBorder(node, spriteScale: 1);

            Assert.Equal(11, border.Left);
            Assert.Equal(11, border.Right);
            Assert.Equal(11, border.Bottom);
            Assert.Equal(11, border.Top);
        }
    }
}
