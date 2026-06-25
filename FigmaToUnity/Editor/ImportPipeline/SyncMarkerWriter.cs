using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal static class SyncMarkerWriter
    {
        private const string SyntheticCanvasNodeType = "CANVAS";

        public static void WriteMarkers(IReadOnlyList<FigmaNode> rootNodes, string fileKey)
        {
            foreach (FigmaNode rootNode in rootNodes)
            {
                if (rootNode.IgnoreNode || rootNode.GameObject == null)
                {
                    continue;
                }

                Transform? canvasTransform = rootNode.GameObject.transform.parent;
                if (canvasTransform != null)
                {
                    WriteCanvasMarker(canvasTransform.gameObject, fileKey, rootNode);
                }

                WriteNodeMarkersRecursive(rootNode, fileKey, rootNode.Id);
            }
        }

        private static void WriteCanvasMarker(GameObject canvasObject, string fileKey, FigmaNode rootNode)
        {
            FigmaSyncMarker marker = EnsureMarker(canvasObject);
            marker.FileKey = fileKey;
            marker.RootFrameNodeId = rootNode.Id;
            marker.NodeId = $"canvas:{rootNode.Id}";
            marker.ParentNodeId = string.Empty;
            marker.ComponentId = string.Empty;
            marker.NodeType = SyntheticCanvasNodeType;
            marker.StableObjectName = canvasObject.name;
            marker.NodeHash = 0;
            marker.IsSyntheticCanvas = true;
        }

        private static void WriteNodeMarkersRecursive(FigmaNode node, string fileKey, string rootFrameNodeId)
        {
            if (node.GameObject == null)
            {
                return;
            }

            FigmaSyncMarker marker = EnsureMarker(node.GameObject);
            marker.FileKey = fileKey;
            marker.RootFrameNodeId = rootFrameNodeId;
            marker.NodeId = node.Id;
            marker.ParentNodeId = node.Parent?.Id ?? string.Empty;
            marker.ComponentId = node.ComponentId ?? string.Empty;
            marker.NodeType = node.Type;
            marker.StableObjectName = node.StableObjectName;
            marker.NodeHash = node.NodeHash;
            marker.IsSyntheticCanvas = false;

            if (node.Children == null)
            {
                return;
            }

            foreach (FigmaNode child in node.Children)
            {
                WriteNodeMarkersRecursive(child, fileKey, rootFrameNodeId);
            }
        }

        private static FigmaSyncMarker EnsureMarker(GameObject gameObject)
        {
            FigmaSyncMarker marker = gameObject.GetComponent<FigmaSyncMarker>();
            if (marker == null)
            {
                marker = gameObject.AddComponent<FigmaSyncMarker>();
            }

            return marker;
        }
    }
}
