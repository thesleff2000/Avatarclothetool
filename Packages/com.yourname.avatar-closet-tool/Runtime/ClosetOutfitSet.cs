using UnityEngine;

namespace YourName.AvatarClosetTool.Runtime
{
    public sealed class ClosetOutfitSet : MonoBehaviour
    {
        [SerializeField] private int _setIndex;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private bool _defaultOn;

        public int SetIndex
        {
            get => _setIndex;
            set => _setIndex = value;
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public bool DefaultOn
        {
            get => _defaultOn;
            set => _defaultOn = value;
        }

        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;
    }
}
