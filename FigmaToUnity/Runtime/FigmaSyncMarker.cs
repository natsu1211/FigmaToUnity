using UnityEngine;

namespace FigmaToUnity.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FigmaSyncMarker : MonoBehaviour
    {
        [SerializeField] private string fileKey = string.Empty;
        [SerializeField] private string rootFrameNodeId = string.Empty;
        [SerializeField] private string nodeId = string.Empty;
        [SerializeField] private string parentNodeId = string.Empty;
        [SerializeField] private string componentId = string.Empty;
        [SerializeField] private string nodeType = string.Empty;
        [SerializeField] private string stableObjectName = string.Empty;
        [SerializeField] private int nodeHash;
        [SerializeField] private bool isSyntheticCanvas;

        public string FileKey
        {
            get => fileKey;
            set => fileKey = value ?? string.Empty;
        }

        public string RootFrameNodeId
        {
            get => rootFrameNodeId;
            set => rootFrameNodeId = value ?? string.Empty;
        }

        public string NodeId
        {
            get => nodeId;
            set => nodeId = value ?? string.Empty;
        }

        public string ParentNodeId
        {
            get => parentNodeId;
            set => parentNodeId = value ?? string.Empty;
        }

        public string ComponentId
        {
            get => componentId;
            set => componentId = value ?? string.Empty;
        }

        public string NodeType
        {
            get => nodeType;
            set => nodeType = value ?? string.Empty;
        }

        public string StableObjectName
        {
            get => stableObjectName;
            set => stableObjectName = value ?? string.Empty;
        }

        public int NodeHash
        {
            get => nodeHash;
            set => nodeHash = value;
        }

        public bool IsSyntheticCanvas
        {
            get => isSyntheticCanvas;
            set => isSyntheticCanvas = value;
        }
    }
}
