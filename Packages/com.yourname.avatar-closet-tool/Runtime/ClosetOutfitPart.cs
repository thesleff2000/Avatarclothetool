using UnityEngine;

namespace YourName.AvatarClosetTool.Runtime
{
    public sealed class ClosetOutfitPart : MonoBehaviour
    {
        [SerializeField] private bool _defaultOn;
        [SerializeField] private string _displayName = string.Empty;

        public bool DefaultOn
        {
            get => _defaultOn;
            set => _defaultOn = value;
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;
    }
}
