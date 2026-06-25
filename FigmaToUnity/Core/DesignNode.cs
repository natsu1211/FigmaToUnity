using System.Collections.Generic;
using Newtonsoft.Json;

namespace FigmaToUnity.Core
{
    public sealed class DesignNode
    {
        // ── JSON-deserialized properties ──────────────────────────────────

        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("componentId")]
        public string? ComponentId { get; set; }

        [JsonProperty("visible")]
        public bool? Visible { get; set; }

        [JsonProperty("children")]
        public List<DesignNode>? Children { get; set; }

        [JsonProperty("absoluteBoundingBox")]
        public BoundingBox? AbsoluteBoundingBox { get; set; }

        [JsonProperty("absoluteRenderBounds")]
        public BoundingBox? AbsoluteRenderBounds { get; set; }

        [JsonProperty("constraints")]
        public Constraints? Constraints { get; set; }

        [JsonProperty("relativeTransform")]
        public List<List<float?>>? RelativeTransform { get; set; }

        [JsonProperty("rotation")]
        public float? Rotation { get; set; }

        [JsonProperty("size")]
        public Float2 Size { get; set; }

        [JsonProperty("fills")]
        public List<Paint>? Fills { get; set; }

        [JsonProperty("strokes")]
        public List<Paint>? Strokes { get; set; }

        [JsonProperty("strokeWeight")]
        public float? StrokeWeight { get; set; }

        [JsonProperty("individualStrokeWeights")]
        public IndividualStrokeWeights? IndividualStrokeWeights { get; set; }

        [JsonProperty("strokeAlign")]
        public string? StrokeAlign { get; set; }

        [JsonProperty("cornerRadius")]
        public float? CornerRadius { get; set; }

        [JsonProperty("rectangleCornerRadii")]
        public List<float>? RectangleCornerRadii { get; set; }

        [JsonProperty("effects")]
        public List<Effect>? Effects { get; set; }

        [JsonProperty("opacity")]
        public float? Opacity { get; set; }

        [JsonProperty("characters")]
        public string? Characters { get; set; }

        [JsonProperty("style")]
        public TextStyle? Style { get; set; }

        [JsonProperty("styleOverrideTable")]
        public Dictionary<string, TextStyle>? StyleOverrideTable { get; set; }

        [JsonProperty("characterStyleOverrides")]
        public List<int>? CharacterStyleOverrides { get; set; }

        [JsonProperty("layoutMode")]
        public string? LayoutMode { get; set; }

        [JsonProperty("layoutWrap")]
        public string? LayoutWrap { get; set; }

        [JsonProperty("layoutPositioning")]
        public string? LayoutPositioning { get; set; }

        [JsonProperty("layoutSizingHorizontal")]
        public string? LayoutSizingHorizontal { get; set; }

        [JsonProperty("layoutSizingVertical")]
        public string? LayoutSizingVertical { get; set; }

        [JsonProperty("itemSpacing")]
        public float? ItemSpacing { get; set; }

        [JsonProperty("counterAxisSpacing")]
        public float? CounterAxisSpacing { get; set; }

        [JsonProperty("paddingLeft")]
        public float? PaddingLeft { get; set; }

        [JsonProperty("paddingRight")]
        public float? PaddingRight { get; set; }

        [JsonProperty("paddingTop")]
        public float? PaddingTop { get; set; }

        [JsonProperty("paddingBottom")]
        public float? PaddingBottom { get; set; }

        [JsonProperty("primaryAxisSizingMode")]
        public string? PrimaryAxisSizingMode { get; set; }

        [JsonProperty("counterAxisSizingMode")]
        public string? CounterAxisSizingMode { get; set; }

        [JsonProperty("primaryAxisAlignItems")]
        public string? PrimaryAxisAlignItems { get; set; }

        [JsonProperty("counterAxisAlignItems")]
        public string? CounterAxisAlignItems { get; set; }

        [JsonProperty("counterAxisAlignContent")]
        public string? CounterAxisAlignContent { get; set; }

        [JsonProperty("layoutAlign")]
        public string? LayoutAlign { get; set; }

        [JsonProperty("layoutGrow")]
        public float? LayoutGrow { get; set; }

        [JsonProperty("minWidth")]
        public float? MinWidth { get; set; }

        [JsonProperty("maxWidth")]
        public float? MaxWidth { get; set; }

        [JsonProperty("minHeight")]
        public float? MinHeight { get; set; }

        [JsonProperty("maxHeight")]
        public float? MaxHeight { get; set; }

        [JsonProperty("clipsContent")]
        public bool? ClipsContent { get; set; }

        [JsonProperty("isMask")]
        public bool? IsMask { get; set; }

        [JsonProperty("preserveRatio")]
        public bool? PreserveRatio { get; set; }

        [JsonProperty("overflowDirection")]
        public string? OverflowDirection { get; set; }

        // ── Processing-state container (IR v2: separated from Figma JSON fields) ──

        [JsonProperty("annotations")]
        public NodeAnnotations Annotations { get; set; } = new();

        // Parent is reconstructed by tree traversal to avoid cycles in JSON output.
        [JsonIgnore]
        public DesignNode? Parent { get; set; }

        // Forwarding accessors keep existing call sites and tests working while the
        // canonical storage lives on Annotations.
        [JsonIgnore]
        public HashSet<NodeTag> Tags => Annotations.Tags;

        [JsonIgnore]
        public int NodeHash
        {
            get => Annotations.NodeHash;
            set => Annotations.NodeHash = value;
        }

        [JsonIgnore]
        public bool ForceImage
        {
            get => Annotations.ForceImage;
            set => Annotations.ForceImage = value;
        }

        [JsonIgnore]
        public bool ForceContainer
        {
            get => Annotations.ForceContainer;
            set => Annotations.ForceContainer = value;
        }

        [JsonIgnore]
        public bool IgnoreNode
        {
            get => Annotations.IgnoreNode;
            set => Annotations.IgnoreNode = value;
        }

        [JsonIgnore]
        public bool ExplicitPrefab
        {
            get => Annotations.ExplicitPrefab;
            set => Annotations.ExplicitPrefab = value;
        }

        public bool ShouldSerializeAnnotations() => !Annotations.IsEmpty;
    }
}
