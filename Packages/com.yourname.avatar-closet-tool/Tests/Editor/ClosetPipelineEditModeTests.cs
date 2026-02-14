using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
            GameObject avatarRoot = CreateRoot("AvatarRoot_ValidationError");
            GameObject menuRoot = CreateChild(avatarRoot, "MenuRoot");
            menuRoot.AddComponent<ClosetMenuRoot>();

            GameObject invalidPartObject = CreateChild(menuRoot, "InvalidPart");
            invalidPartObject.AddComponent<ClosetOutfitPart>();

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult result = pipeline.RunPipeline(new ClosetPipeline.PipelineRequest
            {
                AvatarRoot = avatarRoot,
                UserOutfits = new List<ClosetPipeline.OutfitInput>()
            }, null);

            Assert.IsTrue(result.HasError);
            Assert.IsFalse(result.Applied);
            Assert.AreEqual(0, CountModules(avatarRoot));
        }

        [Test]
        public void RepairRunsOnlyWhenNeeded()
        {
            GameObject avatarRoot = CreateInventoryTreeWithOneSet("AvatarRoot_RepairNeeded", out ClosetOutfitSet set);
            ClosetPipeline pipeline = new ClosetPipeline();

            ClosetPipeline.PipelineResult first = pipeline.RunPipeline(BuildEmptyRequest(avatarRoot), null);
            Assert.IsFalse(first.HasError);
            Assert.IsTrue(first.Applied);

            GameObject moduleAfterFirst = FindModule(avatarRoot);
            Assert.IsNotNull(moduleAfterFirst);
            int firstId = moduleAfterFirst.GetInstanceID();

            ClosetPipeline.PipelineResult second = pipeline.RunPipeline(BuildEmptyRequest(avatarRoot), null);
            Assert.IsFalse(second.HasError);
            Assert.IsTrue(second.Applied);

            GameObject moduleAfterSecond = FindModule(avatarRoot);
            Assert.IsNotNull(moduleAfterSecond);
            int secondId = moduleAfterSecond.GetInstanceID();
            Assert.AreEqual(firstId, secondId, "Repair not needed: module should be reused.");

            AvatarClosetModuleMetadata metadata = moduleAfterSecond.GetComponent<AvatarClosetModuleMetadata>();
            Assert.IsNotNull(metadata);
            metadata.MarkerId = "broken-marker";

            ClosetPipeline.PipelineResult third = pipeline.RunPipeline(BuildEmptyRequest(avatarRoot), null);
            Assert.IsFalse(third.HasError);
            Assert.IsTrue(third.Applied);

            GameObject moduleAfterThird = FindModule(avatarRoot);
            Assert.IsNotNull(moduleAfterThird);
            int thirdId = moduleAfterThird.GetInstanceID();
            Assert.AreNotEqual(secondId, thirdId, "Repair needed: module should be rebuilt.");
            Assert.AreEqual(set.gameObject, FindSingleStore(avatarRoot).Outfits[0].TargetGameObject);
        }

        [Test]
        public void IdempotentApply()
        {
            GameObject avatarRoot = CreateRoot("AvatarRoot_Idempotent");
            GameObject menuRootObject = CreateChild(avatarRoot, "MenuRoot");
            menuRootObject.AddComponent<ClosetMenuRoot>();

            GameObject setAObject = CreateChild(menuRootObject, "SetA");
            ClosetOutfitSet setA = setAObject.AddComponent<ClosetOutfitSet>();
            setA.SetIndex = 0;

            GameObject setBObject = CreateChild(menuRootObject, "SetB");
            ClosetOutfitSet setB = setBObject.AddComponent<ClosetOutfitSet>();
            setB.SetIndex = 1;

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult first = pipeline.RunPipeline(BuildEmptyRequest(avatarRoot), null);
            ClosetPipeline.PipelineResult second = pipeline.RunPipeline(BuildEmptyRequest(avatarRoot), null);

            Assert.IsFalse(first.HasError);
            Assert.IsFalse(second.HasError);
            Assert.AreEqual(1, CountModules(avatarRoot));

            AvatarClosetRegistrationStore[] stores = avatarRoot.GetComponentsInChildren<AvatarClosetRegistrationStore>(true);
            Assert.AreEqual(1, stores.Length);

            GameObject module = FindModule(avatarRoot);
            Assert.IsNotNull(module);
            AvatarClosetModuleMetadata metadata = module.GetComponent<AvatarClosetModuleMetadata>();
            Assert.IsNotNull(metadata);
            Assert.Greater(metadata.GeneratedOutfitCount, 0);
        }

        [Test]
        public void RepairRebuildKeepsStore()
        {
            GameObject avatarRoot = CreateInventoryTreeWithOneSet("AvatarRoot_KeepStore", out ClosetOutfitSet set);

            GameObject storeHolder = CreateChild(avatarRoot, "AvatarClosetRegistrationStore");
            AvatarClosetRegistrationStore store = storeHolder.AddComponent<AvatarClosetRegistrationStore>();
            store.Outfits.Add(new AvatarClosetRegistrationStore.OutfitRecord
            {
                DisplayName = "StoredOutfit",
                TargetGameObject = set.gameObject,
                ParameterKey = "ACT_STORED"
            });

            GameObject brokenModule = CreateChild(avatarRoot, "AvatarClosetModule");
            AvatarClosetModuleMetadata brokenMetadata = brokenModule.AddComponent<AvatarClosetModuleMetadata>();
            brokenMetadata.MarkerId = "broken-marker";
            brokenMetadata.SchemaVersion = 1;

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineResult result = pipeline.RunPipeline(BuildEmptyRequest(avatarRoot), null);

            Assert.IsFalse(result.HasError);
            Assert.IsTrue(result.Applied);

            AvatarClosetRegistrationStore[] stores = avatarRoot.GetComponentsInChildren<AvatarClosetRegistrationStore>(true);
            Assert.AreEqual(1, stores.Length);
            Assert.AreEqual(1, stores[0].Outfits.Count);
            Assert.AreEqual(set.gameObject, stores[0].Outfits[0].TargetGameObject);

            GameObject module = FindModule(avatarRoot);
            Assert.IsNotNull(module);
            AvatarClosetModuleMetadata metadata = module.GetComponent<AvatarClosetModuleMetadata>();
            Assert.IsNotNull(metadata);
            Assert.AreEqual("avatar-closet-module-v1", metadata.MarkerId);
        }

        [Test]
        public void ContextMenuAssignsSetIndexUniquely()
        {
            GameObject avatarRoot = CreateRoot("AvatarRoot_ContextMenu");
            GameObject menuRootObject = CreateChild(avatarRoot, "MenuRoot");
            menuRootObject.AddComponent<ClosetMenuRoot>();

            GameObject first = CreateChild(menuRootObject, "SetFirst");
            GameObject second = CreateChild(menuRootObject, "SetSecond");

            bool firstOk = ClosetHierarchyContextMenu.TryAssignOutfitSet(first, out _);
            bool secondOk = ClosetHierarchyContextMenu.TryAssignOutfitSet(second, out _);

            Assert.IsTrue(firstOk);
            Assert.IsTrue(secondOk);

            ClosetOutfitSet firstSet = first.GetComponent<ClosetOutfitSet>();
            ClosetOutfitSet secondSet = second.GetComponent<ClosetOutfitSet>();
            Assert.IsNotNull(firstSet);
            Assert.IsNotNull(secondSet);
            Assert.AreNotEqual(firstSet.SetIndex, secondSet.SetIndex);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, new[] { firstSet.SetIndex, secondSet.SetIndex });
        }

        [Test]
        public void PartMustBeUnderOutfitSetValidation()
        {
            GameObject avatarRoot = CreateRoot("AvatarRoot_InvalidPart");
            GameObject menuRootObject = CreateChild(avatarRoot, "MenuRoot");
            menuRootObject.AddComponent<ClosetMenuRoot>();

            GameObject invalidPart = CreateChild(menuRootObject, "PartOutsideSet");
            invalidPart.AddComponent<ClosetOutfitPart>();

            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.ValidationResult validation = pipeline.ValidateOnly(BuildEmptyRequest(avatarRoot));

            Assert.IsTrue(validation.HasError);
            Assert.IsTrue(validation.Messages.Any(m => m.Text.Contains("OutfitPart") && m.Text.Contains("ClosetOutfitSet")));
        }

        private GameObject CreateInventoryTreeWithOneSet(string rootName, out ClosetOutfitSet set)
        {
            GameObject avatarRoot = CreateRoot(rootName);
            GameObject menuRootObject = CreateChild(avatarRoot, "MenuRoot");
            menuRootObject.AddComponent<ClosetMenuRoot>();

            GameObject setObject = CreateChild(menuRootObject, "SetA");
            set = setObject.AddComponent<ClosetOutfitSet>();
            set.SetIndex = 0;
            return avatarRoot;
        }

        private static ClosetPipeline.PipelineRequest BuildEmptyRequest(GameObject avatarRoot)
        {
            return new ClosetPipeline.PipelineRequest
            {
                AvatarRoot = avatarRoot,
                UserOutfits = new List<ClosetPipeline.OutfitInput>()
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

        private static AvatarClosetRegistrationStore FindSingleStore(GameObject avatarRoot)
        {
            AvatarClosetRegistrationStore[] stores = avatarRoot.GetComponentsInChildren<AvatarClosetRegistrationStore>(true);
            Assert.AreEqual(1, stores.Length);
            return stores[0];
        }
    }
}
