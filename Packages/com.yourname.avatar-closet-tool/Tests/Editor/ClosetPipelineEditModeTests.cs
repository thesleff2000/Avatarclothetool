using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YourName.AvatarClosetTool.Editor;
using YourName.AvatarClosetTool.Runtime;

namespace YourName.AvatarClosetTool.Tests.Editor
{
    public sealed class ClosetPipelineEditModeTests
    {
        private readonly List<GameObject> _objectsToCleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objectsToCleanup.Count; i++)
            {
                if (_objectsToCleanup[i] != null)
                {
                    Object.DestroyImmediate(_objectsToCleanup[i]);
                }
            }

            _objectsToCleanup.Clear();
        }

        [Test]
        public void PipelineStopsOnValidationError()
        {
            EnsureMaOrInconclusive();
            GameObject avatarRoot = CreateRoot("AvatarRoot_ValidationError");
            GameObject closetRoot = CreateChild(avatarRoot, "ClosetRoot");
            GameObject outfit = CreateChild(avatarRoot, "OutfitOutsideClosetRoot");

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult result = pipeline.RunPipeline(
                BuildRequest(avatarRoot, closetRoot, new[] { outfit }),
                null);

            Assert.IsTrue(result.HasError);
            Assert.IsFalse(result.Applied);
            Assert.AreEqual(0, CountModules(avatarRoot));
        }

        [Test]
        public void RepairRunsOnlyWhenNeeded()
        {
            EnsureMaOrInconclusive();
            GameObject avatarRoot = CreateRoot("AvatarRoot_RepairNeeded");
            GameObject closetRoot = CreateChild(avatarRoot, "ClosetRoot");
            GameObject setA = CreateChild(closetRoot, "SetA");

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult first = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA }), null);
            Assert.IsFalse(first.HasError);
            Assert.IsTrue(first.Applied);

            GameObject moduleAfterFirst = FindModule(avatarRoot);
            Assert.IsNotNull(moduleAfterFirst);
            int firstId = moduleAfterFirst.GetInstanceID();

            ClosetPipeline.PipelineResult second = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA }), null);
            Assert.IsFalse(second.HasError);
            Assert.IsTrue(second.Applied);

            GameObject moduleAfterSecond = FindModule(avatarRoot);
            Assert.IsNotNull(moduleAfterSecond);
            int secondId = moduleAfterSecond.GetInstanceID();
            Assert.AreEqual(firstId, secondId);

            AvatarClosetModuleMetadata metadata = moduleAfterSecond.GetComponent<AvatarClosetModuleMetadata>();
            Assert.IsNotNull(metadata);
            metadata.MarkerId = "broken-marker";

            ClosetPipeline.PipelineResult third = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA }), null);
            Assert.IsFalse(third.HasError);
            Assert.IsTrue(third.Applied);

            GameObject moduleAfterThird = FindModule(avatarRoot);
            Assert.IsNotNull(moduleAfterThird);
            Assert.AreNotEqual(secondId, moduleAfterThird.GetInstanceID());
        }

        [Test]
        public void IdempotentApply()
        {
            EnsureMaOrInconclusive();
            GameObject avatarRoot = CreateRoot("AvatarRoot_Idempotent");
            GameObject closetRoot = CreateChild(avatarRoot, "ClosetRoot");
            GameObject setA = CreateChild(closetRoot, "SetA");
            GameObject setB = CreateChild(closetRoot, "SetB");

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult first = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA, setB }), null);
            ClosetPipeline.PipelineResult second = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA, setB }), null);

            Assert.IsFalse(first.HasError);
            Assert.IsFalse(second.HasError);
            Assert.AreEqual(1, CountModules(avatarRoot));
            Assert.AreEqual(1, avatarRoot.GetComponentsInChildren<AvatarClosetRegistrationStore>(true).Length);
        }

        [Test]
        public void RepairRebuildKeepsStore()
        {
            EnsureMaOrInconclusive();
            GameObject avatarRoot = CreateRoot("AvatarRoot_KeepStore");
            GameObject closetRoot = CreateChild(avatarRoot, "ClosetRoot");
            GameObject setA = CreateChild(closetRoot, "SetA");

            GameObject storeHolder = CreateChild(avatarRoot, "AvatarClosetRegistrationStore");
            AvatarClosetRegistrationStore store = storeHolder.AddComponent<AvatarClosetRegistrationStore>();
            store.Outfits.Add(new AvatarClosetRegistrationStore.OutfitRecord
            {
                DisplayName = "StoredOutfit",
                TargetGameObject = setA,
                ParameterKey = "ACT_STORED"
            });

            GameObject brokenModule = CreateChild(avatarRoot, "AvatarClosetModule");
            AvatarClosetModuleMetadata brokenMetadata = brokenModule.AddComponent<AvatarClosetModuleMetadata>();
            brokenMetadata.MarkerId = "broken-marker";
            brokenMetadata.SchemaVersion = 1;

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult result = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA }), null);

            Assert.IsFalse(result.HasError);
            Assert.IsTrue(result.Applied);
            Assert.AreEqual(1, avatarRoot.GetComponentsInChildren<AvatarClosetRegistrationStore>(true).Length);
            Assert.AreEqual(setA, avatarRoot.GetComponentInChildren<AvatarClosetRegistrationStore>(true).Outfits[0].TargetGameObject);
        }

        [Test]
        public void ApplyCreatesFxControllerAndSetParam()
        {
            EnsureMaOrInconclusive();
            GameObject avatarRoot = CreateRoot("AvatarRoot_ApplyFx");
            GameObject closetRoot = CreateChild(avatarRoot, "ClosetRoot");
            GameObject setA = CreateChild(closetRoot, "SetA");
            GameObject setB = CreateChild(closetRoot, "SetB");

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult result = pipeline.RunPipeline(BuildRequest(avatarRoot, closetRoot, new[] { setA, setB }), null);

            Assert.IsFalse(result.HasError);
            GameObject module = FindModule(avatarRoot);
            Assert.IsNotNull(module);

            Component mergeAnimator = FindComponentByTypeNames(module, new[]
            {
                "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator",
                "ModularAvatarMergeAnimator"
            });
            Assert.IsNotNull(mergeAnimator);
            Assert.IsTrue(HasAnimatorControllerReference(mergeAnimator));

            Component parameters = FindComponentByTypeNames(module, new[]
            {
                "nadena.dev.modular_avatar.core.ModularAvatarParameters",
                "ModularAvatarParameters"
            });
            Assert.IsNotNull(parameters);
            Assert.IsTrue(SerializedContainsStringValue(parameters, "ACT_SET"));
        }

        private static ClosetPipeline.PipelineRequest BuildRequest(GameObject avatarRoot, GameObject closetRoot, IReadOnlyList<GameObject> outfits)
        {
            return new ClosetPipeline.PipelineRequest
            {
                AvatarRoot = avatarRoot,
                ClosetRoot = closetRoot,
                UserOutfits = outfits.Select(go => new ClosetPipeline.OutfitInput
                {
                    DisplayName = go.name,
                    TargetGameObject = go
                }).ToList()
            };
        }

        private GameObject CreateRoot(string name)
        {
            GameObject root = new GameObject(name);
            _objectsToCleanup.Add(root);
            return root;
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static GameObject FindModule(GameObject avatarRoot)
        {
            for (int i = 0; i < avatarRoot.transform.childCount; i++)
            {
                Transform child = avatarRoot.transform.GetChild(i);
                if (child.name == "AvatarClosetModule")
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static int CountModules(GameObject avatarRoot)
        {
            int count = 0;
            for (int i = 0; i < avatarRoot.transform.childCount; i++)
            {
                if (avatarRoot.transform.GetChild(i).name == "AvatarClosetModule")
                {
                    count++;
                }
            }

            return count;
        }

        private static void EnsureMaOrInconclusive()
        {
            if (ResolveType("nadena.dev.modular_avatar.core.ModularAvatarParameters") == null ||
                ResolveType("nadena.dev.modular_avatar.core.ModularAvatarMenuItem") == null ||
                ResolveType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator") == null)
            {
                Assert.Inconclusive("Modular Avatar is not installed in this project.");
            }
        }

        private static Component FindComponentByTypeNames(GameObject target, IEnumerable<string> typeNames)
        {
            foreach (string typeName in typeNames)
            {
                System.Type type = ResolveType(typeName);
                if (type == null)
                {
                    continue;
                }

                Component component = target.GetComponent(type);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static System.Type ResolveType(string typeName)
        {
            System.Type type = System.Type.GetType(typeName, false);
            if (type != null)
            {
                return type;
            }

            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName, false);
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    type = null;
                }

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool HasAnimatorControllerReference(Component component)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                RuntimeAnimatorController controller = iterator.objectReferenceValue as RuntimeAnimatorController;
                if (controller != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SerializedContainsStringValue(Component component, string expectedValue)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType == SerializedPropertyType.String &&
                    iterator.stringValue == expectedValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
