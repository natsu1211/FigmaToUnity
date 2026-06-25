using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.SharedPipeline;
using FigmaToUnity.Runtime;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class DiffPlanner
    {
        public DiffPlan Create(IReadOnlyList<FigmaNode> rootNodes, SceneSyncIndex sceneIndex)
        {
            DiffPlan plan = new();

            foreach (FigmaNode rootNode in rootNodes)
            {
                if (rootNode.IgnoreNode)
                {
                    continue;
                }

                FrameDiffPlan framePlan = new(rootNode);
                plan.AddFrame(framePlan);

                if (sceneIndex.IsInvalidRoot(rootNode.Id))
                {
                    MarkFallback(framePlan, "Scene sync markers are duplicated or invalid.");
                    continue;
                }

                if (!sceneIndex.TryGetCanvasMarker(rootNode.Id, out FigmaSyncMarker? canvasMarker) || canvasMarker == null)
                {
                    MarkFallback(framePlan, "No existing synced canvas was found for this frame.");
                    continue;
                }

                if (!sceneIndex.TryGetMarker(rootNode.Id, out FigmaSyncMarker? rootMarker) || rootMarker == null || rootMarker.RootFrameNodeId != rootNode.Id)
                {
                    MarkFallback(framePlan, "The root frame marker is missing, so diff update is unsafe.");
                    continue;
                }

                Dictionary<string, FigmaNode> figmaNodes = new(System.StringComparer.Ordinal);
                CollectNodes(rootNode, figmaNodes);

                foreach (FigmaNode node in figmaNodes.Values)
                {
                    DiffAction action = DiffAction.None;
                    if (!sceneIndex.TryGetMarker(node.Id, out FigmaSyncMarker? marker) || marker == null || marker.RootFrameNodeId != rootNode.Id)
                    {
                        action |= DiffAction.New;
                    }
                    else
                    {
                        if (marker.NodeHash != node.NodeHash || !string.Equals(marker.NodeType, node.Type, System.StringComparison.Ordinal))
                        {
                            action |= DiffAction.Changed;
                        }

                        string expectedParentId = node.Parent?.Id ?? string.Empty;
                        if (!string.Equals(marker.ParentNodeId, expectedParentId, System.StringComparison.Ordinal))
                        {
                            action |= DiffAction.Reparented;
                        }

                        if (marker.transform.GetSiblingIndex() != GetExpectedSiblingIndex(node))
                        {
                            action |= DiffAction.Reordered;
                        }
                    }

                    if (action != DiffAction.None)
                    {
                        framePlan.NodeDiffs[node.Id] = new NodeDiff(node, action)
                        {
                            RequiresSpriteRefresh = SpriteImporter.ShouldDownloadSprite(node) && action.HasFlag(DiffAction.Changed)
                        };
                    }
                }

                foreach (FigmaSyncMarker marker in sceneIndex.GetMarkersForRoot(rootNode.Id))
                {
                    if (marker == null || marker.IsSyntheticCanvas || string.IsNullOrWhiteSpace(marker.NodeId))
                    {
                        continue;
                    }

                    if (!figmaNodes.ContainsKey(marker.NodeId))
                    {
                        framePlan.RemovedNodeIds.Add(marker.NodeId);
                    }
                }
            }

            return plan;
        }

        private static void MarkFallback(FrameDiffPlan framePlan, string reason)
        {
            framePlan.FallbackToFullRebuild = true;
            framePlan.FallbackReason = reason;
            framePlan.NodeDiffs.Clear();
            framePlan.RemovedNodeIds.Clear();
        }

        private static void CollectNodes(FigmaNode node, Dictionary<string, FigmaNode> nodes)
        {
            if (node.IgnoreNode)
            {
                return;
            }

            nodes[node.Id] = node;
            if (node.Children == null)
            {
                return;
            }

            foreach (FigmaNode child in node.Children)
            {
                CollectNodes(child, nodes);
            }
        }

        private static int GetExpectedSiblingIndex(FigmaNode node)
        {
            if (node.Parent?.Children == null)
            {
                return 0;
            }

            int effectiveIndex = 0;
            foreach (FigmaNode sibling in node.Parent.Children)
            {
                if (ReferenceEquals(sibling, node))
                {
                    return effectiveIndex;
                }

                if (!sibling.IgnoreNode)
                {
                    effectiveIndex++;
                }
            }

            return 0;
        }
    }
}
