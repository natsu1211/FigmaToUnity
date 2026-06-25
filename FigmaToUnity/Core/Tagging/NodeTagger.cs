namespace FigmaToUnity.Core
{
    public sealed class NodeTagger
    {
        public void TagTree(DesignNode root)
        {
            TagRecursive(root, null);
        }

        private void TagRecursive(DesignNode node, DesignNode? parent)
        {
            node.Parent = parent;
            ApplyManualTags(node);
            ApplyFigmaTags(node);
            ApplySmartTags(node, parent);

            if (node.Children == null)
            {
                return;
            }

            foreach (DesignNode child in node.Children)
            {
                TagRecursive(child, node);
            }
        }

        private static void ApplyManualTags(DesignNode node)
        {
            foreach (NodeTag tag in ManualTagParser.Parse(node.Name))
            {
                node.Tags.Add(tag);
            }

            if (node.Tags.Contains(NodeTag.Ignore))
            {
                node.IgnoreNode = true;
            }

            if (node.Tags.Contains(NodeTag.Prefab))
            {
                node.ExplicitPrefab = true;
            }

            if (node.Tags.Contains(NodeTag.Image))
            {
                node.ForceImage = true;
            }

            if (node.Tags.Contains(NodeTag.NinePatch))
            {
                node.Tags.Add(NodeTag.Image);
                node.ForceImage = true;
                IgnoreDescendants(node);
            }

            if (node.Tags.Contains(NodeTag.Container))
            {
                node.ForceContainer = true;
            }

            // "#use:<path-or-name>" binds this node to an existing project prefab. The
            // node itself becomes a positioned anchor; its Figma subtree is dropped so
            // nothing is built or fetched for it (the external prefab replaces it).
            if (ManualTagParser.TryParseExternalPrefab(node.Name, out string externalPrefabReference))
            {
                node.Tags.Add(NodeTag.PrefabRef);
                node.ExternalPrefabPath = externalPrefabReference;
                IgnoreDescendants(node);
            }
        }

        private static void ApplyFigmaTags(DesignNode node)
        {
            // A #use: node is replaced wholesale by an external prefab, so it must not pick
            // up Image/Text/AutoLayout/etc. tags that would add components to the anchor.
            if (node.Tags.Contains(NodeTag.PrefabRef))
            {
                return;
            }

            if (node.Type == "TEXT")
            {
                node.Tags.Add(NodeTag.Text);
            }

            if (node.LayoutMode is "HORIZONTAL" or "VERTICAL")
            {
                node.Tags.Add(NodeTag.AutoLayout);
            }

            if (node.LayoutWrap == "WRAP")
            {
                node.Tags.Add(NodeTag.AutoLayoutWrap);
            }

            if (node.ClipsContent == true)
            {
                node.Tags.Add(NodeTag.ClipBounds);
            }

            if (node.IsMask == true)
            {
                node.Tags.Add(NodeTag.Mask);
            }

            if (node.PreserveRatio == true)
            {
                node.Tags.Add(NodeTag.AspectRatio);
            }

            if (node.Style?.TextAutoResize is "HEIGHT" or "WIDTH_AND_HEIGHT")
            {
                node.Tags.Add(NodeTag.HugContents);
            }

            if (TaggingRules.HasVisiblePaints(node) || node.Type is "VECTOR" or "LINE" or "ELLIPSE" or "BOOLEAN_OPERATION" or "STAR" or "REGULAR_POLYGON")
            {
                node.Tags.Add(NodeTag.Image);
            }
        }

        private static void ApplySmartTags(DesignNode node, DesignNode? parent)
        {
            if (node.Tags.Contains(NodeTag.PrefabRef))
            {
                return;
            }

            if (!TaggingRules.IsVisible(node))
            {
                node.IgnoreNode = true;
            }

            if (!node.Tags.Contains(NodeTag.Button) && IsLikelyButton(node))
            {
                node.Tags.Add(NodeTag.Button);
            }

            if (!node.Tags.Contains(NodeTag.Image) && IsLikelyDecorativeGroup(node))
            {
                node.Tags.Add(NodeTag.Image);
                node.ForceImage = true;
                IgnoreDescendants(node);
            }

            if (!node.Tags.Contains(NodeTag.NinePatch) && TaggingRules.IsNineSlice(node))
            {
                node.Tags.Add(NodeTag.NinePatch);
                node.Tags.Add(NodeTag.Image);
                node.ForceImage = true;
                IgnoreDescendants(node);
            }

            if (parent == null || node.ForceImage)
            {
                return;
            }

            if (node.Type == "VECTOR" && parent.Type == "FRAME")
            {
                node.ForceImage = true;
                node.Tags.Add(NodeTag.Image);
            }
        }

        private static void IgnoreDescendants(DesignNode node)
        {
            if (node.Children == null)
            {
                return;
            }

            foreach (DesignNode child in node.Children)
            {
                child.IgnoreNode = true;
                IgnoreDescendants(child);
            }
        }

        private static bool IsLikelyButton(DesignNode node)
        {
            if (node.IgnoreNode || node.Type == "TEXT")
            {
                return false;
            }

            if (TaggingRules.HasButtonStateChildren(node))
            {
                return true;
            }

            if (!TaggingRules.IsLikelyButtonName(node.Name))
            {
                return false;
            }

            return TaggingRules.HasGraphicContent(node) || HasGraphicDescendant(node);
        }

        private static bool IsLikelyDecorativeGroup(DesignNode node)
        {
            if (node.IgnoreNode || node.Type == "TEXT" || node.Children == null || node.Children.Count == 0)
            {
                return false;
            }

            return TaggingRules.IsLikelyDecorativeGroupName(node.Name) && HasGraphicDescendant(node);
        }

        private static bool HasGraphicDescendant(DesignNode node)
        {
            if (node.Children == null)
            {
                return false;
            }

            foreach (DesignNode child in node.Children)
            {
                if (TaggingRules.HasGraphicContent(child) || HasGraphicDescendant(child))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
