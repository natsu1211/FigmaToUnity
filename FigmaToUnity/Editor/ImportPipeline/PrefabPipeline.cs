using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FigmaToUnity.Core;
using FigmaToUnity.Runtime;
using UnityEditor;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal enum PrefabPlanKind
    {
        None,
        Explicit,
        Auto
    }

    internal sealed class PrefabPlan
    {
        public PrefabPlanKind Kind { get; set; }
        public string SourceNodeId { get; set; } = string.Empty;
        public string? ComponentId { get; set; }
        public string? SourceComponentIdentity { get; set; }
        public string EmitSourceNodeId { get; set; } = string.Empty;
        public string PrefabKey { get; set; } = string.Empty;
        public string PrefabName { get; set; } = string.Empty;
        public string PrefabPath { get; set; } = string.Empty;
        public FigmaNode EmitSourceNode { get; set; } = null!;
        public List<FigmaNode> SceneNodes { get; } = new();
    }

    internal sealed class PrefabPlanSet
    {
        public List<PrefabPlan> Plans { get; } = new();
        public Dictionary<FigmaNode, PrefabPlan> NodePlans { get; } = new();
    }

    internal sealed class PrefabPipeline
    {
        public PrefabPlanSet BuildPlans(
            IReadOnlyList<FigmaNode> builtNodes,
            string prefabOutputRoot,
            Dictionary<string, FigmaComponent>? components,
            bool enableAutoComponentPrefabs,
            Action<string>? log = null)
        {
            PrefabPlanSet result = new();
            Dictionary<string, PrefabPlan> plansByKey = new(StringComparer.Ordinal);

            foreach (FigmaNode node in builtNodes)
            {
                PrefabPlanKind kind = GetPlanKind(node);
                if (kind == PrefabPlanKind.None)
                {
                    continue;
                }

                if (kind == PrefabPlanKind.Auto)
                {
                    if (!enableAutoComponentPrefabs || HasAutoPrefabAncestor(node))
                    {
                        continue;
                    }
                }

                string? sourceComponentIdentity = GetSourceComponentIdentity(node);
                string prefabKey = GetPrefabKey(kind, node, sourceComponentIdentity);
                bool isNewPlan = !plansByKey.TryGetValue(prefabKey, out PrefabPlan? plan);
                if (isNewPlan)
                {
                    plan = new PrefabPlan
                    {
                        Kind = kind,
                        SourceNodeId = node.Id,
                        ComponentId = string.IsNullOrWhiteSpace(node.ComponentId) ? null : node.ComponentId,
                        SourceComponentIdentity = sourceComponentIdentity,
                        PrefabKey = prefabKey,
                        PrefabName = ResolvePrefabName(kind, node, sourceComponentIdentity, components),
                        EmitSourceNode = node,
                        EmitSourceNodeId = node.Id
                    };

                    plansByKey.Add(prefabKey, plan);
                    result.Plans.Add(plan);

                    log?.Invoke(kind == PrefabPlanKind.Explicit
                        ? $"Plan: node '{node.Name}' ({node.Id}) → Explicit prefab (tagged #prefab)"
                        : $"Plan: node '{node.Name}' ({node.Id}) → Auto prefab (type={node.Type}, sourceComponent={sourceComponentIdentity})");
                }
                else
                {
                    log?.Invoke($"Plan: node '{node.Name}' ({node.Id}) → reuse existing {plan!.Kind} plan [{prefabKey}]");
                }

                plan.SceneNodes.Add(node);
                result.NodePlans[node] = plan;

                if (ShouldPreferAsEmitSource(plan, node))
                {
                    string previousId = plan.EmitSourceNodeId;
                    plan.EmitSourceNode = node;
                    plan.EmitSourceNodeId = node.Id;
                    log?.Invoke($"Plan: emit source for [{prefabKey}] changed from {previousId} to {node.Id} (type={node.Type}, prefer COMPONENT over INSTANCE or shallower depth)");
                }
            }

            FinalizePrefabPaths(result.Plans, prefabOutputRoot);
            return result;
        }

        public void EmitAndInstantiate(PrefabPlanSet planSet, Action<string>? log = null)
        {
            if (planSet.Plans.Count == 0)
            {
                return;
            }

            foreach (PrefabPlan plan in GetEmissionOrder(planSet))
            {
                if (plan.EmitSourceNode.GameObject == null)
                {
                    continue;
                }

                AttachMarker(plan.EmitSourceNode.GameObject, plan);

                EnsureFolderExists(Path.GetDirectoryName(plan.PrefabPath)!.Replace('\\', '/'));
                GameObject? prefabAsset = PrefabUtility.SaveAsPrefabAsset(plan.EmitSourceNode.GameObject, plan.PrefabPath);
                if (prefabAsset == null)
                {
                    log?.Invoke($"Failed to save prefab asset: {plan.PrefabPath}");
                    continue;
                }

                log?.Invoke($"Saved {plan.Kind} prefab: {plan.PrefabPath} (emitSource={plan.EmitSourceNodeId}, instances={plan.SceneNodes.Count})");

                foreach (FigmaNode sceneNode in plan.SceneNodes.OrderByDescending(GetSceneDepth))
                {
                    ReplaceWithPrefabInstance(sceneNode, prefabAsset);
                }
            }
        }

        private static bool HasAutoPrefabAncestor(FigmaNode node)
        {
            FigmaNode? parent = node.Parent;
            while (parent != null)
            {
                if (GetSourceComponentIdentity(parent) != null)
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return false;
        }

        private static PrefabPlanKind GetPlanKind(FigmaNode node)
        {
            if (node.ExplicitPrefab)
            {
                return PrefabPlanKind.Explicit;
            }

            return GetSourceComponentIdentity(node) != null ? PrefabPlanKind.Auto : PrefabPlanKind.None;
        }

        private static string? GetSourceComponentIdentity(FigmaNode node)
        {
            if (string.Equals(node.Type, "COMPONENT", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(node.Id) ? null : node.Id;
            }

            if (string.Equals(node.Type, "INSTANCE", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(node.ComponentId) ? null : node.ComponentId;
            }

            return null;
        }

        private static string GetPrefabKey(PrefabPlanKind kind, FigmaNode node, string? sourceComponentIdentity)
        {
            return kind switch
            {
                PrefabPlanKind.Explicit => $"explicit:{node.Id}",
                PrefabPlanKind.Auto => $"component:{sourceComponentIdentity}",
                _ => string.Empty
            };
        }

        private static string ResolvePrefabName(
            PrefabPlanKind kind,
            FigmaNode node,
            string? sourceComponentIdentity,
            Dictionary<string, FigmaComponent>? components)
        {
            if (kind == PrefabPlanKind.Explicit && !FigmaNameResolver.IsPlaceholderName(node.Name))
            {
                return FigmaNameSanitizer.Sanitize(node.Name);
            }

            if (kind == PrefabPlanKind.Auto)
            {
                if (!string.IsNullOrWhiteSpace(sourceComponentIdentity) && components != null)
                {
                    string componentKey = sourceComponentIdentity!;
                    if (components.TryGetValue(componentKey, out FigmaComponent? component) &&
                        component != null &&
                        !FigmaNameResolver.IsPlaceholderName(component.Name))
                    {
                        return FigmaNameSanitizer.Sanitize(component.Name!);
                    }
                }

                if (!FigmaNameResolver.IsPlaceholderName(node.Name))
                {
                    return FigmaNameSanitizer.Sanitize(node.Name);
                }
            }

            // Placeholder name — fall back to a type label. Path collisions are
            // disambiguated downstream by FinalizePrefabPaths via `-1`/`-2`
            // suffixes, so multiple anonymous Components emit Component.prefab,
            // Component-1.prefab, etc.
            return FigmaNameResolver.TypeLabel(node.Type);
        }

        private static bool ShouldPreferAsEmitSource(PrefabPlan plan, FigmaNode candidate)
        {
            if (plan.Kind != PrefabPlanKind.Auto)
            {
                return false;
            }

            bool currentIsComponent = string.Equals(plan.EmitSourceNode.Type, "COMPONENT", StringComparison.OrdinalIgnoreCase);
            bool candidateIsComponent = string.Equals(candidate.Type, "COMPONENT", StringComparison.OrdinalIgnoreCase);

            if (!currentIsComponent && candidateIsComponent)
            {
                return true;
            }

            if (currentIsComponent == candidateIsComponent)
            {
                return GetSceneDepth(candidate) < GetSceneDepth(plan.EmitSourceNode);
            }

            return false;
        }

        private static void FinalizePrefabPaths(IReadOnlyList<PrefabPlan> plans, string prefabOutputRoot)
        {
            Dictionary<string, List<PrefabPlan>> groups = new(StringComparer.OrdinalIgnoreCase);

            foreach (PrefabPlan plan in plans)
            {
                string folder = GetPrefabFolder(prefabOutputRoot, plan.EmitSourceNode, plan.Kind);
                string basePath = FigmaImporterUtils.CombineAssetPath(folder, $"{plan.PrefabName}.prefab");
                if (!groups.TryGetValue(basePath, out List<PrefabPlan>? samePath))
                {
                    samePath = new List<PrefabPlan>();
                    groups.Add(basePath, samePath);
                }

                samePath.Add(plan);
            }

            foreach ((string basePath, List<PrefabPlan> samePath) in groups)
            {
                if (samePath.Count == 1)
                {
                    samePath[0].PrefabPath = basePath;
                    continue;
                }

                // Sort by PrefabKey so suffixes are deterministic across imports
                // even if the source iteration order shifts slightly.
                samePath.Sort(static (a, b) => string.CompareOrdinal(a.PrefabKey, b.PrefabKey));

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
                string directory = Path.GetDirectoryName(basePath)!.Replace('\\', '/');

                for (int i = 0; i < samePath.Count; i++)
                {
                    PrefabPlan plan = samePath[i];
                    if (i == 0)
                    {
                        plan.PrefabPath = basePath;
                    }
                    else
                    {
                        plan.PrefabPath = FigmaImporterUtils.CombineAssetPath(directory, $"{fileNameWithoutExtension}-{i}.prefab");
                    }
                }
            }
        }

        private static string GetPrefabFolder(string prefabOutputRoot, FigmaNode emitSource, PrefabPlanKind kind)
        {
            string frameName = FigmaImporterUtils.GetRootFrame(emitSource).Name;
            string frameFolder = FigmaImporterUtils.ToAssetFolderPath(prefabOutputRoot, frameName);
            string subFolder = kind == PrefabPlanKind.Explicit ? "Explicit" : "Components";
            return FigmaImporterUtils.CombineAssetPath(frameFolder, subFolder);
        }

        private static IEnumerable<PrefabPlan> GetEmissionOrder(PrefabPlanSet planSet)
        {
            return planSet.Plans
                .OrderByDescending(plan => GetPlannedAncestorDepth(plan.EmitSourceNode, planSet.NodePlans, plan))
                .ThenByDescending(plan => GetSceneDepth(plan.EmitSourceNode));
        }

        private static int GetPlannedAncestorDepth(FigmaNode node, IReadOnlyDictionary<FigmaNode, PrefabPlan> nodePlans, PrefabPlan currentPlan)
        {
            int depth = 0;
            FigmaNode? parent = node.Parent;

            while (parent != null)
            {
                if (nodePlans.TryGetValue(parent, out PrefabPlan? parentPlan) && !ReferenceEquals(parentPlan, currentPlan))
                {
                    depth++;
                }

                parent = parent.Parent;
            }

            return depth;
        }

        private static int GetSceneDepth(FigmaNode node)
        {
            int depth = 0;
            FigmaNode? current = node.Parent;

            while (current != null)
            {
                depth++;
                current = current.Parent;
            }

            return depth;
        }

        private static void ReplaceWithPrefabInstance(FigmaNode node, GameObject prefabAsset)
        {
            GameObject? oldObject = node.GameObject;
            if (oldObject == null)
            {
                return;
            }

            Transform? parent = oldObject.transform.parent;
            int siblingIndex = oldObject.transform.GetSiblingIndex();
            GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            if (parent != null)
            {
                newObject.transform.SetParent(parent, false);
            }

            newObject.transform.SetSiblingIndex(siblingIndex);
            CopyTransform(oldObject.transform, newObject.transform);
            UnityEngine.Object.DestroyImmediate(oldObject);
            RebindSubtree(node, newObject.transform);
        }

        private static void CopyTransform(Transform source, Transform destination)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;

            if (source is RectTransform sourceRect && destination is RectTransform destinationRect)
            {
                destinationRect.anchorMin = sourceRect.anchorMin;
                destinationRect.anchorMax = sourceRect.anchorMax;
                destinationRect.pivot = sourceRect.pivot;
                destinationRect.sizeDelta = sourceRect.sizeDelta;
                destinationRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                destinationRect.offsetMin = sourceRect.offsetMin;
                destinationRect.offsetMax = sourceRect.offsetMax;
            }
        }

        private static void RebindSubtree(FigmaNode node, Transform transform)
        {
            node.GameObject = transform.gameObject;
            node.RectTransform = transform as RectTransform;

            if (node.Children == null)
            {
                return;
            }

            int childCount = Math.Min(node.Children.Count, transform.childCount);
            for (int i = 0; i < childCount; i++)
            {
                RebindSubtree(node.Children[i], transform.GetChild(i));
            }
        }

        private static void AttachMarker(GameObject root, PrefabPlan plan)
        {
            FigmaPrefabMarker marker = root.GetComponent<FigmaPrefabMarker>();
            if (marker == null)
            {
                marker = root.AddComponent<FigmaPrefabMarker>();
            }

            marker.SourceNodeId = plan.SourceNodeId;
            marker.SourceComponentIdentity = plan.SourceComponentIdentity ?? string.Empty;
            marker.PrefabKind = (int)plan.Kind;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
