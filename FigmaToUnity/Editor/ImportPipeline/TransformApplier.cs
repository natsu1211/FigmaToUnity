using System.Collections.Generic;
using FigmaToUnity.Core;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class TransformApplier
    {
        public void ApplyBaseTransforms(IReadOnlyList<FigmaNode> nodes)
        {
            foreach (FigmaNode node in nodes)
            {
                if (node.RectTransform == null)
                {
                    continue;
                }

                ApplyNodeTransform(node);
            }
        }

        private void ApplyNodeTransform(FigmaNode node)
        {
            RectTransform rectTransform = node.RectTransform!;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            BoundingBox? box = node.AbsoluteBoundingBox;
            if (box == null)
            {
                SetAnchors(rectTransform, node);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
                return;
            }

            string horizontal = node.Constraints?.Horizontal ?? "LEFT";
            string vertical = node.Constraints?.Vertical ?? "TOP";
            bool stretchWidth = horizontal is "LEFT_RIGHT" or "SCALE";
            bool stretchHeight = vertical is "TOP_BOTTOM" or "SCALE";

            if (IsManagedByAutoLayout(node))
            {
                // The parent LayoutGroup owns position and (often) size.
                // sizeDelta is only a real "size" with single-point anchors —
                // stretch anchors would re-interpret it as overflow.
                Vector2 pointAnchor = new(0.5f, 0.5f);
                rectTransform.anchorMin = pointAnchor;
                rectTransform.anchorMax = pointAnchor;
                rectTransform.pivot = pointAnchor;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.sizeDelta = new Vector2(box.Width, box.Height);
                rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                SetAnchors(rectTransform, node);
                rectTransform.pivot = GetPivot(horizontal, vertical);

                if (node.Parent == null || node.Parent.RectTransform == null || node.Parent.AbsoluteBoundingBox == null)
                {
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                    rectTransform.sizeDelta = new Vector2(box.Width, box.Height);
                    rectTransform.anchoredPosition = Vector2.zero;
                }
                else
                {
                    BoundingBox parentBox = node.Parent.AbsoluteBoundingBox;
                    ApplyPosition(node, rectTransform, box, parentBox, horizontal, vertical, stretchWidth, stretchHeight);
                }
            }

            if (node.Rotation.HasValue && !Mathf.Approximately(node.Rotation.Value, 0f))
            {
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, -node.Rotation.Value);
            }
        }

        private static bool IsManagedByAutoLayout(FigmaNode node)
        {
            return node.Parent != null &&
                   node.Parent.Tags.Contains(NodeTag.AutoLayout) &&
                   node.LayoutPositioning != "ABSOLUTE";
        }

        private static void ApplyPosition(
            FigmaNode node,
            RectTransform rectTransform,
            BoundingBox box,
            BoundingBox parentBox,
            string horizontal,
            string vertical,
            bool stretchWidth,
            bool stretchHeight)
        {
            GetLocalOffsets(node, box, parentBox, out float left, out float top);
            float right = parentBox.Width - left - box.Width;
            float bottom = parentBox.Height - top - box.Height;

            // Compute offsetMin/Max directly for both stretched and point-anchor
            // axes. Mixing offsetMin/Max with sizeDelta in stretch mode causes
            // Unity to back-solve and wipe the insets — write them in one shot.
            Vector2 pivot = rectTransform.pivot;

            float anchoredX = horizontal switch
            {
                "RIGHT" => -right,
                "CENTER" => left + box.Width * 0.5f - parentBox.Width * 0.5f,
                "LEFT_RIGHT" => 0f,
                "SCALE" => 0f,
                _ => left,
            };

            float anchoredY = vertical switch
            {
                "BOTTOM" => bottom,
                "CENTER" => -(top + box.Height * 0.5f - parentBox.Height * 0.5f),
                "TOP_BOTTOM" => 0f,
                "SCALE" => 0f,
                _ => -top,
            };

            float minX = stretchWidth ? left : anchoredX - pivot.x * box.Width;
            float maxX = stretchWidth ? -right : anchoredX + (1f - pivot.x) * box.Width;
            float minY = stretchHeight ? bottom : anchoredY - pivot.y * box.Height;
            float maxY = stretchHeight ? -top : anchoredY + (1f - pivot.y) * box.Height;

            rectTransform.offsetMin = new Vector2(minX, minY);
            rectTransform.offsetMax = new Vector2(maxX, maxY);
        }

        private static void GetLocalOffsets(FigmaNode node, BoundingBox box, BoundingBox parentBox, out float left, out float top)
        {
            if (node.RelativeTransform != null &&
                node.RelativeTransform.Count >= 2 &&
                node.RelativeTransform[0] != null &&
                node.RelativeTransform[1] != null &&
                node.RelativeTransform[0].Count >= 3 &&
                node.RelativeTransform[1].Count >= 3 &&
                node.RelativeTransform[0][2].HasValue &&
                node.RelativeTransform[1][2].HasValue)
            {
                left = node.RelativeTransform[0][2]!.Value;
                top = node.RelativeTransform[1][2]!.Value;
                return;
            }

            left = box.X - parentBox.X;
            top = box.Y - parentBox.Y;
        }

        private void SetAnchors(RectTransform rectTransform, FigmaNode node)
        {
            string horizontal = node.Constraints?.Horizontal ?? "LEFT";
            string vertical = node.Constraints?.Vertical ?? "TOP";

            Vector2 anchorMin = new(0f, 1f);
            Vector2 anchorMax = new(0f, 1f);

            switch (horizontal)
            {
                case "LEFT_RIGHT":
                case "SCALE":
                    anchorMin.x = 0f;
                    anchorMax.x = 1f;
                    break;
                case "CENTER":
                    anchorMin.x = 0.5f;
                    anchorMax.x = 0.5f;
                    break;
                case "RIGHT":
                    anchorMin.x = 1f;
                    anchorMax.x = 1f;
                    break;
                default:
                    anchorMin.x = 0f;
                    anchorMax.x = 0f;
                    break;
            }

            switch (vertical)
            {
                case "TOP_BOTTOM":
                case "SCALE":
                    anchorMin.y = 0f;
                    anchorMax.y = 1f;
                    break;
                case "CENTER":
                    anchorMin.y = 0.5f;
                    anchorMax.y = 0.5f;
                    break;
                case "BOTTOM":
                    anchorMin.y = 0f;
                    anchorMax.y = 0f;
                    break;
                default:
                    anchorMin.y = 1f;
                    anchorMax.y = 1f;
                    break;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }

        private static Vector2 GetPivot(string horizontal, string vertical)
        {
            float pivotX = horizontal switch
            {
                "RIGHT" => 1f,
                "CENTER" => 0.5f,
                "LEFT_RIGHT" => 0.5f,
                "SCALE" => 0.5f,
                _ => 0f,
            };

            float pivotY = vertical switch
            {
                "BOTTOM" => 0f,
                "CENTER" => 0.5f,
                "TOP_BOTTOM" => 0.5f,
                "SCALE" => 0.5f,
                _ => 1f,
            };

            return new Vector2(pivotX, pivotY);
        }
    }
}
