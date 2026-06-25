using System.Collections.Generic;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class ImageApplier
    {
        private readonly UnityImageBackend _unityImageBackend = new();
        private readonly ProceduralRoundedImageBackend _proceduralRoundedImageBackend = new();

        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            List<FigmaNode> proceduralNodes = new();
            List<FigmaNode> unityNodes = new();

            foreach (FigmaNode node in nodes)
            {
                if (!node.Tags.Contains(NodeTag.Image) || node.Tags.Contains(NodeTag.Text))
                {
                    continue;
                }

                if (ImageStyleSupport.TryGetCornerRadii(node, out _))
                {
                    proceduralNodes.Add(node);
                }
                else
                {
                    unityNodes.Add(node);
                }
            }

            if (proceduralNodes.Count > 0)
            {
                _proceduralRoundedImageBackend.Apply(proceduralNodes);
            }

            if (unityNodes.Count > 0)
            {
                _unityImageBackend.Apply(unityNodes);
            }
        }
    }
}
