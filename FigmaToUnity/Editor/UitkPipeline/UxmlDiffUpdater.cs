using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor.UitkPipeline
{
    // Surgical UXML rewriter for DiffUpdate mode. Reads an existing per-frame
    // UXML, walks the new IR tree, and updates importer-managed nodes in place
    // while preserving any element a user inserted by hand.
    //
    // A node is "importer-managed" iff it carries a `figma-id` attribute. The
    // diff matches IR nodes to existing elements by that id within the same
    // parent. Manual nodes (no `figma-id`) are kept in place using the
    // "anchor to nearest preceding figma sibling" rule documented in plan §9.
    //
    // Out of scope for v1:
    //   - Cross-parent reparenting detection (treated as remove + create).
    //   - Element-type changes triggered by tag flips (treated as remove + create).
    //   - USS surgical patches — the whole-frame .generated.uss is regenerated
    //     by UitkImportBackend, so this updater only touches UXML.
    internal static class UxmlDiffUpdater
    {
        private const string UiNamespace = "UnityEngine.UIElements";
        private static readonly XNamespace Ui = UiNamespace;

        // Attribute names the importer owns. Everything else on an element is
        // assumed to be user-authored and is left untouched on update.
        private static readonly HashSet<string> ImporterAttributes = new()
        {
            "name",
            "figma-id",
            "figma-name",
            "figma-hash",
            "figma-root-id",
            "class",
            "text",
        };

        // Returns either the updated UXML, or null when the existing document
        // is not compatible with diff-update (different root, malformed, etc.)
        // and the caller should fall back to a full rewrite.
        public static string? TryUpdate(string existingUxml, FigmaNode newRoot, string wrapperUssRelativeToUxml)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Parse(existingUxml, LoadOptions.PreserveWhitespace);
            }
            catch (System.Xml.XmlException)
            {
                return null;
            }

            XElement? rootUxml = doc.Root;
            if (rootUxml == null || rootUxml.Name.LocalName != "UXML")
            {
                return null;
            }

            // First UI-namespace element under the root is the figma frame root.
            XElement? existingFrame = null;
            foreach (XElement child in rootUxml.Elements())
            {
                if (child.Name.Namespace == Ui)
                {
                    existingFrame = child;
                    break;
                }
            }

            if (existingFrame == null)
            {
                return null;
            }

            string? existingRootId = (string?)existingFrame.Attribute("figma-root-id")
                ?? (string?)existingFrame.Attribute("figma-id");
            if (!string.Equals(existingRootId, newRoot.Id, System.StringComparison.Ordinal))
            {
                return null;
            }

            EnsureStyleSrc(rootUxml, wrapperUssRelativeToUxml);

            string requiredElementName = ResolveElementType(newRoot).Element;
            if (existingFrame.Name.LocalName != requiredElementName)
            {
                // Root element type changed (e.g., Frame → Text). Re-create the
                // root subtree from scratch; preserve no descendant state.
                existingFrame.ReplaceWith(BuildFreshElement(newRoot, isRoot: true));
            }
            else
            {
                UpdateNodeInPlace(existingFrame, newRoot, isRoot: true);
                ReconcileChildren(existingFrame, newRoot);
            }

            // Drop any XML declaration the existing file carried. Older builds of
            // UxmlEmitter emitted `<?xml version="1.0" encoding="utf-16"?>` which
            // Unity rejects when the file is then written as UTF-8 bytes. Stripping
            // the declaration on every diff-update heals those files in place.
            return doc.ToString(SaveOptions.None);
        }

        private static void ReconcileChildren(XElement parentXml, FigmaNode parentNode)
        {
            // Snapshot of existing children, separated by managed-vs-manual so we
            // can fold manual nodes back in via their anchor relationship.
            List<(XElement element, string? anchorFigmaId)> manualEntries = new();
            Dictionary<string, XElement> existingManaged = new(System.StringComparer.Ordinal);

            string? lastFigmaIdSeen = null;
            foreach (XElement child in parentXml.Elements())
            {
                string? figmaId = (string?)child.Attribute("figma-id");
                if (!string.IsNullOrEmpty(figmaId))
                {
                    existingManaged[figmaId!] = child;
                    lastFigmaIdSeen = figmaId;
                }
                else
                {
                    manualEntries.Add((child, lastFigmaIdSeen));
                }
            }

            // Build the new child sequence in IR order. For each IR child, take the
            // existing element (updated in place) when figma-id + element type
            // matches; otherwise build fresh.
            List<XElement> updatedManaged = new();
            List<string> updatedManagedFigmaIds = new();

            if (parentNode.Children != null)
            {
                foreach (FigmaNode childNode in parentNode.Children)
                {
                    if (childNode.IgnoreNode)
                    {
                        continue;
                    }

                    if (!UitkRenderPolicy.ShouldEmitElement(childNode.Design))
                    {
                        continue;
                    }

                    string requiredType = ResolveElementType(childNode).Element;
                    if (existingManaged.TryGetValue(childNode.Id, out XElement? existing)
                        && existing.Name.LocalName == requiredType)
                    {
                        UpdateNodeInPlace(existing, childNode, isRoot: false);
                        ReconcileChildren(existing, childNode);
                        updatedManaged.Add(existing);
                        updatedManagedFigmaIds.Add(childNode.Id);
                    }
                    else
                    {
                        // New, or type-change (treat as replace).
                        XElement fresh = BuildFreshElement(childNode, isRoot: false);
                        updatedManaged.Add(fresh);
                        updatedManagedFigmaIds.Add(childNode.Id);
                    }
                }
            }

            // Splice manual entries back at their anchor positions. If the anchor
            // figma-id is still in the new sequence, the manual element goes
            // directly after it. If the anchor was removed or never existed,
            // append to the tail so manual nodes are never silently dropped.
            List<XElement> finalChildren = new(updatedManaged);
            foreach ((XElement manualElement, string? anchorFigmaId) in manualEntries)
            {
                int insertAt;
                if (anchorFigmaId == null)
                {
                    insertAt = 0;
                }
                else
                {
                    int anchorIdx = updatedManagedFigmaIds.IndexOf(anchorFigmaId);
                    insertAt = anchorIdx >= 0
                        ? finalChildren.IndexOf(updatedManaged[anchorIdx]) + 1
                        : finalChildren.Count;
                }

                if (insertAt > finalChildren.Count)
                {
                    insertAt = finalChildren.Count;
                }

                finalChildren.Insert(insertAt, manualElement);
            }

            parentXml.RemoveNodes();
            foreach (XElement child in finalChildren)
            {
                parentXml.Add(child);
            }
        }

        private static void UpdateNodeInPlace(XElement element, FigmaNode node, bool isRoot)
        {
            (string _, string roleClass) = ResolveElementType(node);

            string sanitizedName = string.IsNullOrWhiteSpace(node.Name)
                ? UitkStyleMapper.ToUssId(node.Id)
                : FigmaNameSanitizer.Sanitize(node.Name);

            SetImporterAttribute(element, "name", UitkStyleMapper.ToUssId(node.Id));
            SetImporterAttribute(element, "figma-name", sanitizedName);
            SetImporterAttribute(element, "figma-id", node.Id);
            SetImporterAttribute(element, "figma-hash", node.NodeHash.ToString(CultureInfo.InvariantCulture));
            SetImporterAttribute(element, "class", roleClass);

            if (isRoot)
            {
                SetImporterAttribute(element, "figma-root-id", node.Id);
            }
            else
            {
                element.Attribute("figma-root-id")?.Remove();
            }

            // text attribute lifecycle matches UxmlEmitter's shortcut rules.
            bool wantsTextAttribute = false;
            string textValue = string.Empty;

            if (element.Name.LocalName == "Label" && !string.IsNullOrEmpty(node.Characters))
            {
                wantsTextAttribute = true;
                textValue = node.Characters!;
            }
            if (wantsTextAttribute)
            {
                SetImporterAttribute(element, "text", textValue);
            }
            else
            {
                element.Attribute("text")?.Remove();
            }
        }

        private static XElement BuildFreshElement(FigmaNode node, bool isRoot)
        {
            (string elementName, string roleClass) = ResolveElementType(node);
            XElement element = new(Ui + elementName);

            string sanitizedName = string.IsNullOrWhiteSpace(node.Name)
                ? UitkStyleMapper.ToUssId(node.Id)
                : FigmaNameSanitizer.Sanitize(node.Name);

            element.SetAttributeValue("name", UitkStyleMapper.ToUssId(node.Id));
            element.SetAttributeValue("figma-name", sanitizedName);
            element.SetAttributeValue("figma-id", node.Id);
            element.SetAttributeValue("figma-hash", node.NodeHash.ToString(CultureInfo.InvariantCulture));
            if (isRoot)
            {
                element.SetAttributeValue("figma-root-id", node.Id);
            }

            element.SetAttributeValue("class", roleClass);

            bool isLabel = elementName == "Label";

            if (isLabel && !string.IsNullOrEmpty(node.Characters))
            {
                element.SetAttributeValue("text", node.Characters);
            }

            if (node.Children != null && !isLabel)
            {
                foreach (FigmaNode child in node.Children)
                {
                    if (child.IgnoreNode || !UitkRenderPolicy.ShouldEmitElement(child.Design))
                    {
                        continue;
                    }

                    element.Add(BuildFreshElement(child, isRoot: false));
                }
            }

            return element;
        }

        private static void EnsureStyleSrc(XElement rootUxml, string wrapperUssRelativeToUxml)
        {
            // Accept either <Style> or the older generated <ui:Style>. Normalize
            // to unqualified <Style> because UI Builder treats <ui:Style> as an
            // unsupported UIElements visual type.
            foreach (XElement styleElement in rootUxml.Elements())
            {
                if (styleElement.Name.LocalName != "Style")
                {
                    continue;
                }

                if (styleElement.Name.Namespace != Ui && styleElement.Name.Namespace != XNamespace.None)
                {
                    continue;
                }

                styleElement.Name = "Style";
                styleElement.SetAttributeValue("src", wrapperUssRelativeToUxml);
                return;
            }

            // No Style child in the existing doc - insert the UI Builder-compatible
            // document-level stylesheet directive before the first UI element.
            XElement styleNode = new("Style");
            styleNode.SetAttributeValue("src", wrapperUssRelativeToUxml);

            XElement? firstUi = null;
            foreach (XElement child in rootUxml.Elements())
            {
                if (child.Name.Namespace == Ui)
                {
                    firstUi = child;
                    break;
                }
            }

            if (firstUi != null)
            {
                firstUi.AddBeforeSelf(styleNode);
            }
            else
            {
                rootUxml.Add(styleNode);
            }
        }

        private static (string Element, string RoleClass) ResolveElementType(FigmaNode node)
        {
            string element = UitkRenderPolicy.ResolveElementKind(node.Design) switch
            {
                UitkElementKind.Label => "Label",
                UitkElementKind.ScrollView => "ScrollView",
                _ => "VisualElement",
            };

            if (node.Tags.Contains(NodeTag.Button))
            {
                return (element, "figma-button");
            }

            if (node.Tags.Contains(NodeTag.Scroll))
            {
                return (element, "figma-scroll");
            }

            if (node.Tags.Contains(NodeTag.Text))
            {
                return (element, "figma-text");
            }

            if (node.Tags.Contains(NodeTag.Image))
            {
                return (element, "figma-image");
            }

            return (element, "figma-frame");
        }

        private static void SetImporterAttribute(XElement element, string name, string value)
        {
            element.SetAttributeValue(name, value);
        }
    }
}
