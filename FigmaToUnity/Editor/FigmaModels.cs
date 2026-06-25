using System.Collections.Generic;
using FigmaToUnity.Core;
using UnityEngine;

namespace FigmaToUnity.Editor
{
    internal sealed class FigmaNode
    {
        public FigmaNode(DesignNode design)
        {
            Design = design;
        }

        public DesignNode Design { get; }

        // Unity runtime state - populated during generation phase

        public FigmaNode? Parent { get; set; }

        public List<FigmaNode>? Children { get; set; }

        public GameObject? GameObject { get; set; }

        public RectTransform? RectTransform { get; set; }

        public Sprite? Sprite { get; set; }

        public Vector4 SpriteBorder { get; set; }

        public string AssetPath { get; set; } = string.Empty;

        public string StableObjectName { get; set; } = string.Empty;

        // Proxy properties - identity

        public string Id => Design.Id;

        public string Name => Design.Name;

        public string Type => Design.Type;

        public string? ComponentId => Design.ComponentId;

        // Proxy properties - semantic (read-write for Editor pipeline compatibility)

        public HashSet<NodeTag> Tags => Design.Tags;

        public bool ForceImage { get => Design.ForceImage; set => Design.ForceImage = value; }

        public bool ForceContainer { get => Design.ForceContainer; set => Design.ForceContainer = value; }

        public bool IgnoreNode { get => Design.IgnoreNode; set => Design.IgnoreNode = value; }

        public bool ExplicitPrefab { get => Design.ExplicitPrefab; set => Design.ExplicitPrefab = value; }

        public string? ExternalPrefabPath { get => Design.ExternalPrefabPath; set => Design.ExternalPrefabPath = value; }

        public int NodeHash { get => Design.NodeHash; set => Design.NodeHash = value; }

        // Proxy properties - geometry

        public BoundingBox? AbsoluteBoundingBox => Design.AbsoluteBoundingBox;

        public BoundingBox? AbsoluteRenderBounds => Design.AbsoluteRenderBounds;

        public Constraints? Constraints => Design.Constraints;

        public List<List<float?>>? RelativeTransform => Design.RelativeTransform;

        public float? Rotation => Design.Rotation;

        public Vector2 Size => new(Design.Size.X, Design.Size.Y);

        // Proxy properties - visual

        public bool? Visible => Design.Visible;

        public float? Opacity => Design.Opacity;

        public List<Paint>? Fills => Design.Fills;

        public List<Paint>? Strokes => Design.Strokes;

        public float? StrokeWeight => Design.StrokeWeight;

        public IndividualStrokeWeights? IndividualStrokeWeights => Design.IndividualStrokeWeights;

        public string? StrokeAlign => Design.StrokeAlign;

        public float? CornerRadius => Design.CornerRadius;

        public List<float>? RectangleCornerRadii => Design.RectangleCornerRadii;

        public List<Effect>? Effects => Design.Effects;

        // Proxy properties - text

        public string? Characters => Design.Characters;

        public TextStyle? Style => Design.Style;

        public Dictionary<string, TextStyle>? StyleOverrideTable => Design.StyleOverrideTable;

        public List<int>? CharacterStyleOverrides => Design.CharacterStyleOverrides;

        // Proxy properties - layout

        public string? LayoutMode => Design.LayoutMode;

        public string? LayoutWrap => Design.LayoutWrap;

        public string? LayoutPositioning => Design.LayoutPositioning;

        public string? LayoutSizingHorizontal => Design.LayoutSizingHorizontal;

        public string? LayoutSizingVertical => Design.LayoutSizingVertical;

        public float? ItemSpacing => Design.ItemSpacing;

        public float? CounterAxisSpacing => Design.CounterAxisSpacing;

        public float? PaddingLeft => Design.PaddingLeft;

        public float? PaddingRight => Design.PaddingRight;

        public float? PaddingTop => Design.PaddingTop;

        public float? PaddingBottom => Design.PaddingBottom;

        public string? PrimaryAxisSizingMode => Design.PrimaryAxisSizingMode;

        public string? CounterAxisSizingMode => Design.CounterAxisSizingMode;

        public string? PrimaryAxisAlignItems => Design.PrimaryAxisAlignItems;

        public string? CounterAxisAlignItems => Design.CounterAxisAlignItems;

        public string? CounterAxisAlignContent => Design.CounterAxisAlignContent;

        public string? LayoutAlign => Design.LayoutAlign;

        public float? LayoutGrow => Design.LayoutGrow;

        public float? MinWidth => Design.MinWidth;

        public float? MaxWidth => Design.MaxWidth;

        public float? MinHeight => Design.MinHeight;

        public float? MaxHeight => Design.MaxHeight;

        // Proxy properties - behavior

        public bool? ClipsContent => Design.ClipsContent;

        public bool? IsMask => Design.IsMask;

        public bool? PreserveRatio => Design.PreserveRatio;

        public string? OverflowDirection => Design.OverflowDirection;
    }

    internal static class FigmaNodeMapper
    {
        public static List<FigmaNode> MapAll(IReadOnlyList<DesignNode> designRoots)
        {
            List<FigmaNode> result = new(designRoots.Count);
            foreach (DesignNode root in designRoots)
            {
                result.Add(Map(root, null));
            }

            return result;
        }

        public static FigmaNode Map(DesignNode design, FigmaNode? parent)
        {
            FigmaNode node = new(design) { Parent = parent };

            if (design.Children != null && design.Children.Count > 0)
            {
                node.Children = new List<FigmaNode>(design.Children.Count);
                foreach (DesignNode child in design.Children)
                {
                    node.Children.Add(Map(child, node));
                }
            }

            return node;
        }
    }
}
