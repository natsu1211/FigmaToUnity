using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor
{
    internal sealed class LayoutApplier
    {
        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            foreach (FigmaNode node in nodes)
            {
                if (node.GameObject == null)
                {
                    continue;
                }

                ApplyLayoutGroup(node);
                ApplyLayoutElement(node);
            }
        }

        private void ApplyLayoutGroup(FigmaNode node)
        {
            if (!node.Tags.Contains(NodeTag.AutoLayout) || node.GameObject == null)
            {
                return;
            }

            if (node.Tags.Contains(NodeTag.AutoLayoutWrap))
            {
                ApplyWrapLayoutGroup(node);
            }
            else
            {
                ApplyLinearLayoutGroup(node);
            }

            ApplyContentSizeFitter(node);
        }

        private static void ApplyLinearLayoutGroup(FigmaNode node)
        {
            GameObject gameObject = node.GameObject!;
            HorizontalOrVerticalLayoutGroup layoutGroup;
            if (node.LayoutMode == "HORIZONTAL")
            {
                HorizontalLayoutGroup component = gameObject.GetComponent<HorizontalLayoutGroup>();
                layoutGroup = component != null ? component : gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            else
            {
                VerticalLayoutGroup component = gameObject.GetComponent<VerticalLayoutGroup>();
                layoutGroup = component != null ? component : gameObject.AddComponent<VerticalLayoutGroup>();
            }

            (bool childControlWidth, bool childControlHeight) = GetChildControlByLayoutMode(node);

            ConfigureCommonLayout(layoutGroup, node);
            layoutGroup.spacing = node.ItemSpacing ?? 0f;
            layoutGroup.childControlWidth = childControlWidth;
            layoutGroup.childControlHeight = childControlHeight;
            layoutGroup.childForceExpandWidth = childControlWidth || GetCounterAxisSpaceBetween(node);
            layoutGroup.childForceExpandHeight = childControlHeight || GetPrimaryAxisSpaceBetween(node);
            layoutGroup.childScaleWidth = false;
            layoutGroup.childScaleHeight = false;
        }

        private static void ApplyWrapLayoutGroup(FigmaNode node)
        {
            GameObject gameObject = node.GameObject!;
            WrapLayoutGroup wrapLayoutGroup = gameObject.GetComponent<WrapLayoutGroup>();
            if (wrapLayoutGroup == null)
            {
                wrapLayoutGroup = gameObject.AddComponent<WrapLayoutGroup>();
            }

            (bool childControlWidth, bool childControlHeight) = GetChildControlByLayoutMode(node);

            ConfigureCommonLayout(wrapLayoutGroup, node);
            wrapLayoutGroup.Horizontal = node.LayoutMode != "VERTICAL";
            wrapLayoutGroup.Spacing = node.ItemSpacing ?? 0f;
            wrapLayoutGroup.SecondarySpacing = node.CounterAxisSpacing ?? node.ItemSpacing ?? 0f;
            wrapLayoutGroup.ChildControlWidth = childControlWidth;
            wrapLayoutGroup.ChildControlHeight = childControlHeight;
            wrapLayoutGroup.MainAxisSpaceBetween = GetPrimaryAxisSpaceBetween(node);
            wrapLayoutGroup.CrossAxisSpaceBetween = node.CounterAxisAlignContent == "SPACE_BETWEEN";
            wrapLayoutGroup.CrossAxisContentAlignment = GetWrapContentAlignment(node.CounterAxisAlignContent);
        }

        private static void ConfigureCommonLayout(LayoutGroup layoutGroup, FigmaNode node)
        {
            layoutGroup.padding = new RectOffset(
                Mathf.RoundToInt(node.PaddingLeft ?? 0f),
                Mathf.RoundToInt(node.PaddingRight ?? 0f),
                Mathf.RoundToInt(node.PaddingTop ?? 0f),
                Mathf.RoundToInt(node.PaddingBottom ?? 0f));
            layoutGroup.childAlignment = GetChildAlignment(node);
        }

        private static void ApplyContentSizeFitter(FigmaNode node)
        {
            if (node.GameObject == null)
            {
                return;
            }

            ContentSizeFitter fitter = node.GameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = node.GameObject.AddComponent<ContentSizeFitter>();
            }

            ContentSizeFitter.FitMode primaryFit = GetFitMode(node.PrimaryAxisSizingMode);
            ContentSizeFitter.FitMode counterFit = GetFitMode(node.CounterAxisSizingMode);

            if (node.LayoutMode == "VERTICAL")
            {
                fitter.horizontalFit = counterFit;
                fitter.verticalFit = primaryFit;
            }
            else
            {
                fitter.horizontalFit = primaryFit;
                fitter.verticalFit = counterFit;
            }
        }

        private static ContentSizeFitter.FitMode GetFitMode(string? sizingMode)
        {
            return sizingMode is "AUTO" or "NONE"
                ? ContentSizeFitter.FitMode.PreferredSize
                : ContentSizeFitter.FitMode.Unconstrained;
        }

        private void ApplyLayoutElement(FigmaNode node)
        {
            if (node.Parent == null || !node.Parent.Tags.Contains(NodeTag.AutoLayout) || node.GameObject == null)
            {
                return;
            }

            LayoutElement layoutElement = node.GameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = node.GameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = node.LayoutPositioning == "ABSOLUTE";
            layoutElement.minWidth = node.MinWidth ?? -1f;
            layoutElement.minHeight = node.MinHeight ?? -1f;
            layoutElement.preferredWidth = -1f;
            layoutElement.preferredHeight = -1f;
            layoutElement.flexibleWidth = -1f;
            layoutElement.flexibleHeight = -1f;

            if (node.AbsoluteBoundingBox != null)
            {
                layoutElement.preferredWidth = node.AbsoluteBoundingBox.Width;
                layoutElement.preferredHeight = node.AbsoluteBoundingBox.Height;
            }

            if (node.LayoutSizingHorizontal == "FILL")
            {
                layoutElement.flexibleWidth = 1f;
            }

            if (node.LayoutSizingVertical == "FILL")
            {
                layoutElement.flexibleHeight = 1f;
            }

            if (node.LayoutGrow == 1)
            {
                if (node.Parent.LayoutMode == "HORIZONTAL")
                {
                    layoutElement.flexibleWidth = 1f;
                }
                else if (node.Parent.LayoutMode == "VERTICAL")
                {
                    layoutElement.flexibleHeight = 1f;
                }
            }

            if (node.LayoutAlign == "STRETCH")
            {
                if (node.Parent.LayoutMode == "HORIZONTAL")
                {
                    layoutElement.flexibleHeight = 1f;
                }
                else if (node.Parent.LayoutMode == "VERTICAL")
                {
                    layoutElement.flexibleWidth = 1f;
                }
            }
        }

        private static (bool childControlWidth, bool childControlHeight) GetChildControlByLayoutMode(FigmaNode node)
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return (false, false);
            }

            bool hasLayoutChildren = false;
            bool allLayoutGrow = true;
            bool allStretch = true;

            foreach (FigmaNode child in node.Children)
            {
                if (child.IgnoreNode || child.LayoutPositioning == "ABSOLUTE")
                {
                    continue;
                }

                hasLayoutChildren = true;
                if (child.LayoutGrow != 1f)
                {
                    allLayoutGrow = false;
                }

                if (child.LayoutAlign != "STRETCH")
                {
                    allStretch = false;
                }
            }

            if (!hasLayoutChildren)
            {
                return (false, false);
            }

            bool childControlWidth = false;
            bool childControlHeight = false;

            if (allLayoutGrow)
            {
                if (node.LayoutMode == "HORIZONTAL")
                {
                    childControlWidth = true;
                }
                else if (node.LayoutMode == "VERTICAL")
                {
                    childControlHeight = true;
                }
            }

            if (allStretch)
            {
                if (node.LayoutMode == "HORIZONTAL")
                {
                    childControlHeight = true;
                }
                else if (node.LayoutMode == "VERTICAL")
                {
                    childControlWidth = true;
                }
            }

            return (childControlWidth, childControlHeight);
        }

        private static bool GetPrimaryAxisSpaceBetween(FigmaNode node)
        {
            return node.LayoutMode == "VERTICAL"
                ? node.PrimaryAxisAlignItems == "SPACE_BETWEEN"
                : node.PrimaryAxisAlignItems == "SPACE_BETWEEN";
        }

        private static bool GetCounterAxisSpaceBetween(FigmaNode node)
        {
            return node.LayoutMode == "VERTICAL"
                ? node.CounterAxisAlignItems == "SPACE_BETWEEN"
                : node.CounterAxisAlignItems == "SPACE_BETWEEN";
        }

        private static TextAnchor GetChildAlignment(FigmaNode node)
        {
            string primary = node.PrimaryAxisAlignItems ?? "NONE";
            string counter = node.CounterAxisAlignItems ?? "NONE";

            if (node.LayoutMode == "HORIZONTAL")
            {
                return (primary, counter) switch
                {
                    ("CENTER", "CENTER") => TextAnchor.MiddleCenter,
                    ("CENTER", "MAX") => TextAnchor.LowerCenter,
                    ("CENTER", _) => TextAnchor.UpperCenter,
                    ("MAX", "CENTER") => TextAnchor.MiddleRight,
                    ("MAX", "MAX") => TextAnchor.LowerRight,
                    ("MAX", _) => TextAnchor.UpperRight,
                    (_, "CENTER") => TextAnchor.MiddleLeft,
                    (_, "MAX") => TextAnchor.LowerLeft,
                    _ => TextAnchor.UpperLeft,
                };
            }

            return (primary, counter) switch
            {
                ("CENTER", "CENTER") => TextAnchor.MiddleCenter,
                ("CENTER", "MAX") => TextAnchor.MiddleRight,
                ("CENTER", _) => TextAnchor.MiddleLeft,
                ("MAX", "CENTER") => TextAnchor.LowerCenter,
                ("MAX", "MAX") => TextAnchor.LowerRight,
                ("MAX", _) => TextAnchor.LowerLeft,
                (_, "CENTER") => TextAnchor.UpperCenter,
                (_, "MAX") => TextAnchor.UpperRight,
                _ => TextAnchor.UpperLeft,
            };
        }

        private static WrapLayoutGroup.Alignment GetWrapContentAlignment(string? value)
        {
            return value switch
            {
                "CENTER" => WrapLayoutGroup.Alignment.Center,
                "MAX" => WrapLayoutGroup.Alignment.End,
                _ => WrapLayoutGroup.Alignment.Start,
            };
        }
    }
}
