using UnityEngine;

namespace YourName.AvatarClosetTool.Runtime
{
    public sealed class AvatarClosetModuleMetadata : MonoBehaviour
    {
        [SerializeField] private int _schemaVersion = 1;
        [SerializeField] private int _generatedOutfitCount;
        [SerializeField] private string _generatorVersion = string.Empty;
        [SerializeField] private string _markerId = string.Empty;

        public int SchemaVersion
        {
            get => _schemaVersion;
            set => _schemaVersion = value;
        }

        public int GeneratedOutfitCount
        {
            get => _generatedOutfitCount;
            set => _generatedOutfitCount = value;
        }

        public string GeneratorVersion
        {
            get => _generatorVersion;
            set => _generatorVersion = value;
        }

        public string MarkerId
        {
            get => _markerId;
            set => _markerId = value;
        }
    }
}
