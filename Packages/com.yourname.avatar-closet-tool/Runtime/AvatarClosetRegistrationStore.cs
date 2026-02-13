using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourName.AvatarClosetTool.Runtime
{
    public sealed class AvatarClosetRegistrationStore : MonoBehaviour
    {
        [Serializable]
        public sealed class OutfitRecord
        {
            public string DisplayName = string.Empty;
            public GameObject TargetGameObject;
            public string OptionalGroupName = string.Empty;
            public string ParameterKey = string.Empty;
            public string LastBindingFingerprint = string.Empty;
        }

        [SerializeField] private List<OutfitRecord> _outfits = new List<OutfitRecord>();

        public List<OutfitRecord> Outfits => _outfits;
    }
}
