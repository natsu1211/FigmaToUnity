namespace FigmaToUnity.Core
{
    // IR v2: renamed from Unity-component-specific names to engine-neutral semantics.
    // Slice9 -> NinePatch, ClipRect -> ClipBounds, ContentSizeFitter -> HugContents.
    // CanvasGroup removed; backends derive transparency from Opacity < 1 directly.
    public enum NodeTag
    {
        Prefab,
        Text,
        Image,
        NinePatch,
        AutoLayout,
        AutoLayoutWrap,
        Button,
        Scroll,
        Mask,
        ClipBounds,
        AspectRatio,
        HugContents,
        Container,
        Ignore,
        PrefabRef
    }
}
