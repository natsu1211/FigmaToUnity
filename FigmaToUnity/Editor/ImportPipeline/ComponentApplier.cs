using System;
using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class ComponentApplier
    {
        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            foreach (FigmaNode node in nodes)
            {
                if (node.GameObject == null)
                {
                    continue;
                }

                ApplyButton(node);
                ApplyCanvasGroup(node);
                ApplyMask(node);
                ApplyAspectRatio(node);
                ApplyScrollRect(node);
            }
        }

        private static void ApplyButton(FigmaNode node)
        {
            if (!node.Tags.Contains(NodeTag.Button) || node.GameObject == null)
            {
                return;
            }

            Graphic targetGraphic = EnsureGraphic(node.GameObject, true);
            Button button = node.GameObject.GetComponent<Button>();
            if (button == null)
            {
                button = node.GameObject.AddComponent<Button>();
            }

            button.targetGraphic = targetGraphic;
            ApplyButtonStates(node, button);
        }

        private static void ApplyButtonStates(FigmaNode node, Button button)
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            Dictionary<string, FigmaNode> states = new(StringComparer.OrdinalIgnoreCase);
            foreach (FigmaNode child in node.Children)
            {
                string stateName = NormalizeButtonStateName(child.Name);
                if (!string.IsNullOrWhiteSpace(stateName) && !states.ContainsKey(stateName))
                {
                    states[stateName] = child;
                }
            }

            if (states.Count == 0)
            {
                return;
            }

            if (states.TryGetValue("default", out FigmaNode? defaultNode) || states.TryGetValue("normal", out defaultNode))
            {
                Graphic? defaultGraphic = FindFirstGraphic(defaultNode);
                if (defaultGraphic != null)
                {
                    defaultGraphic.raycastTarget = true;
                    button.targetGraphic = defaultGraphic;
                }
            }

            SpriteState spriteState = button.spriteState;
            bool hasSpriteSwap = false;

            hasSpriteSwap |= TryAssignStateSprite(states, "hover", sprite => spriteState.highlightedSprite = sprite);
            hasSpriteSwap |= TryAssignStateSprite(states, "pressed", sprite => spriteState.pressedSprite = sprite);
            hasSpriteSwap |= TryAssignStateSprite(states, "disabled", sprite => spriteState.disabledSprite = sprite);
            hasSpriteSwap |= TryAssignStateSprite(states, "selected", sprite => spriteState.selectedSprite = sprite);

            if (hasSpriteSwap)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = spriteState;
            }
        }

        private static bool TryAssignStateSprite(Dictionary<string, FigmaNode> states, string stateName, Action<Sprite> assign)
        {
            if (!states.TryGetValue(stateName, out FigmaNode? stateNode))
            {
                return false;
            }

            Image? image = FindFirstImage(stateNode);
            if (image == null || image.sprite == null)
            {
                return false;
            }

            assign(image.sprite);
            if (stateNode.GameObject != null)
            {
                stateNode.GameObject.SetActive(false);
            }

            return true;
        }

        private static string NormalizeButtonStateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string lowered = (name ?? string.Empty).Trim().ToLowerInvariant();
            return lowered switch
            {
                "default" => "default",
                "normal" => "normal",
                "hover" => "hover",
                "pressed" => "pressed",
                "disabled" => "disabled",
                "selected" => "selected",
                _ => string.Empty,
            };
        }

        private static Graphic? FindFirstGraphic(FigmaNode node)
        {
            if (node.GameObject != null)
            {
                Graphic graphic = node.GameObject.GetComponent<Graphic>();
                if (graphic != null)
                {
                    return graphic;
                }
            }

            if (node.Children == null)
            {
                return null;
            }

            foreach (FigmaNode child in node.Children)
            {
                Graphic? nestedGraphic = FindFirstGraphic(child);
                if (nestedGraphic != null)
                {
                    return nestedGraphic;
                }
            }

            return null;
        }

        private static Image? FindFirstImage(FigmaNode node)
        {
            if (node.GameObject != null)
            {
                Image image = node.GameObject.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            if (node.Children == null)
            {
                return null;
            }

            foreach (FigmaNode child in node.Children)
            {
                Image? nestedImage = FindFirstImage(child);
                if (nestedImage != null)
                {
                    return nestedImage;
                }
            }

            return null;
        }

        private static void ApplyCanvasGroup(FigmaNode node)
        {
            // v2: derive CanvasGroup directly from Opacity instead of from a tag (NodeTag.CanvasGroup removed).
            if (node.GameObject == null || !node.Opacity.HasValue || node.Opacity.Value >= 1f)
            {
                return;
            }

            CanvasGroup group = node.GameObject.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = node.GameObject.AddComponent<CanvasGroup>();
            }

            group.alpha = node.Opacity.Value;
        }

        private static void ApplyMask(FigmaNode node)
        {
            if (node.GameObject == null)
            {
                return;
            }

            if (node.Tags.Contains(NodeTag.ClipBounds))
            {
                if (ShouldUseRoundedMask(node))
                {
                    ApplyRoundedClipMask(node);
                }
                else
                {
                    if (node.GameObject.GetComponent<RectMask2D>() == null)
                    {
                        node.GameObject.AddComponent<RectMask2D>();
                    }
                }
            }

            if (node.Tags.Contains(NodeTag.Mask))
            {
                EnsureGraphic(node.GameObject, false);

                Mask mask = node.GameObject.GetComponent<Mask>();
                if (mask == null)
                {
                    mask = node.GameObject.AddComponent<Mask>();
                }

                mask.showMaskGraphic = false;
            }
        }

        private static bool ShouldUseRoundedMask(FigmaNode node)
        {
            return ImageStyleSupport.TryGetCornerRadii(node, out _);
        }

        private static void ApplyRoundedClipMask(FigmaNode node)
        {
            RectMask2D rectMask = node.GameObject!.GetComponent<RectMask2D>();
            if (rectMask != null)
            {
                UnityEngine.Object.DestroyImmediate(rectMask);
            }

            ProceduralRoundedImage image = node.GameObject!.GetComponent<ProceduralRoundedImage>();
            if (image == null)
            {
                image = node.GameObject.AddComponent<ProceduralRoundedImage>();
                image.color = new Color(1f, 1f, 1f, 1f);
                image.raycastTarget = false;
            }

            if (ImageStyleSupport.TryGetCornerRadii(node, out Vector4 cornerRadii))
            {
                image.CornerRadii = cornerRadii;
            }

            image.FalloffDistance = Mathf.Max(0.01f, FigmaImportSettings.instance.ProceduralImageFalloff);

            Mask mask = node.GameObject.GetComponent<Mask>();
            if (mask == null)
            {
                mask = node.GameObject.AddComponent<Mask>();
            }

            bool hasVisibleFill = node.Tags.Contains(NodeTag.Image);
            mask.showMaskGraphic = hasVisibleFill;
        }

        private static void ApplyAspectRatio(FigmaNode node)
        {
            if (!node.Tags.Contains(NodeTag.AspectRatio) || node.GameObject == null || node.AbsoluteBoundingBox == null || node.AbsoluteBoundingBox.Height <= 0f)
            {
                return;
            }

            // FitInParent overrides the rect to fill the parent while keeping aspect.
            // Only meaningful when the parent is a LayoutGroup that varies the child's size —
            // for fixed-rect siblings (e.g. children of a GROUP), it makes every sibling
            // expand to the same parent area and they all overlap.
            // Image.preserveAspect already handles sprite aspect inside a fixed rect.
            bool parentIsAutoLayout = node.Parent != null && node.Parent.Tags.Contains(NodeTag.AutoLayout);
            if (!parentIsAutoLayout)
            {
                AspectRatioFitter existing = node.GameObject.GetComponent<AspectRatioFitter>();
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing);
                }
                return;
            }

            AspectRatioFitter fitter = node.GameObject.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = node.GameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = node.AbsoluteBoundingBox.Width / node.AbsoluteBoundingBox.Height;
        }

        private static void ApplyScrollRect(FigmaNode node)
        {
            if (!node.Tags.Contains(NodeTag.Scroll) || node.GameObject == null || node.Children == null)
            {
                return;
            }

            ScrollRect scrollRect = node.GameObject.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                scrollRect = node.GameObject.AddComponent<ScrollRect>();
            }

            foreach (FigmaNode child in node.Children)
            {
                if (child.RectTransform == null)
                {
                    continue;
                }

                string lowered = child.Name.ToLowerInvariant();
                if (scrollRect.viewport == null && lowered.Contains("viewport"))
                {
                    scrollRect.viewport = child.RectTransform;
                }
                else if (scrollRect.content == null && lowered.Contains("content"))
                {
                    scrollRect.content = child.RectTransform;
                }
            }

            if (scrollRect.viewport == null && node.Children.Count > 0)
            {
                scrollRect.viewport = node.Children[0].RectTransform;
            }

            if (scrollRect.content == null && TryFindContentInViewport(node, scrollRect.viewport, out RectTransform? viewportContent))
            {
                scrollRect.content = viewportContent;
            }

            if (scrollRect.content == null)
            {
                scrollRect.content = scrollRect.viewport;
            }

            ApplyScrollAxes(node, scrollRect);
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private static void ApplyScrollAxes(FigmaNode node, ScrollRect scrollRect)
        {
            string overflow = node.OverflowDirection ?? string.Empty;
            if (string.IsNullOrWhiteSpace(overflow) || string.Equals(overflow, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                return;
            }

            string normalized = overflow.ToUpperInvariant();
            bool horizontal = normalized.Contains("HORIZONTAL");
            bool vertical = normalized.Contains("VERTICAL");
            if (!horizontal && !vertical)
            {
                horizontal = true;
                vertical = true;
            }

            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
        }

        private static bool TryFindContentInViewport(FigmaNode node, RectTransform? viewport, out RectTransform? content)
        {
            content = null;
            if (viewport == null || node.Children == null)
            {
                return false;
            }

            foreach (FigmaNode child in node.Children)
            {
                if (child.RectTransform != viewport || child.Children == null)
                {
                    continue;
                }

                foreach (FigmaNode viewportChild in child.Children)
                {
                    if (viewportChild.RectTransform == null)
                    {
                        continue;
                    }

                    string lowered = viewportChild.Name.ToLowerInvariant();
                    if (lowered.Contains("content"))
                    {
                        content = viewportChild.RectTransform;
                        return true;
                    }
                }

                if (child.Children.Count > 0)
                {
                    content = child.Children[0].RectTransform;
                    return content != null;
                }
            }

            return false;
        }

        private static Graphic EnsureGraphic(GameObject gameObject, bool raycastTarget)
        {
            Graphic graphic = gameObject.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = raycastTarget;
                return graphic;
            }

            Image image = gameObject.GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
            }

            image.raycastTarget = raycastTarget;
            return image;
        }
    }
}
