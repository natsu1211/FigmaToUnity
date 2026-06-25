using System.Collections.Generic;
using FigmaToUnity.Core;
using Xunit;

namespace FigmaToUnity.Core.Tests
{
    public class FigmaNodeHasherTests
    {
        [Fact]
        public void SameTreeProducesSameHashAcrossCalls()
        {
            DesignNode tree1 = BuildSimpleFrame();
            DesignNode tree2 = BuildSimpleFrame();

            FigmaNodeHasher.ComputeHashes(new[] { tree1 }, new HashOptions());
            FigmaNodeHasher.ComputeHashes(new[] { tree2 }, new HashOptions());

            Assert.Equal(tree1.NodeHash, tree2.NodeHash);
            Assert.Equal(tree1.Children![0].NodeHash, tree2.Children![0].NodeHash);
        }

        [Fact]
        public void TagInsertionOrderDoesNotAffectHash()
        {
            DesignNode a = BuildSimpleFrame();
            a.Tags.Add(NodeTag.Button);
            a.Tags.Add(NodeTag.Image);
            a.Tags.Add(NodeTag.AutoLayout);

            DesignNode b = BuildSimpleFrame();
            b.Tags.Add(NodeTag.AutoLayout);
            b.Tags.Add(NodeTag.Image);
            b.Tags.Add(NodeTag.Button);

            FigmaNodeHasher.ComputeHashes(new[] { a }, new HashOptions());
            FigmaNodeHasher.ComputeHashes(new[] { b }, new HashOptions());

            Assert.Equal(a.NodeHash, b.NodeHash);
        }

        [Fact]
        public void ColorChangeProducesDifferentHash()
        {
            DesignNode a = BuildSimpleFrame();
            a.Fills = new List<Paint>
            {
                new() { Type = "SOLID", Color = new FigmaColor { R = 1f, G = 0f, B = 0f, A = 1f } }
            };

            DesignNode b = BuildSimpleFrame();
            b.Fills = new List<Paint>
            {
                new() { Type = "SOLID", Color = new FigmaColor { R = 0f, G = 1f, B = 0f, A = 1f } }
            };

            FigmaNodeHasher.ComputeHashes(new[] { a }, new HashOptions());
            FigmaNodeHasher.ComputeHashes(new[] { b }, new HashOptions());

            Assert.NotEqual(a.NodeHash, b.NodeHash);
        }

        [Fact]
        public void ProceduralFalloffOptionOnlyInfluencesImageTaggedNodes()
        {
            DesignNode imageNode = BuildSimpleFrame();
            imageNode.Tags.Add(NodeTag.Image);

            FigmaNodeHasher.ComputeHashes(new[] { imageNode }, new HashOptions { ProceduralImageFalloff = 0.5f });
            int hashWithFalloffA = imageNode.NodeHash;

            FigmaNodeHasher.ComputeHashes(new[] { imageNode }, new HashOptions { ProceduralImageFalloff = 0.9f });
            int hashWithFalloffB = imageNode.NodeHash;

            Assert.NotEqual(hashWithFalloffA, hashWithFalloffB);

            DesignNode plainNode = BuildSimpleFrame();
            FigmaNodeHasher.ComputeHashes(new[] { plainNode }, new HashOptions { ProceduralImageFalloff = 0.5f });
            int plainA = plainNode.NodeHash;

            FigmaNodeHasher.ComputeHashes(new[] { plainNode }, new HashOptions { ProceduralImageFalloff = 0.9f });
            int plainB = plainNode.NodeHash;

            Assert.Equal(plainA, plainB);
        }

        private static DesignNode BuildSimpleFrame()
        {
            return new DesignNode
            {
                Id = "1:1",
                Name = "Root",
                Type = "FRAME",
                Children = new List<DesignNode>
                {
                    new() { Id = "1:2", Name = "Child", Type = "FRAME" }
                }
            };
        }
    }
}
