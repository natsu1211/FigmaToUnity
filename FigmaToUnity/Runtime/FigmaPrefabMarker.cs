using UnityEngine;

namespace FigmaToUnity.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FigmaPrefabMarker : MonoBehaviour
    {
        [SerializeField] private string sourceNodeId = string.Empty;
        [SerializeField] private string sourceComponentIdentity = string.Empty;
        [SerializeField] private int prefabKind;

        public string SourceNodeId
        {
            get => sourceNodeId;
            set => sourceNodeId = value ?? string.Empty;
        }

        public string SourceComponentIdentity
        {
            get => sourceComponentIdentity;
            set => sourceComponentIdentity = value ?? string.Empty;
        }

        public int PrefabKind
        {
            get => prefabKind;
            set => prefabKind = value;
        }
    }
}
