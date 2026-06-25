using System.Collections.Generic;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor
{
    internal sealed class FigmaImportSession
    {
        public string Token { get; set; } = string.Empty;
        public string FigmaUrl { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public string FigmaFileName { get; set; } = string.Empty;
        public string OutputFolder { get; set; } = FigmaImporterPaths.DefaultSpriteOutputRoot;
        public string PrefabOutputFolder { get; set; } = FigmaImporterPaths.DefaultPrefabOutputRoot;
        public FigmaFileResponse? FileResponse { get; set; }
        public List<FrameSummary> Frames { get; } = new();
        public HashSet<string> SelectedFrameIds { get; } = new();
        public List<DesignNode> DesignRootNodes { get; } = new();
        public List<FigmaNode> ImportedRootNodes { get; } = new();

        /// <summary>
        /// Full node subtree per frame, fetched lazily the first time the user expands
        /// a frame in the selection UI. Keyed by frame node id. Used to render the
        /// internal structure tree so the import scope can be chosen per node.
        /// </summary>
        public Dictionary<string, DesignNode> FrameTrees { get; } = new();

        /// <summary>
        /// Node ids the user has unchecked in the structure tree. During import these
        /// are marked <c>IgnoreNode = true</c>, which prunes the node and its whole
        /// subtree from every backend pass.
        /// </summary>
        public HashSet<string> ExcludedNodeIds { get; } = new();

        /// <summary>
        /// When true, each root DesignNode receives the Prefab tag and
        /// ExplicitPrefab=true after tagging so the prefab pipeline emits
        /// a prefab per imported root. Set from the --root-as-prefab CLI flag.
        /// </summary>
        public bool RootAsPrefab { get; set; }
    }
}
