using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class UnityImageBackend
    {
        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            foreach (FigmaNode node in nodes)
            {
                if (!node.Tags.Contains(NodeTag.Image) || node.Tags.Contains(NodeTag.Text) || node.GameObject == null)
                {
                    continue;
                }

                RemoveProceduralImage(node.GameObject);

                Image image = node.GameObject.GetComponent<Image>();
                if (image == null)
                {
                    image = node.GameObject.AddComponent<Image>();
                }

                image.raycastTarget = node.Tags.Contains(NodeTag.Button);
                image.type = ImageStyleSupport.GetImageType(node);
                image.preserveAspect = ImageStyleSupport.ShouldPreserveAspect(node, image.type);

                if (node.Sprite != null)
                {
                    image.sprite = node.Sprite;
                    image.color = Color.white;
                }
                else
                {
                    image.sprite = null;
                    image.color = ImageStyleSupport.GetNodeColor(node);
                }

                ApplyStrokeOutline(node, image);
            }
        }

        private static void ApplyStrokeOutline(FigmaNode node, Image image)
        {
            if (!ImageStyleSupport.TryGetStroke(node, out Color strokeColor, out float strokeWidth))
            {
                Outline existingOutline = image.GetComponent<Outline>();
                if (existingOutline != null)
                {
                    Object.DestroyImmediate(existingOutline);
                }

                return;
            }

            Outline outline = image.GetComponent<Outline>();
            if (outline == null)
            {
                outline = image.gameObject.AddComponent<Outline>();
            }

            outline.useGraphicAlpha = false;
            outline.effectColor = strokeColor;
            outline.effectDistance = new Vector2(strokeWidth, -strokeWidth);
        }

        private static void RemoveProceduralImage(GameObject gameObject)
        {
            ProceduralRoundedImage proceduralImage = gameObject.GetComponent<ProceduralRoundedImage>();
            if (proceduralImage != null)
            {
                Object.DestroyImmediate(proceduralImage);
            }
        }
    }
}
