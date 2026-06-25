using System.Collections.Generic;
using FigmaToUnity.Runtime;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class SceneSyncIndex
    {
        private readonly Dictionary<string, FigmaSyncMarker> _markersByNodeId = new(System.StringComparer.Ordinal);
        private readonly Dictionary<string, List<FigmaSyncMarker>> _markersByRootFrameId = new(System.StringComparer.Ordinal);
        private readonly Dictionary<string, FigmaSyncMarker> _canvasMarkersByRootFrameId = new(System.StringComparer.Ordinal);
        private readonly HashSet<string> _invalidRootFrameIds = new(System.StringComparer.Ordinal);

        public static SceneSyncIndex BuildActiveSceneIndex()
        {
            SceneSyncIndex index = new();
            FigmaSyncMarker[] markers = Object.FindObjectsByType<FigmaSyncMarker>(FindObjectsSortMode.None);
            foreach (FigmaSyncMarker marker in markers)
            {
                index.IndexMarker(marker);
            }

            return index;
        }

        // Indexes only markers under a single root (the prefab contents root when importing
        // inside an open Prefab Stage). A global scene scan would also pick up markers from
        // the hidden main scene behind the prefab stage, so diff updates must stay scoped.
        public static SceneSyncIndex BuildFromRoot(Transform root)
        {
            SceneSyncIndex index = new();
            FigmaSyncMarker[] markers = root.GetComponentsInChildren<FigmaSyncMarker>(true);
            foreach (FigmaSyncMarker marker in markers)
            {
                index.IndexMarker(marker);
            }

            return index;
        }

        public bool IsInvalidRoot(string rootFrameNodeId)
        {
            return _invalidRootFrameIds.Contains(rootFrameNodeId);
        }

        public bool TryGetMarker(string nodeId, out FigmaSyncMarker? marker)
        {
            return _markersByNodeId.TryGetValue(nodeId, out marker);
        }

        public bool TryGetCanvasMarker(string rootFrameNodeId, out FigmaSyncMarker? marker)
        {
            return _canvasMarkersByRootFrameId.TryGetValue(rootFrameNodeId, out marker);
        }

        public IReadOnlyList<FigmaSyncMarker> GetMarkersForRoot(string rootFrameNodeId)
        {
            return _markersByRootFrameId.TryGetValue(rootFrameNodeId, out List<FigmaSyncMarker>? markers)
                ? markers
                : System.Array.Empty<FigmaSyncMarker>();
        }

        private void IndexMarker(FigmaSyncMarker marker)
        {
            if (string.IsNullOrWhiteSpace(marker.RootFrameNodeId))
            {
                return;
            }

            if (!_markersByRootFrameId.TryGetValue(marker.RootFrameNodeId, out List<FigmaSyncMarker>? rootMarkers))
            {
                rootMarkers = new List<FigmaSyncMarker>();
                _markersByRootFrameId.Add(marker.RootFrameNodeId, rootMarkers);
            }

            rootMarkers.Add(marker);

            if (marker.IsSyntheticCanvas)
            {
                if (_canvasMarkersByRootFrameId.ContainsKey(marker.RootFrameNodeId))
                {
                    _invalidRootFrameIds.Add(marker.RootFrameNodeId);
                }
                else
                {
                    _canvasMarkersByRootFrameId.Add(marker.RootFrameNodeId, marker);
                }
            }
            else if (!string.IsNullOrWhiteSpace(marker.NodeId))
            {
                if (_markersByNodeId.TryGetValue(marker.NodeId, out FigmaSyncMarker? existing))
                {
                    _invalidRootFrameIds.Add(marker.RootFrameNodeId);
                    _invalidRootFrameIds.Add(existing.RootFrameNodeId);
                }
                else
                {
                    _markersByNodeId.Add(marker.NodeId, marker);
                }
            }
        }
    }
}
