using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Runtime
{
    [AddComponentMenu("Layout/Figma Wrap Layout Group")]
    public sealed class WrapLayoutGroup : LayoutGroup
    {
        public enum Alignment
        {
            Start,
            Center,
            End,
        }

        [SerializeField] private bool m_Horizontal = true;
        [SerializeField] private float m_Spacing;
        [SerializeField] private float m_SecondarySpacing;
        [SerializeField] private bool m_ChildControlWidth = true;
        [SerializeField] private bool m_ChildControlHeight = true;
        [SerializeField] private bool m_MainAxisSpaceBetween;
        [SerializeField] private bool m_CrossAxisSpaceBetween;
        [SerializeField] private Alignment m_CrossAxisContentAlignment = Alignment.Start;

        private readonly List<PlacedChild> _children = new();
        private readonly List<LineInfo> _lines = new();

        public bool Horizontal
        {
            get => m_Horizontal;
            set => SetProperty(ref m_Horizontal, value);
        }

        public float Spacing
        {
            get => m_Spacing;
            set => SetProperty(ref m_Spacing, value);
        }

        public float SecondarySpacing
        {
            get => m_SecondarySpacing;
            set => SetProperty(ref m_SecondarySpacing, value);
        }

        public bool ChildControlWidth
        {
            get => m_ChildControlWidth;
            set => SetProperty(ref m_ChildControlWidth, value);
        }

        public bool ChildControlHeight
        {
            get => m_ChildControlHeight;
            set => SetProperty(ref m_ChildControlHeight, value);
        }

        public bool MainAxisSpaceBetween
        {
            get => m_MainAxisSpaceBetween;
            set => SetProperty(ref m_MainAxisSpaceBetween, value);
        }

        public bool CrossAxisSpaceBetween
        {
            get => m_CrossAxisSpaceBetween;
            set => SetProperty(ref m_CrossAxisSpaceBetween, value);
        }

        public Alignment CrossAxisContentAlignment
        {
            get => m_CrossAxisContentAlignment;
            set => SetProperty(ref m_CrossAxisContentAlignment, value);
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            LayoutComputation layout = CalculateLayout();
            SetLayoutInputForAxis(layout.PreferredWidth, layout.PreferredWidth, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            LayoutComputation layout = CalculateLayout();
            SetLayoutInputForAxis(layout.PreferredHeight, layout.PreferredHeight, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            ApplyLayout();
        }

        public override void SetLayoutVertical()
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            LayoutComputation layout = CalculateLayout();
            if (_children.Count == 0)
            {
                return;
            }

            float availableCross = GetAvailableCrossSize();
            float extraCross = float.IsInfinity(availableCross) ? 0f : Mathf.Max(0f, availableCross - layout.ContentCrossSize);
            float lineGap = m_SecondarySpacing;
            float crossOffset = 0f;

            if (m_CrossAxisSpaceBetween && _lines.Count > 1 && !float.IsInfinity(availableCross))
            {
                lineGap += extraCross / (_lines.Count - 1);
            }
            else
            {
                crossOffset = extraCross * GetAlignmentFactor(m_CrossAxisContentAlignment);
            }

            float runningCross = crossOffset;
            for (int lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
            {
                LineInfo line = _lines[lineIndex];
                ApplyLine(line, runningCross);
                runningCross += line.CrossSize + lineGap;
            }
        }

        private void ApplyLine(LineInfo line, float crossOffset)
        {
            float availableMain = GetAvailableMainSize();
            float extraMain = float.IsInfinity(availableMain) ? 0f : Mathf.Max(0f, availableMain - line.MainSize);
            float mainOffset = 0f;
            float extraItemSpacing = 0f;

            if (m_MainAxisSpaceBetween && line.ItemCount > 1 && !float.IsInfinity(availableMain))
            {
                extraItemSpacing = extraMain / (line.ItemCount - 1);
            }
            else
            {
                mainOffset = extraMain * GetMainAxisAlignmentFactor();
            }

            float innerCrossAlignment = GetInnerCrossAlignmentFactor();
            for (int i = 0; i < line.ItemCount; i++)
            {
                int childIndex = line.StartIndex + i;
                PlacedChild child = _children[childIndex];
                float childMain = mainOffset + child.MainPosition + extraItemSpacing * i;
                float childCrossSize = m_Horizontal ? child.Height : child.Width;
                float childCross = crossOffset + (line.CrossSize - childCrossSize) * innerCrossAlignment;

                if (m_Horizontal)
                {
                    SetChildAlongAxis(child.RectTransform, 0, padding.left + childMain, child.Width);
                    SetChildAlongAxis(child.RectTransform, 1, padding.top + childCross, child.Height);
                }
                else
                {
                    SetChildAlongAxis(child.RectTransform, 0, padding.left + childCross, child.Width);
                    SetChildAlongAxis(child.RectTransform, 1, padding.top + childMain, child.Height);
                }
            }
        }

        private LayoutComputation CalculateLayout()
        {
            _children.Clear();
            _lines.Clear();

            if (rectChildren.Count == 0)
            {
                return new LayoutComputation(padding.horizontal, padding.vertical, 0f);
            }

            return m_Horizontal ? CalculateHorizontalLayout() : CalculateVerticalLayout();
        }

        private LayoutComputation CalculateHorizontalLayout()
        {
            float availableWidth = GetAvailableMainSize();
            float currentLineWidth = 0f;
            float currentLineHeight = 0f;
            int lineStart = 0;
            int lineItemCount = 0;

            foreach (RectTransform child in rectChildren)
            {
                float childWidth = GetChildSize(child, 0, m_ChildControlWidth);
                float childHeight = GetChildSize(child, 1, m_ChildControlHeight);
                float nextWidth = lineItemCount > 0 ? currentLineWidth + m_Spacing + childWidth : childWidth;
                if (lineItemCount > 0 && nextWidth > availableWidth)
                {
                    _lines.Add(new LineInfo(lineStart, lineItemCount, currentLineWidth, currentLineHeight));
                    currentLineWidth = 0f;
                    currentLineHeight = 0f;
                    lineStart = _children.Count;
                    lineItemCount = 0;
                }

                float childX = lineItemCount > 0 ? currentLineWidth + m_Spacing : 0f;
                _children.Add(new PlacedChild(child, childX, childWidth, childHeight));
                currentLineWidth = childX + childWidth;
                currentLineHeight = Mathf.Max(currentLineHeight, childHeight);
                lineItemCount++;
            }

            if (lineItemCount > 0)
            {
                _lines.Add(new LineInfo(lineStart, lineItemCount, currentLineWidth, currentLineHeight));
            }

            return BuildComputation(true);
        }

        private LayoutComputation CalculateVerticalLayout()
        {
            float availableHeight = GetAvailableMainSize();
            float currentLineHeight = 0f;
            float currentLineWidth = 0f;
            int lineStart = 0;
            int lineItemCount = 0;

            foreach (RectTransform child in rectChildren)
            {
                float childWidth = GetChildSize(child, 0, m_ChildControlWidth);
                float childHeight = GetChildSize(child, 1, m_ChildControlHeight);
                float nextHeight = lineItemCount > 0 ? currentLineHeight + m_Spacing + childHeight : childHeight;
                if (lineItemCount > 0 && nextHeight > availableHeight)
                {
                    _lines.Add(new LineInfo(lineStart, lineItemCount, currentLineHeight, currentLineWidth));
                    currentLineHeight = 0f;
                    currentLineWidth = 0f;
                    lineStart = _children.Count;
                    lineItemCount = 0;
                }

                float childY = lineItemCount > 0 ? currentLineHeight + m_Spacing : 0f;
                _children.Add(new PlacedChild(child, childY, childWidth, childHeight));
                currentLineHeight = childY + childHeight;
                currentLineWidth = Mathf.Max(currentLineWidth, childWidth);
                lineItemCount++;
            }

            if (lineItemCount > 0)
            {
                _lines.Add(new LineInfo(lineStart, lineItemCount, currentLineHeight, currentLineWidth));
            }

            return BuildComputation(false);
        }

        private LayoutComputation BuildComputation(bool horizontal)
        {
            float contentMainSize = 0f;
            float contentCrossSize = 0f;
            for (int i = 0; i < _lines.Count; i++)
            {
                LineInfo line = _lines[i];
                contentMainSize = Mathf.Max(contentMainSize, line.MainSize);
                contentCrossSize += line.CrossSize;
            }

            if (_lines.Count > 1)
            {
                contentCrossSize += m_SecondarySpacing * (_lines.Count - 1);
            }

            return horizontal
                ? new LayoutComputation(padding.horizontal + contentMainSize, padding.vertical + contentCrossSize, contentCrossSize)
                : new LayoutComputation(padding.horizontal + contentCrossSize, padding.vertical + contentMainSize, contentCrossSize);
        }

        private float GetAvailableMainSize()
        {
            float value = m_Horizontal ? rectTransform.rect.width - padding.horizontal : rectTransform.rect.height - padding.vertical;
            return value > 0f ? value : float.PositiveInfinity;
        }

        private float GetAvailableCrossSize()
        {
            float value = m_Horizontal ? rectTransform.rect.height - padding.vertical : rectTransform.rect.width - padding.horizontal;
            return value > 0f ? value : float.PositiveInfinity;
        }

        private float GetChildSize(RectTransform child, int axis, bool controlSize)
        {
            float preferred = LayoutUtility.GetPreferredSize(child, axis);
            if (controlSize && preferred > 0f)
            {
                return preferred;
            }

            float size = axis == 0 ? child.rect.width : child.rect.height;
            if (size > 0f)
            {
                return size;
            }

            return axis == 0 ? child.sizeDelta.x : child.sizeDelta.y;
        }

        private float GetMainAxisAlignmentFactor()
        {
            if (m_Horizontal)
            {
                return childAlignment switch
                {
                    TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => 0.5f,
                    TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => 1f,
                    _ => 0f,
                };
            }

            return childAlignment switch
            {
                TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => 0.5f,
                TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => 1f,
                _ => 0f,
            };
        }

        private float GetInnerCrossAlignmentFactor()
        {
            if (m_Horizontal)
            {
                return childAlignment switch
                {
                    TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => 0.5f,
                    TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => 1f,
                    _ => 0f,
                };
            }

            return childAlignment switch
            {
                TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => 0.5f,
                TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => 1f,
                _ => 0f,
            };
        }

        private static float GetAlignmentFactor(Alignment alignment)
        {
            return alignment switch
            {
                Alignment.Center => 0.5f,
                Alignment.End => 1f,
                _ => 0f,
            };
        }

        private readonly struct PlacedChild
        {
            public PlacedChild(RectTransform rectTransform, float mainPosition, float width, float height)
            {
                RectTransform = rectTransform;
                MainPosition = mainPosition;
                Width = width;
                Height = height;
            }

            public RectTransform RectTransform { get; }
            public float MainPosition { get; }
            public float Width { get; }
            public float Height { get; }
        }

        private readonly struct LineInfo
        {
            public LineInfo(int startIndex, int itemCount, float mainSize, float crossSize)
            {
                StartIndex = startIndex;
                ItemCount = itemCount;
                MainSize = mainSize;
                CrossSize = crossSize;
            }

            public int StartIndex { get; }
            public int ItemCount { get; }
            public float MainSize { get; }
            public float CrossSize { get; }
        }

        private readonly struct LayoutComputation
        {
            public LayoutComputation(float preferredWidth, float preferredHeight, float contentCrossSize)
            {
                PreferredWidth = preferredWidth;
                PreferredHeight = preferredHeight;
                ContentCrossSize = contentCrossSize;
            }

            public float PreferredWidth { get; }
            public float PreferredHeight { get; }
            public float ContentCrossSize { get; }
        }
    }
}
