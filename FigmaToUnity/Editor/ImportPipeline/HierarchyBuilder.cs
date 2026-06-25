using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class HierarchyBuilder
    {
        private SceneSyncIndex? _sceneSyncIndex;
        private DiffPlan? _diffPlan;

        public List<FigmaNode> Build(
            IReadOnlyList<FigmaNode> rootNodes,
            string figmaFileName,
            SceneSyncIndex? sceneSyncIndex = null,
            DiffPlan? diffPlan = null)
        {
            _sceneSyncIndex = sceneSyncIndex;
            _diffPlan = diffPlan;

            List<FigmaNode> builtNodes = new();

            foreach (FigmaNode rootNode in rootNodes)
            {
                if (rootNode.IgnoreNode)
                {
                    continue;
                }

                CreateCanvasHierarchy(rootNode, figmaFileName, builtNodes);
            }

            _sceneSyncIndex = null;
            _diffPlan = null;
            return builtNodes;
        }

        private void CreateCanvasHierarchy(FigmaNode rootNode, string figmaFileName, List<FigmaNode> builtNodes)
        {
            string canvasName = $"{FigmaNameSanitizer.Sanitize(figmaFileName)}_{FigmaNameSanitizer.Sanitize(rootNode.Name)}_Canvas";

            bool useReuse = TryGetActiveFrame(rootNode.Id, out _);
            GameObject canvasObject = useReuse
                ? ResolveExistingCanvas(rootNode.Id) ?? CreateNewCanvas(canvasName)
                : CreateNewCanvas(canvasName);

            EnsureCanvasComponents(canvasObject, rootNode);
            canvasObject.name = canvasName;

            CreateNodeRecursive(rootNode, canvasObject.transform, builtNodes, useReuse, 0, ancestorPrefix: null);
        }

        private bool TryGetActiveFrame(string rootFrameNodeId, out FrameDiffPlan? framePlan)
        {
            framePlan = null;
            return _sceneSyncIndex != null &&
                   _diffPlan != null &&
                   _diffPlan.TryGetFrame(rootFrameNodeId, out framePlan) &&
                   framePlan != null &&
                   !framePlan.FallbackToFullRebuild;
        }

        private GameObject? ResolveExistingCanvas(string rootFrameNodeId)
        {
            if (_sceneSyncIndex != null &&
                _sceneSyncIndex.TryGetCanvasMarker(rootFrameNodeId, out FigmaSyncMarker? marker) &&
                marker != null)
            {
                return marker.gameObject;
            }

            return null;
        }

        private static GameObject CreateNewCanvas(string canvasName)
        {
            return new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        private static void EnsureCanvasComponents(GameObject canvasObject, FigmaNode rootNode)
        {
            RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = canvasObject.AddComponent<RectTransform>();
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = rootNode.AbsoluteBoundingBox != null
                ? new Vector2(rootNode.AbsoluteBoundingBox.Width, rootNode.AbsoluteBoundingBox.Height)
                : new Vector2(1920f, 1080f);

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private void CreateNodeRecursive(FigmaNode node, Transform parent, List<FigmaNode> builtNodes, bool useReuse, int siblingIndex, string? ancestorPrefix)
        {
            string objectName = ResolveDisplayName(node, siblingIndex, ancestorPrefix);
            GameObject nodeObject = useReuse ? ResolveExistingNode(node.Id) ?? CreateNewNode(objectName) : CreateNewNode(objectName);
            nodeObject.name = objectName;
            nodeObject.transform.SetParent(parent, false);
            nodeObject.transform.SetSiblingIndex(siblingIndex);

            RectTransform rectTransform = nodeObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = nodeObject.AddComponent<RectTransform>();
            }

            node.GameObject = nodeObject;
            node.RectTransform = rectTransform;
            node.StableObjectName = objectName;
            builtNodes.Add(node);

            if (node.Children == null)
            {
                return;
            }

            // Pass our own name down only when it is meaningful — otherwise
            // forward whatever the closest meaningful ancestor was so children
            // don't accumulate prefixes from generic placeholder ancestors.
            string? childAncestorPrefix = FigmaNameResolver.IsPlaceholderName(node.Name)
                ? ancestorPrefix
                : objectName;

            int effectiveSiblingIndex = 0;
            foreach (FigmaNode child in node.Children)
            {
                if (child.IgnoreNode)
                {
                    continue;
                }

                CreateNodeRecursive(child, nodeObject.transform, builtNodes, useReuse, effectiveSiblingIndex, childAncestorPrefix);
                effectiveSiblingIndex++;
            }
        }

        private static string ResolveDisplayName(FigmaNode node, int siblingIndex, string? ancestorPrefix)
        {
            if (!FigmaNameResolver.IsPlaceholderName(node.Name))
            {
                return FigmaNameSanitizer.Sanitize(node.Name);
            }

            if (node.Type == "TEXT")
            {
                string summary = FigmaNameResolver.TextSummary(node.Characters);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    return FigmaNameSanitizer.Sanitize(summary);
                }
            }

            string typeLabel = FigmaNameResolver.TypeLabel(node.Type);
            string indexed = $"{typeLabel}_{siblingIndex}";
            string combined = string.IsNullOrEmpty(ancestorPrefix) ? indexed : $"{ancestorPrefix}_{indexed}";
            return FigmaNameSanitizer.Sanitize(combined);
        }

        private GameObject? ResolveExistingNode(string nodeId)
        {
            if (_sceneSyncIndex != null &&
                _sceneSyncIndex.TryGetMarker(nodeId, out FigmaSyncMarker? marker) &&
                marker != null)
            {
                return marker.gameObject;
            }

            return null;
        }

        private static GameObject CreateNewNode(string objectName)
        {
            return new GameObject(objectName, typeof(RectTransform));
        }
    }
}
