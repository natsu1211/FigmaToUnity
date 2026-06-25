using Newtonsoft.Json;
using System.Collections.Generic;

namespace FigmaToUnity.Core
{
    public sealed class BoundingBox
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }
    }

    public sealed class Constraints
    {
        [JsonProperty("vertical")]
        public string? Vertical { get; set; }

        [JsonProperty("horizontal")]
        public string? Horizontal { get; set; }
    }

    public sealed class Paint
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("scaleMode")]
        public string? ScaleMode { get; set; }

        [JsonProperty("imageRef")]
        public string? ImageRef { get; set; }

        [JsonProperty("visible")]
        public bool? Visible { get; set; }

        [JsonProperty("opacity")]
        public float? Opacity { get; set; }

        [JsonProperty("color")]
        public FigmaColor? Color { get; set; }

        [JsonProperty("gradientStops")]
        public List<GradientStop>? GradientStops { get; set; }
    }

    public sealed class GradientStop
    {
        [JsonProperty("position")]
        public float Position { get; set; }

        [JsonProperty("color")]
        public FigmaColor? Color { get; set; }
    }

    public sealed class FigmaColor
    {
        [JsonProperty("r")]
        public float R { get; set; }

        [JsonProperty("g")]
        public float G { get; set; }

        [JsonProperty("b")]
        public float B { get; set; }

        [JsonProperty("a")]
        public float A { get; set; } = 1f;
    }

    public sealed class IndividualStrokeWeights
    {
        [JsonProperty("top")]
        public float? Top { get; set; }

        [JsonProperty("bottom")]
        public float? Bottom { get; set; }

        [JsonProperty("left")]
        public float? Left { get; set; }

        [JsonProperty("right")]
        public float? Right { get; set; }
    }

    public sealed class Effect
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("visible")]
        public bool? Visible { get; set; }
    }

    public sealed class TextStyle
    {
        [JsonProperty("fontFamily")]
        public string? FontFamily { get; set; }

        [JsonProperty("fontWeight")]
        public float? FontWeight { get; set; }

        [JsonProperty("fontSize")]
        public float FontSize { get; set; }

        [JsonProperty("textAlignHorizontal")]
        public string? TextAlignHorizontal { get; set; }

        [JsonProperty("textAlignVertical")]
        public string? TextAlignVertical { get; set; }

        [JsonProperty("letterSpacing")]
        public float? LetterSpacing { get; set; }

        [JsonProperty("lineHeightPx")]
        public float? LineHeightPx { get; set; }

        [JsonProperty("lineHeightPercentFontSize")]
        public float? LineHeightPercentFontSize { get; set; }

        [JsonProperty("textAutoResize")]
        public string? TextAutoResize { get; set; }

        [JsonProperty("textCase")]
        public string? TextCase { get; set; }

        [JsonProperty("textDecoration")]
        public string? TextDecoration { get; set; }

        [JsonProperty("italic")]
        public bool? Italic { get; set; }
    }
}
