using FigmaToUnity.Core;
using Xunit;

namespace FigmaToUnity.Core.Tests
{
    public class NodeTaggerTests
    {
        [Fact]
        public void ClippedViewportWithShiftedOversizedContentIsNotAutoTaggedAsScroll()
        {
            DesignNode viewport = new()
            {
                ClipsContent = true,
                AbsoluteBoundingBox = new BoundingBox
                {
                    X = 47,
                    Y = 166,
                    Width = 610,
                    Height = 920,
                },
                Children = new()
                {
                    new DesignNode
                    {
                        Type = "FRAME",
                        AbsoluteBoundingBox = new BoundingBox
                        {
                            X = 47,
                            Y = -3356,
                            Width = 610,
                            Height = 4514,
                        },
                        Children = new()
                        {
                            new DesignNode
                            {
                                Type = "TEXT",
                                Characters = "Long scroll content",
                            },
                        },
                    },
                },
            };
            DesignNode root = new()
            {
                Type = "FRAME",
                Children = new() { viewport },
            };

            new NodeTagger().TagTree(root);

            Assert.DoesNotContain(NodeTag.Scroll, viewport.Tags);
        }

        [Fact]
        public void RootFrameWithOversizedArtIsNotAutoTaggedAsScroll()
        {
            DesignNode root = new()
            {
                Type = "FRAME",
                ClipsContent = true,
                OverflowDirection = "VERTICAL_SCROLLING",
                AbsoluteBoundingBox = new BoundingBox
                {
                    X = 0,
                    Y = 0,
                    Width = 750,
                    Height = 1334,
                },
                Children = new()
                {
                    new DesignNode
                    {
                        Type = "FRAME",
                        AbsoluteBoundingBox = new BoundingBox
                        {
                            X = 0,
                            Y = 0,
                            Width = 1024,
                            Height = 2688,
                        },
                    },
                },
            };

            new NodeTagger().TagTree(root);

            Assert.DoesNotContain(NodeTag.Scroll, root.Tags);
        }

        [Fact]
        public void DecorativeGroupsAreTaggedAsImagesAndIgnoreChildren()
        {
            DesignNode decoration = new()
            {
                Name = "装飾",
                Type = "FRAME",
                Children = new()
                {
                    new DesignNode
                    {
                        Type = "RECTANGLE",
                        Fills = new()
                        {
                            new Paint
                            {
                                Type = "SOLID",
                                Color = new FigmaColor { R = 0f, G = 1f, B = 1f, A = 1f },
                            },
                        },
                    },
                },
            };

            new NodeTagger().TagTree(decoration);

            Assert.Contains(NodeTag.Image, decoration.Tags);
            Assert.True(decoration.ForceImage);
            Assert.True(decoration.Children[0].IgnoreNode);
        }
    }
}
