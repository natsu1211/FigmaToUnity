using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.ImportPipeline;
using FigmaToUnity.Editor.SharedPipeline;
using FigmaToUnity.Editor.State;
using FigmaToUnity.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.UguiPipeline
{
    // Runs steps 6–14 of the import pipeline (sprite fetch through layout rebuild) for
    // the UGUI backend. Pulled out of FigmaImportController so the controller can dispatch
    // to multiple backends.
    internal sealed class UguiImportBackend
    {
        private const int ApplyChunkSize = 64;

        private readonly HierarchyBuilder _hierarchyBuilder = new();
        private readonly TransformApplier _transformApplier = new();
        private readonly SpriteImporter _spriteImporter;
        private readonly ImageApplier _imageApplier = new();
        private readonly TextApplier _textApplier = new();
        private readonly LayoutApplier _layoutApplier = new();
        private readonly ComponentApplier _componentApplier = new();
        private readonly PrefabPipeline _prefabPipeline = new();
        private readonly DiffPlanner _diffPlanner = new();

        public UguiImportBackend(FigmaApiClient apiClient)
        {
            _spriteImporter = new SpriteImporter(apiClient);
        }

        public async Task ImportAsync(ImportContext context)
        {
            FigmaImportSession session = context.Session;
            ImportModeKind importMode = context.ImportMode;
            SceneSyncIndex? sceneSyncIndex = null;
            DiffPlan? diffPlan = null;

            if (importMode == ImportModeKind.DiffUpdate)
            {
                context.Report("Plan Diff", "Scanning existing scene sync markers.", 0, context.ImportedRootNodes.Count, false, true);
                sceneSyncIndex = SceneSyncIndex.BuildActiveSceneIndex();
                diffPlan = _diffPlanner.Create(context.ImportedRootNodes, sceneSyncIndex);
                LogDiffSummary(diffPlan, context.Log);
                DestroyFallbackCanvases(diffPlan, sceneSyncIndex);
                UnpackPrefabInstancesForDiff(diffPlan, sceneSyncIndex);
            }

            context.Report("Download Sprites", "Fetching image URLs and resolving sprite assets.", 0, 0, true, true);
            IProgress<SpriteImportProgress> spriteProgress = new Progress<SpriteImportProgress>(p =>
            {
                context.Report(p.Stage, $"{p.Stage} ({p.Current}/{p.Total}).", p.Current, p.Total, p.Total <= 0, true);
                if (!string.IsNullOrEmpty(p.Log))
                {
                    context.Log(p.Log!);
                }
            });
            int resolvedSprites = importMode == ImportModeKind.DiffUpdate && diffPlan != null
                ? await ImportSpritesForDiffAsync(context, diffPlan, spriteProgress)
                : await _spriteImporter.ImportAsync(
                    context.ImportedRootNodes,
                    session.Token,
                    session.FileKey,
                    session.OutputFolder,
                    context.CancellationToken,
                    spriteProgress);
            context.Log($"Resolved {resolvedSprites} sprite asset(s).");

            context.Report("Build Hierarchy", "Creating Canvas and RectTransform hierarchy.", 0, context.ImportedRootNodes.Count, false, true);
            List<FigmaNode> builtNodes = importMode == ImportModeKind.DiffUpdate && sceneSyncIndex != null && diffPlan != null
                ? _hierarchyBuilder.Build(context.ImportedRootNodes, session.FigmaFileName, sceneSyncIndex, diffPlan)
                : _hierarchyBuilder.Build(context.ImportedRootNodes, session.FigmaFileName);
            context.Log($"Prepared {builtNodes.Count} GameObject node(s) in the scene.");

            if (importMode == ImportModeKind.DiffUpdate && sceneSyncIndex != null && diffPlan != null)
            {
                DestroyRemovedNodes(diffPlan, sceneSyncIndex);
                CleanupGeneratedComponents(builtNodes, diffPlan);
            }

            await ApplyChunkedAsync(context, builtNodes, "Apply Transforms",
                "Applying RectTransforms ({0}/{1}).",
                chunk => _transformApplier.ApplyBaseTransforms(chunk));

            await ApplyChunkedAsync(context, builtNodes, "Apply Images",
                "Adding Image components ({0}/{1}).",
                chunk => _imageApplier.Apply(chunk));

            string textModeLabel = FigmaImportSettings.instance.TextComponent == TextComponentKind.Legacy
                ? "Adding Legacy Text components ({0}/{1})."
                : "Adding TextMeshProUGUI components ({0}/{1}).";
            TextApplier.ClearCaches();
            await ApplyChunkedAsync(context, builtNodes, "Apply Text",
                textModeLabel,
                chunk => _textApplier.Apply(chunk));

            await ApplyChunkedAsync(context, builtNodes, "Apply Layout",
                "Adding layout components ({0}/{1}).",
                chunk => _layoutApplier.Apply(chunk));

            await ApplyChunkedAsync(context, builtNodes, "Apply Components",
                "Adding Button/Mask/ScrollRect ({0}/{1}).",
                chunk => _componentApplier.Apply(chunk));

            context.Report("Generate Prefabs", "Planning and emitting explicit and component-based prefabs.", 0, builtNodes.Count, false, true);
            PrefabPlanSet prefabPlans = _prefabPipeline.BuildPlans(
                builtNodes,
                session.PrefabOutputFolder,
                session.FileResponse?.Components,
                FigmaImportSettings.instance.EnableAutoComponentPrefabs,
                context.Log);

            // Pre-emit pass: embed FigmaSyncMarker so saved .prefab assets carry the marker
            // even when --root-as-prefab is used without --scene.
            SyncMarkerWriter.WriteMarkers(context.ImportedRootNodes, session.FileKey);

            _prefabPipeline.EmitAndInstantiate(prefabPlans, context.Log);
            context.Log($"Generated {prefabPlans.Plans.Count} prefab asset plan(s).");

            // Post-emit pass: EmitAndInstantiate may swap scene GameObjects with prefab
            // instances; re-run WriteMarkers so every FigmaNode points at its real GO.
            SyncMarkerWriter.WriteMarkers(context.ImportedRootNodes, session.FileKey);

            await RebuildLayoutsChunkedAsync(context, builtNodes);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (context.ImportedRootNodes.Count > 0 && context.ImportedRootNodes[0].GameObject != null)
            {
                Selection.activeObject = context.ImportedRootNodes[0].GameObject;
            }

            int totalNodes = 0;
            foreach (FigmaNode rootNode in context.ImportedRootNodes)
            {
                totalNodes += CountNodes(rootNode);
            }

            context.Log($"Imported {context.ImportedRootNodes.Count} frame(s), tagged {totalNodes} node(s), prepared {builtNodes.Count} scene object(s).");
            // Backend-local stage only — the controller emits the final 'Import Complete'
            // once the selected backend (UGUI / UIToolkit) has finished, so this
            // backend doesn't surface a premature completion stage.
            context.Report("UGUI Backend Complete", "Scene hierarchy generated.", 1, 1, false, true);
        }

        private async Task<int> ImportSpritesForDiffAsync(ImportContext context, DiffPlan diffPlan, IProgress<SpriteImportProgress>? progress)
        {
            List<FigmaNode> spriteNodes = new();
            HashSet<string> forceRefreshNodeIds = new(StringComparer.Ordinal);

            foreach (FrameDiffPlan framePlan in diffPlan.Frames)
            {
                SpriteImporter.CollectDownloadableNodesRecursive(framePlan.RootNode, spriteNodes);

                if (framePlan.FallbackToFullRebuild)
                {
                    continue;
                }

                foreach (NodeDiff nodeDiff in framePlan.NodeDiffs.Values)
                {
                    if (nodeDiff.RequiresSpriteRefresh)
                    {
                        forceRefreshNodeIds.Add(nodeDiff.Node.Id);
                    }
                }
            }

            return await _spriteImporter.ImportNodesAsync(
                spriteNodes,
                context.Session.Token,
                context.Session.FileKey,
                context.Session.OutputFolder,
                context.CancellationToken,
                forceRefreshNodeIds,
                progress);
        }

        private async Task ApplyChunkedAsync(
            ImportContext context,
            IReadOnlyList<FigmaNode> nodes,
            string stageName,
            string statusFormat,
            Action<IReadOnlyList<FigmaNode>> applyChunk)
        {
            int total = nodes.Count;
            if (total == 0)
            {
                context.Report(stageName, string.Format(statusFormat, 0, 0), 0, 0, false, true);
                return;
            }

            List<FigmaNode> chunk = new(ApplyChunkSize);
            for (int offset = 0; offset < total; offset += ApplyChunkSize)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int len = Math.Min(ApplyChunkSize, total - offset);
                chunk.Clear();
                for (int k = 0; k < len; k++)
                {
                    chunk.Add(nodes[offset + k]);
                }

                applyChunk(chunk);

                int processed = offset + len;
                context.Report(stageName, string.Format(statusFormat, processed, total), processed, total, false, true);
                await Task.Yield();
            }
        }

        private async Task RebuildLayoutsChunkedAsync(ImportContext context, IReadOnlyList<FigmaNode> nodes)
        {
            int total = nodes.Count;
            if (total == 0)
            {
                return;
            }

            int processed = 0;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                FigmaNode node = nodes[i];
                if (node.RectTransform != null &&
                    (node.Tags.Contains(NodeTag.AutoLayout) || node.Tags.Contains(NodeTag.HugContents) || node.Tags.Contains(NodeTag.Scroll)))
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(node.RectTransform);
                }

                processed++;
                if (processed % ApplyChunkSize == 0)
                {
                    context.Report("Rebuild Layouts", $"Rebuilding layout ({processed}/{total}).", processed, total, false, true);
                    await Task.Yield();
                }
            }

            context.Report("Rebuild Layouts", $"Rebuilding layout ({total}/{total}).", total, total, false, true);
        }

        private static void DestroyFallbackCanvases(DiffPlan diffPlan, SceneSyncIndex sceneSyncIndex)
        {
            foreach (FrameDiffPlan framePlan in diffPlan.Frames)
            {
                if (!framePlan.FallbackToFullRebuild)
                {
                    continue;
                }

                if (!sceneSyncIndex.TryGetCanvasMarker(framePlan.RootFrameNodeId, out FigmaSyncMarker? canvasMarker) || canvasMarker == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(canvasMarker.gameObject);
            }
        }

        private static void UnpackPrefabInstancesForDiff(DiffPlan diffPlan, SceneSyncIndex sceneSyncIndex)
        {
            HashSet<GameObject> unpackTargets = new();

            foreach (FrameDiffPlan framePlan in diffPlan.Frames)
            {
                if (framePlan.FallbackToFullRebuild)
                {
                    continue;
                }

                foreach (FigmaSyncMarker marker in sceneSyncIndex.GetMarkersForRoot(framePlan.RootFrameNodeId))
                {
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(marker.gameObject))
                    {
                        unpackTargets.Add(marker.gameObject);
                    }
                }
            }

            foreach (GameObject target in unpackTargets)
            {
                PrefabUtility.UnpackPrefabInstance(target, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }

        private static void DestroyRemovedNodes(DiffPlan diffPlan, SceneSyncIndex sceneSyncIndex)
        {
            HashSet<string> removedNodeIds = new(StringComparer.Ordinal);
            foreach (FrameDiffPlan framePlan in diffPlan.Frames)
            {
                foreach (string removedNodeId in framePlan.RemovedNodeIds)
                {
                    removedNodeIds.Add(removedNodeId);
                }
            }

            foreach (string removedNodeId in removedNodeIds)
            {
                if (!sceneSyncIndex.TryGetMarker(removedNodeId, out FigmaSyncMarker? marker) || marker == null || marker.gameObject == null)
                {
                    continue;
                }

                if (HasRemovedAncestor(marker.transform.parent, removedNodeIds))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }
        }

        private static bool HasRemovedAncestor(Transform? current, HashSet<string> removedNodeIds)
        {
            while (current != null)
            {
                FigmaSyncMarker ancestorMarker = current.GetComponent<FigmaSyncMarker>();
                if (ancestorMarker != null && !ancestorMarker.IsSyntheticCanvas && removedNodeIds.Contains(ancestorMarker.NodeId))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void CleanupGeneratedComponents(IReadOnlyList<FigmaNode> nodes, DiffPlan diffPlan)
        {
            foreach (FigmaNode node in nodes)
            {
                if (node.GameObject == null)
                {
                    continue;
                }

                if (diffPlan.TryGetFrame(GetRootFrameNodeId(node), out FrameDiffPlan? framePlan) &&
                    framePlan != null &&
                    framePlan.NodeDiffs.TryGetValue(node.Id, out NodeDiff? nodeDiff) &&
                    nodeDiff.Action.HasFlag(DiffAction.New))
                {
                    continue;
                }

                RemoveComponent<Image>(node.GameObject);
                RemoveComponent<Outline>(node.GameObject);
                RemoveComponent<TextMeshProUGUI>(node.GameObject);
                RemoveComponent<Text>(node.GameObject);
                RemoveComponent<ContentSizeFitter>(node.GameObject);
                RemoveComponent<HorizontalLayoutGroup>(node.GameObject);
                RemoveComponent<VerticalLayoutGroup>(node.GameObject);
                RemoveComponent<WrapLayoutGroup>(node.GameObject);
                RemoveComponent<LayoutElement>(node.GameObject);
                RemoveComponent<Button>(node.GameObject);
                RemoveComponent<CanvasGroup>(node.GameObject);
                RemoveComponent<RectMask2D>(node.GameObject);
                RemoveComponent<Mask>(node.GameObject);
                RemoveComponent<AspectRatioFitter>(node.GameObject);
                RemoveComponent<ScrollRect>(node.GameObject);
            }
        }

        private static void RemoveComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static string GetRootFrameNodeId(FigmaNode node)
        {
            FigmaNode current = node;
            while (current.Parent != null)
            {
                current = current.Parent;
            }

            return current.Id;
        }

        private static void LogDiffSummary(DiffPlan diffPlan, Action<string> log)
        {
            int fallbackFrames = 0;
            int newNodes = 0;
            int changedNodes = 0;
            int reparentedNodes = 0;
            int reorderedNodes = 0;
            int removedNodes = 0;

            foreach (FrameDiffPlan framePlan in diffPlan.Frames)
            {
                if (framePlan.FallbackToFullRebuild)
                {
                    fallbackFrames++;
                    log($"Diff: frame '{framePlan.RootNode.Name}' falls back to full rebuild. {framePlan.FallbackReason}");
                }

                removedNodes += framePlan.RemovedNodeIds.Count;
                foreach (NodeDiff nodeDiff in framePlan.NodeDiffs.Values)
                {
                    if (nodeDiff.Action.HasFlag(DiffAction.New))
                    {
                        newNodes++;
                    }

                    if (nodeDiff.Action.HasFlag(DiffAction.Changed))
                    {
                        changedNodes++;
                    }

                    if (nodeDiff.Action.HasFlag(DiffAction.Reparented))
                    {
                        reparentedNodes++;
                    }

                    if (nodeDiff.Action.HasFlag(DiffAction.Reordered))
                    {
                        reorderedNodes++;
                    }
                }
            }

            log($"Diff summary: fallbackFrames={fallbackFrames}, new={newNodes}, changed={changedNodes}, reparented={reparentedNodes}, reordered={reorderedNodes}, removed={removedNodes}.");
        }

        private static int CountNodes(FigmaNode node)
        {
            int count = 1;
            if (node.Children == null)
            {
                return count;
            }

            foreach (FigmaNode child in node.Children)
            {
                count += CountNodes(child);
            }

            return count;
        }
    }
}
