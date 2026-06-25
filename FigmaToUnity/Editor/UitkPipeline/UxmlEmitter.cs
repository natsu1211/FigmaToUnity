using System.Text;
using System.Xml;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor.UitkPipeline
{
    // Walks a tagged FigmaNode tree and emits a UXML document. Element type is
    // chosen by NodeTag (Label / ScrollView / VisualElement). Every managed
    // node carries figma-id + figma-hash; the root also carries figma-root-id
    // so the diff updater can locate the root quickly.
    internal static class UxmlEmitter
    {
        private const string UiNamespace = "UnityEngine.UIElements";
        private const string UieNamespace = "UnityEditor.UIElements";

        public static string Emit(FigmaNode root, string ussSrcRelativeToUxml)
        {
            StringBuilder sb = new();
            // OmitXmlDeclaration = true matches UI Builder's output and avoids
            // declaring utf-16 while File.WriteAllText writes UTF-8 bytes.
            XmlWriterSettings xmlSettings = new()
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = true,
                Encoding = Encoding.UTF8,
            };

            using (XmlWriter writer = XmlWriter.Create(sb, xmlSettings))
            {
                writer.WriteStartElement("ui", "UXML", UiNamespace);
                writer.WriteAttributeString("xmlns", "uie", null, UieNamespace);
                writer.WriteAttributeString("engine", UiNamespace);
                writer.WriteAttributeString("editor", UieNamespace);
                writer.WriteAttributeString("editor-extension-mode", "False");

                // Unity's stylesheet include is a document-level directive, not
                // a VisualElement. UI Builder expects it as unqualified <Style>.
                writer.WriteStartElement("Style");
                writer.WriteAttributeString("src", ussSrcRelativeToUxml);
                writer.WriteEndElement();

                EmitNode(writer, root, isRoot: true);

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
        }

        private static void EmitNode(XmlWriter writer, FigmaNode node, bool isRoot)
        {
            if (node.IgnoreNode || !UitkRenderPolicy.ShouldEmitElement(node.Design))
            {
                return;
            }

            (string elementName, string roleClass) = ResolveElementType(node);
            writer.WriteStartElement("ui", elementName, UiNamespace);

            string sanitizedName = string.IsNullOrWhiteSpace(node.Name)
                ? UitkStyleMapper.ToUssId(node.Id)
                : FigmaNameSanitizer.Sanitize(node.Name);

            writer.WriteAttributeString("name", UitkStyleMapper.ToUssId(node.Id));
            writer.WriteAttributeString("figma-name", sanitizedName);
            writer.WriteAttributeString("figma-id", node.Id);
            writer.WriteAttributeString("figma-hash", node.NodeHash.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (isRoot)
            {
                writer.WriteAttributeString("figma-root-id", node.Id);
            }

            writer.WriteAttributeString("class", roleClass);

            bool isLabel = elementName == "Label";
            if (isLabel && !string.IsNullOrEmpty(node.Characters))
            {
                writer.WriteAttributeString("text", node.Characters);
            }

            if (node.Children != null && !isLabel)
            {
                foreach (FigmaNode child in node.Children)
                {
                    EmitNode(writer, child, isRoot: false);
                }
            }

            writer.WriteEndElement();
        }

        private static (string element, string roleClass) ResolveElementType(FigmaNode node)
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
    }
}
