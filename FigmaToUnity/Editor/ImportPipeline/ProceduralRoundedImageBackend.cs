using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class ProceduralRoundedImageBackend
    {
        private readonly UnityImageBackend _unityImageBackend = new();

        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            foreach (FigmaNode node in nodes)
            {
                if (!node.Tags.Contains(NodeTag.Image) || node.Tags.Contains(NodeTag.Text) || node.GameObject == null)
                {
                    continue;
                }

                Image.Type imageType = ImageStyleSupport.GetImageType(node);
                if (imageType is Image.Type.Sliced or Image.Type.Tiled)
                {
                    Debug.LogWarning($"[FigmaImporter] Procedural rounded image fallback: {imageType} unsupported on '{node.Name}'. Using Unity Image.");
                    _unityImageBackend.Apply(new[] { node });
                    continue;
                }

                RemoveBaseImage(node.GameObject);
                RemoveOutline(node.GameObject);

                ProceduralRoundedImage image = node.GameObject.GetComponent<ProceduralRoundedImage>();
                if (image == null)
                {
                    image = node.GameObject.AddComponent<ProceduralRoundedImage>();
                }

                image.raycastTarget = node.Tags.Contains(NodeTag.Button);
                image.type = imageType;
                image.preserveAspect = ImageStyleSupport.ShouldPreserveAspect(node, imageType);

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

                image.CornerRadii = ImageStyleSupport.TryGetCornerRadii(node, out Vector4 cornerRadii)
                    ? cornerRadii
                    : Vector4.zero;

                if (ImageStyleSupport.TryGetStroke(node, out Color strokeColor, out float strokeWidth))
                {
                    image.StrokeColor = strokeColor;
                    image.StrokeWidth = strokeWidth;
                }
                else
                {
                    image.StrokeColor = Color.clear;
                    image.StrokeWidth = 0f;
                }

                image.FalloffDistance = FigmaImportSettings.instance.ProceduralImageFalloff;
            }
        }

        private static void RemoveBaseImage(GameObject gameObject)
        {
            Image image = gameObject.GetComponent<Image>();
            if (image != null && image.GetType() == typeof(Image))
            {
                Object.DestroyImmediate(image);
            }
        }

        private static void RemoveOutline(GameObject gameObject)
        {
            Outline outline = gameObject.GetComponent<Outline>();
            if (outline != null)
            {
                Object.DestroyImmediate(outline);
            }
        }
    }
}
