using UnityEngine;

namespace YourName.AvatarClosetTool.Runtime
{
    public sealed class ClosetMenuRoot : MonoBehaviour
    {
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _namespacePrefix = string.Empty;
        [SerializeField] private int _version = 1;

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public string NamespacePrefix
        {
            get => _namespacePrefix;
            set => _namespacePrefix = value;
        }

        public int Version
        {
            get => _version;
            set => _version = value;
        }

        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;
    }
}
