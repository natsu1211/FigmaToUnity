using System;
using System.Collections.Generic;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor.ImportPipeline
{
    [Flags]
    internal enum DiffAction
    {
        None = 0,
        New = 1 << 0,
        Changed = 1 << 1,
        Reparented = 1 << 2,
        Reordered = 1 << 3
    }

    internal sealed class NodeDiff
    {
        public NodeDiff(FigmaNode node, DiffAction action)
        {
            Node = node;
            Action = action;
        }

        public FigmaNode Node { get; }
        public DiffAction Action { get; set; }
        public bool RequiresSpriteRefresh { get; set; }
    }

    internal sealed class FrameDiffPlan
    {
        public FrameDiffPlan(FigmaNode rootNode)
        {
            RootNode = rootNode;
        }

        public FigmaNode RootNode { get; }
        public string RootFrameNodeId => RootNode.Id;
        public bool FallbackToFullRebuild { get; set; }
        public string FallbackReason { get; set; } = string.Empty;
        public Dictionary<string, NodeDiff> NodeDiffs { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RemovedNodeIds { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class DiffPlan
    {
        private readonly Dictionary<string, FrameDiffPlan> _framesByRootId = new(StringComparer.Ordinal);

        public IEnumerable<FrameDiffPlan> Frames => _framesByRootId.Values;

        public void AddFrame(FrameDiffPlan framePlan)
        {
            _framesByRootId[framePlan.RootFrameNodeId] = framePlan;
        }

        public bool TryGetFrame(string rootFrameNodeId, out FrameDiffPlan? framePlan)
        {
            return _framesByRootId.TryGetValue(rootFrameNodeId, out framePlan);
        }
    }
}
