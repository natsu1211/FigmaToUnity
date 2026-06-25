using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    // Resolves "#use:<path-or-name>" nodes to an existing project prefab and instantiates it
    // in place of the built anchor GameObject, instead of generating a brand-new prefab.
    //
    // The reference may be either an asset path ("Assets/UI/Btn.prefab", a leading "Assets/"
    // and the ".prefab" extension are added if missing) or a bare prefab name ("Btn"), which is
    // resolved by AssetDatabase search. Only the placement (anchors / pivot / anchoredPosition)
    // from the Figma node is applied; the prefab keeps its own authored size.
    internal sealed class ExternalPrefabPipeline
    {
        public void Apply(IReadOnlyList<FigmaNode> builtNodes, Action<string>? log = null)
        {
            foreach (FigmaNode node in builtNodes)
            {
                string? reference = node.ExternalPrefabPath;
                if (string.IsNullOrWhiteSpace(reference) || node.GameObject == null)
                {
                    continue;
                }

                GameObject? prefabAsset = ResolvePrefab(reference!);
                if (prefabAsset == null)
                {
                    log?.Invoke($"#use: could not resolve prefab '{reference}' for node '{node.Name}' ({node.Id}). Left the placeholder GameObject in place.");
                    continue;
                }

                // In Diff mode the reused GameObject may already be an instance of this exact
                // prefab — keep it (and any manual overrides) instead of re-instantiating.
                GameObject existing = node.GameObject;
                if (PrefabUtility.IsAnyPrefabInstanceRoot(existing) &&
                    PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefabAsset)
                {
                    continue;
                }

                InstantiateExternal(node, prefabAsset, log);
            }
        }

        private static GameObject? ResolvePrefab(string reference)
        {
            bool looksLikePath = reference.Contains('/') ||
                                 reference.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

            if (looksLikePath)
            {
                string path = reference.Replace('\\', '/');
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".prefab";
                }

                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    path = "Assets/" + path.TrimStart('/');
                }

                GameObject? byPath = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (byPath != null)
                {
                    return byPath;
                }

                // Fall back to a name search using the file name part.
                reference = Path.GetFileNameWithoutExtension(reference);
            }

            return FindPrefabByName(reference);
        }

        private static GameObject? FindPrefabByName(string prefabName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(assetPath), prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                }
            }

            return null;
        }

        private static void InstantiateExternal(FigmaNode node, GameObject prefabAsset, Action<string>? log)
        {
            GameObject oldObject = node.GameObject!;
            Transform? parent = oldObject.transform.parent;
            int siblingIndex = oldObject.transform.GetSiblingIndex();

            var newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            if (parent != null)
            {
                newObject.transform.SetParent(parent, false);
            }

            newObject.transform.SetSiblingIndex(siblingIndex);
            CopyPlacement(oldObject.transform, newObject.transform);

            UnityEngine.Object.DestroyImmediate(oldObject);

            // The Figma subtree under a #use: node is ignored during tagging, so the anchor has
            // no built children to rebind — point the node at the new instance and we are done.
            node.GameObject = newObject;
            node.RectTransform = newObject.transform as RectTransform;

            log?.Invoke($"#use: instantiated '{AssetDatabase.GetAssetPath(prefabAsset)}' for node '{node.Name}' ({node.Id}).");
        }

        // Position only: copy anchors / pivot / anchored position so the prefab lands where the
        // design placed it, but keep the prefab's own sizeDelta (its authored size).
        private static void CopyPlacement(Transform source, Transform destination)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;

            if (source is RectTransform sourceRect && destination is RectTransform destinationRect)
            {
                destinationRect.anchorMin = sourceRect.anchorMin;
                destinationRect.anchorMax = sourceRect.anchorMax;
                destinationRect.pivot = sourceRect.pivot;
                destinationRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
            }
        }
    }
}
