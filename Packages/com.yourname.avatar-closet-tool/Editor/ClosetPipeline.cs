using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using YourName.AvatarClosetTool.Runtime;

namespace YourName.AvatarClosetTool.Editor
{
    public sealed class ClosetPipeline
    {
        private const string ModuleObjectName = "AvatarClosetModule";
        private const string RegistrationStoreObjectName = "AvatarClosetRegistrationStore";
        private const int CurrentModuleSchemaVersion = 1;
        private const string ExpectedModuleMarker = "avatar-closet-module-v1";
        private const string DefaultSetParameterName = "ACT_SET";
        private const string MaInstallGuidance =
            "Modular Avatar가 설치되어 있어야 합니다. VCC 또는 GitHub에서 설치 후 다시 시도하세요.";

        private static readonly string[] MaParametersTypeNames =
        {
            "nadena.dev.modular_avatar.core.ModularAvatarParameters",
            "ModularAvatarParameters"
        };

        private static readonly string[] MaMenuItemTypeNames =
        {
            "nadena.dev.modular_avatar.core.ModularAvatarMenuItem",
            "ModularAvatarMenuItem"
        };

        private static readonly string[] MaObjectToggleTypeNames =
        {
            "nadena.dev.modular_avatar.core.ModularAvatarObjectToggle",
            "ModularAvatarObjectToggle"
        };

        private static readonly string[] MaMergeAnimatorTypeNames =
        {
            "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator",
            "ModularAvatarMergeAnimator"
        };

        private sealed class MaRequiredTypes
        {
            public Type ParametersType;
            public Type MenuItemType;
            public Type ObjectToggleType;
            public Type MergeAnimatorType;
        }

        public enum MessageSeverity
        {
            Info,
            Warning,
            Error
        }

        public sealed class PipelineMessage
        {
            public MessageSeverity Severity;
            public string Text;
        }

        public sealed class OutfitInput
        {
            public string DisplayName = string.Empty;
            public GameObject TargetGameObject;
            public string OptionalGroupName = string.Empty;
        }

        private sealed class InventoryPart
        {
            public ClosetOutfitPart Component;
            public string ParameterName;
        }

        private sealed class InventorySet
        {
            public ClosetOutfitSet Component;
            public string DisplayName;
            public List<InventoryPart> Parts = new List<InventoryPart>();
        }

        private sealed class InventoryRoot
        {
            public ClosetMenuRoot Component;
            public string DisplayName;
            public string NamespacePrefix;
            public string SetParameterName;
            public List<InventorySet> Sets = new List<InventorySet>();
        }

        public sealed class PipelineRequest
        {
            public GameObject AvatarRoot;
            public IReadOnlyList<OutfitInput> UserOutfits;
        }

        public sealed class ValidationResult
        {
            public bool HasError;
            public bool HasWarning;
            public bool NeedsRepair;
            public List<PipelineMessage> Messages = new List<PipelineMessage>();
        }

        public sealed class RepairResult
        {
            public bool DidRepair;
            public bool HasError;
            public List<PipelineMessage> Messages = new List<PipelineMessage>();
            public List<OutfitInput> EffectiveOutfits = new List<OutfitInput>();
        }

        public sealed class PipelineResult
        {
            public bool HasError;
            public bool Applied;
            public string FinalStatus = "Idle";
            public string Summary = string.Empty;
            public List<PipelineMessage> Messages = new List<PipelineMessage>();
        }

        // NeedsRepair=true conditions:
        // 1) Duplicate AvatarClosetModule objects exist under AvatarRoot.
        // 2) Existing module metadata is missing, schema is outdated, or marker mismatches.
        // 3) Existing module structure is not as expected (missing MA core components).
        // 4) Module exists but RegistrationStore metadata is missing or corrupted.
        public PipelineResult RunPipeline(PipelineRequest request, Action<string> statusCallback)
        {
            PipelineResult result = new PipelineResult();

            SetStatus("Validating...", statusCallback);
            Debug.Log("[ClosetPipeline] Step 1/3 ValidateOnly");
            ValidationResult validation = ValidateOnly(request);
            result.Messages.AddRange(validation.Messages);
            result.HasError = validation.HasError;
            if (result.HasError)
            {
                result.FinalStatus = "Done";
                result.Summary = "Validation failed. Apply was blocked.";
                SetStatus(result.FinalStatus, statusCallback);
                return result;
            }

            SetStatus("Repairing...", statusCallback);
            Debug.Log("[ClosetPipeline] Step 2/3 RepairIfNeeded");
            RepairResult repair = RepairIfNeeded(request, validation);
            result.Messages.AddRange(repair.Messages);
            result.HasError = validation.HasError || repair.HasError;
            if (result.HasError)
            {
                result.FinalStatus = "Done";
                result.Summary = "Repair failed. Apply was blocked.";
                SetStatus(result.FinalStatus, statusCallback);
                return result;
            }

            Debug.Assert(!result.HasError, "[ClosetPipeline] ApplyChanges must never be called when HasError is true.");
            SetStatus("Applying...", statusCallback);
            Debug.Log("[ClosetPipeline] Step 3/3 ApplyChanges");
            List<PipelineMessage> applyMessages;
            bool applied = ApplyChanges(request.AvatarRoot, repair.EffectiveOutfits, out applyMessages);
            result.Messages.AddRange(applyMessages);
            result.Applied = applied;
            bool applyHasError = applyMessages.Any(m => m.Severity == MessageSeverity.Error);
            result.HasError = validation.HasError || repair.HasError || applyHasError;

            result.FinalStatus = "Done";
            result.Summary = BuildSummary(result.Messages, result.Applied, result.HasError);
            SetStatus(result.FinalStatus, statusCallback);
            return result;
        }

        public ValidationResult ValidateOnly(PipelineRequest request)
        {
            ValidationResult result = new ValidationResult();
            GameObject avatarRoot = request != null ? request.AvatarRoot : null;
            List<OutfitInput> userOutfits = NormalizeOutfits(request != null ? request.UserOutfits : null);

            if (!TryResolveType("nadena.dev.modular_avatar.core.ModularAvatarParameters", out _))
            {
                AddMessage(
                    result.Messages,
                    MessageSeverity.Error,
                    $"{MaInstallGuidance} 누락 타입: nadena.dev.modular_avatar.core.ModularAvatarParameters");
                result.HasError = true;
                return result;
            }

            if (!TryResolveType("nadena.dev.modular_avatar.core.ModularAvatarMenuItem", out _))
            {
                AddMessage(
                    result.Messages,
                    MessageSeverity.Error,
                    $"{MaInstallGuidance} 누락 타입: nadena.dev.modular_avatar.core.ModularAvatarMenuItem");
                result.HasError = true;
                return result;
            }

            if (!TryGetRequiredMaTypes(out MaRequiredTypes maTypes, out string missingType))
            {
                AddMessage(
                    result.Messages,
                    MessageSeverity.Error,
                    $"{MaInstallGuidance} 누락 타입: {missingType}");
                result.HasError = true;
                return result;
            }

            if (avatarRoot == null)
            {
                AddMessage(result.Messages, MessageSeverity.Error, "Avatar Root is missing.");
                result.HasError = true;
                return result;
            }

            List<InventoryRoot> inventoryRoots = CollectInventoryRoots(avatarRoot);
            if (inventoryRoots.Count == 0 && userOutfits.Count == 0)
            {
                AddMessage(result.Messages, MessageSeverity.Error, "ClosetMenuRoot가 없습니다. 하이어라키 우클릭 [Inventory 기능 > 메뉴 지정]으로 먼저 지정하세요.");
                result.HasError = true;
                return result;
            }

            if (inventoryRoots.Count > 0)
            {
                ValidateInventoryHierarchy(avatarRoot, inventoryRoots, result.Messages);
            }

            for (int i = 0; i < userOutfits.Count; i++)
            {
                OutfitInput outfit = userOutfits[i];
                if (outfit.TargetGameObject == null)
                {
                    AddMessage(result.Messages, MessageSeverity.Error, $"Outfit #{i + 1} target is missing.");
                    continue;
                }

                if (!IsUnderAvatarRoot(avatarRoot, outfit.TargetGameObject))
                {
                    AddMessage(result.Messages, MessageSeverity.Error, $"Outfit '{GetOutfitName(outfit)}' target is outside Avatar Root hierarchy.");
                }
            }

            ValidateParameterCollision(avatarRoot, userOutfits, result.Messages);

            List<GameObject> modules = FindModuleCandidates(avatarRoot);
            if (modules.Count > 1)
            {
                result.NeedsRepair = true;
                AddMessage(result.Messages, MessageSeverity.Warning, "Duplicate AvatarClosetModule objects detected. Repair is required.");
            }

            GameObject primaryModule = modules.FirstOrDefault();
            AvatarClosetRegistrationStore store = FindRegistrationStore(avatarRoot);

            if (primaryModule != null)
            {
                AvatarClosetModuleMetadata metadata = primaryModule.GetComponent<AvatarClosetModuleMetadata>();
                if (metadata == null)
                {
                    result.NeedsRepair = true;
                    AddMessage(result.Messages, MessageSeverity.Warning, "Module metadata is missing. Repair is required.");
                }
                else
                {
                    if (metadata.SchemaVersion != CurrentModuleSchemaVersion)
                    {
                        result.NeedsRepair = true;
                        AddMessage(result.Messages, MessageSeverity.Warning, "Module schema version is outdated. Repair is required.");
                    }

                    if (!string.Equals(metadata.MarkerId, ExpectedModuleMarker, StringComparison.Ordinal))
                    {
                        result.NeedsRepair = true;
                        AddMessage(result.Messages, MessageSeverity.Warning, "Module marker mismatch detected. Repair is required.");
                    }
                }

                if (!HasExpectedStructure(primaryModule, maTypes))
                {
                    result.NeedsRepair = true;
                    AddMessage(result.Messages, MessageSeverity.Warning, "Module structure is not as expected. Repair is required.");
                }

                if (store == null || !IsStoreHealthy(store))
                {
                    result.NeedsRepair = true;
                    AddMessage(result.Messages, MessageSeverity.Warning, "RegistrationStore is missing/corrupted while module exists. Repair is required.");
                }
            }

            result.HasError = result.Messages.Any(m => m.Severity == MessageSeverity.Error);
            result.HasWarning = result.Messages.Any(m => m.Severity == MessageSeverity.Warning);

            if (!result.HasError && !result.HasWarning)
            {
                AddMessage(result.Messages, MessageSeverity.Info, "OK - validation passed.");
            }

            return result;
        }

        public RepairResult RepairIfNeeded(PipelineRequest request, ValidationResult validation)
        {
            RepairResult result = new RepairResult();
            GameObject avatarRoot = request.AvatarRoot;
            List<InventoryRoot> inventoryRoots = CollectInventoryRoots(avatarRoot);
            List<OutfitInput> userOutfits = NormalizeOutfits(request.UserOutfits);
            AvatarClosetRegistrationStore store = FindRegistrationStore(avatarRoot);
            List<OutfitInput> storeOutfits = LoadOutfitsFromStore(store);

            if (inventoryRoots.Count > 0)
            {
                result.EffectiveOutfits = BuildLegacyOutfitInputsFromInventory(inventoryRoots);
            }

            if (inventoryRoots.Count == 0 && storeOutfits.Count > 0 && userOutfits.Count > 0 && !AreOutfitSetsEquivalent(storeOutfits, userOutfits))
            {
                result.HasError = true;
                AddMessage(result.Messages, MessageSeverity.Error,
                    "Conflict detected between user input outfits and RegistrationStore metadata. Repair stopped. Resolve conflict before Apply.");
                return result;
            }

            if (inventoryRoots.Count == 0)
            {
                result.EffectiveOutfits = storeOutfits.Count > 0 ? storeOutfits : userOutfits;
            }

            if (result.EffectiveOutfits.Count == 0)
            {
                result.HasError = true;
                AddMessage(result.Messages, MessageSeverity.Error,
                    "No usable outfit data found in user input or RegistrationStore.");
                return result;
            }

            if (!validation.NeedsRepair)
            {
                AddMessage(result.Messages, MessageSeverity.Info, "No repair needed.");
                return result;
            }

            try
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Repair Avatar Closet Module");

                List<GameObject> modules = FindModuleCandidates(avatarRoot);
                for (int i = 0; i < modules.Count; i++)
                {
                    Undo.DestroyObjectImmediate(modules[i]);
                }

                GameObject module = CreateModuleObject(avatarRoot);
                if (inventoryRoots.Count > 0)
                {
                    RebuildModuleContentsFromInventory(module, avatarRoot, inventoryRoots);
                }
                else
                {
                    RebuildModuleContents(module, avatarRoot, result.EffectiveOutfits);
                }

                AvatarClosetRegistrationStore targetStore = store ?? CreateRegistrationStore(avatarRoot);
                SaveOutfitsToStore(targetStore, avatarRoot, result.EffectiveOutfits);

                PrefabUtility.RecordPrefabInstancePropertyModifications(avatarRoot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(module);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetStore);

                EditorUtility.SetDirty(avatarRoot);
                EditorUtility.SetDirty(module);
                EditorUtility.SetDirty(targetStore);

                Undo.CollapseUndoOperations(group);
                result.DidRepair = true;
                AddMessage(result.Messages, MessageSeverity.Info, "Repair completed by recreating AvatarClosetModule.");
            }
            catch (Exception ex)
            {
                result.HasError = true;
                AddMessage(result.Messages, MessageSeverity.Error, $"Repair failed: {ex.Message}");
            }

            return result;
        }

        public bool ApplyChanges(GameObject avatarRoot, IReadOnlyList<OutfitInput> effectiveOutfits, out List<PipelineMessage> messages)
        {
            messages = new List<PipelineMessage>();
            List<InventoryRoot> inventoryRoots = CollectInventoryRoots(avatarRoot);

            if (avatarRoot == null)
            {
                AddMessage(messages, MessageSeverity.Error, "Apply failed: Avatar Root is missing.");
                return false;
            }

            List<OutfitInput> outfits = inventoryRoots.Count > 0
                ? BuildLegacyOutfitInputsFromInventory(inventoryRoots)
                : NormalizeOutfits(effectiveOutfits);
            if (outfits.Count == 0)
            {
                AddMessage(messages, MessageSeverity.Error, "Apply failed: no valid outfits to apply.");
                return false;
            }

            for (int i = 0; i < outfits.Count; i++)
            {
                if (!IsUnderAvatarRoot(avatarRoot, outfits[i].TargetGameObject))
                {
                    AddMessage(messages, MessageSeverity.Error,
                        $"Apply failed: outfit '{GetOutfitName(outfits[i])}' target is outside Avatar Root.");
                    return false;
                }
            }

            try
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply Avatar Closet Module");

                List<GameObject> modules = FindModuleCandidates(avatarRoot);
                GameObject module;
                if (modules.Count == 0)
                {
                    module = CreateModuleObject(avatarRoot);
                }
                else
                {
                    module = modules[0];
                    for (int i = 1; i < modules.Count; i++)
                    {
                        Undo.DestroyObjectImmediate(modules[i]);
                    }
                }

                if (inventoryRoots.Count > 0)
                {
                    RebuildModuleContentsFromInventory(module, avatarRoot, inventoryRoots);
                }
                else
                {
                    RebuildModuleContents(module, avatarRoot, outfits);
                }

                AvatarClosetRegistrationStore store = FindRegistrationStore(avatarRoot) ?? CreateRegistrationStore(avatarRoot);
                SaveOutfitsToStore(store, avatarRoot, outfits);

                PrefabUtility.RecordPrefabInstancePropertyModifications(avatarRoot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(module);
                PrefabUtility.RecordPrefabInstancePropertyModifications(store);

                EditorUtility.SetDirty(avatarRoot);
                EditorUtility.SetDirty(module);
                EditorUtility.SetDirty(store);

                Undo.CollapseUndoOperations(group);
                AddMessage(messages, MessageSeverity.Info, "Apply completed. Module updated idempotently.");
                return true;
            }
            catch (Exception ex)
            {
                AddMessage(messages, MessageSeverity.Error, $"Apply failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsStoreHealthy(AvatarClosetRegistrationStore store)
        {
            if (store == null || store.Outfits == null || store.Outfits.Count == 0)
            {
                return false;
            }

            return store.Outfits.All(record =>
                record != null &&
                record.TargetGameObject != null &&
                !string.IsNullOrWhiteSpace(record.ParameterKey));
        }

        private static List<InventoryRoot> CollectInventoryRoots(GameObject avatarRoot)
        {
            List<InventoryRoot> roots = new List<InventoryRoot>();
            if (avatarRoot == null)
            {
                return roots;
            }

            ClosetMenuRoot[] menuRoots = avatarRoot.GetComponentsInChildren<ClosetMenuRoot>(true);
            for (int i = 0; i < menuRoots.Length; i++)
            {
                ClosetMenuRoot menuRoot = menuRoots[i];
                if (menuRoot == null)
                {
                    continue;
                }

                InventoryRoot root = new InventoryRoot
                {
                    Component = menuRoot,
                    DisplayName = menuRoot.EffectiveDisplayName,
                    NamespacePrefix = menuRoot.NamespacePrefix ?? string.Empty
                };
                root.SetParameterName = string.IsNullOrWhiteSpace(root.NamespacePrefix)
                    ? DefaultSetParameterName
                    : $"{root.NamespacePrefix}_{DefaultSetParameterName}";

                ClosetOutfitSet[] sets = menuRoot.GetComponentsInChildren<ClosetOutfitSet>(true);
                for (int s = 0; s < sets.Length; s++)
                {
                    ClosetOutfitSet set = sets[s];
                    if (set == null || set.transform == menuRoot.transform)
                    {
                        continue;
                    }

                    InventorySet inventorySet = new InventorySet
                    {
                        Component = set,
                        DisplayName = set.EffectiveDisplayName
                    };

                    ClosetOutfitPart[] parts = set.GetComponentsInChildren<ClosetOutfitPart>(true);
                    for (int p = 0; p < parts.Length; p++)
                    {
                        ClosetOutfitPart part = parts[p];
                        if (part == null)
                        {
                            continue;
                        }

                        InventoryPart inventoryPart = new InventoryPart
                        {
                            Component = part,
                            ParameterName = BuildPartParameterName(root, set, part)
                        };
                        inventorySet.Parts.Add(inventoryPart);
                    }

                    root.Sets.Add(inventorySet);
                }

                roots.Add(root);
            }

            return roots;
        }

        private static void ValidateInventoryHierarchy(GameObject avatarRoot, IReadOnlyList<InventoryRoot> roots, List<PipelineMessage> messages)
        {
            if (roots.Count == 0)
            {
                return;
            }

            ClosetOutfitSet[] allSets = avatarRoot.GetComponentsInChildren<ClosetOutfitSet>(true);
            for (int i = 0; i < allSets.Length; i++)
            {
                ClosetOutfitSet set = allSets[i];
                if (set == null)
                {
                    continue;
                }

                ClosetMenuRoot parentRoot = set.GetComponentInParent<ClosetMenuRoot>(true);
                if (parentRoot == null || !set.transform.IsChildOf(parentRoot.transform))
                {
                    AddMessage(messages, MessageSeverity.Error, $"OutfitSet '{set.gameObject.name}' is not under ClosetMenuRoot.");
                }
            }

            ClosetOutfitPart[] allParts = avatarRoot.GetComponentsInChildren<ClosetOutfitPart>(true);
            for (int i = 0; i < allParts.Length; i++)
            {
                ClosetOutfitPart part = allParts[i];
                if (part == null)
                {
                    continue;
                }

                ClosetOutfitSet parentSet = part.GetComponentInParent<ClosetOutfitSet>(true);
                if (parentSet == null || !part.transform.IsChildOf(parentSet.transform))
                {
                    AddMessage(messages, MessageSeverity.Error, $"OutfitPart '{part.gameObject.name}' is not under ClosetOutfitSet.");
                }
            }

            for (int i = 0; i < roots.Count; i++)
            {
                InventoryRoot root = roots[i];
                if (root.Sets.Count == 0)
                {
                    AddMessage(messages, MessageSeverity.Warning, $"ClosetMenuRoot '{root.DisplayName}' has no OutfitSet.");
                    continue;
                }

                Dictionary<int, int> indexCounts = new Dictionary<int, int>();
                for (int s = 0; s < root.Sets.Count; s++)
                {
                    int index = root.Sets[s].Component.SetIndex;
                    indexCounts.TryGetValue(index, out int count);
                    indexCounts[index] = count + 1;
                }

                foreach (KeyValuePair<int, int> pair in indexCounts)
                {
                    if (pair.Value > 1)
                    {
                        AddMessage(messages, MessageSeverity.Error, $"ClosetMenuRoot '{root.DisplayName}' has duplicate setIndex '{pair.Key}'.");
                    }
                }
            }
        }

        private static List<OutfitInput> BuildLegacyOutfitInputsFromInventory(IReadOnlyList<InventoryRoot> roots)
        {
            List<OutfitInput> outfits = new List<OutfitInput>();
            for (int i = 0; i < roots.Count; i++)
            {
                InventoryRoot root = roots[i];
                for (int s = 0; s < root.Sets.Count; s++)
                {
                    InventorySet set = root.Sets[s];
                    outfits.Add(new OutfitInput
                    {
                        DisplayName = set.DisplayName,
                        TargetGameObject = set.Component.gameObject,
                        OptionalGroupName = root.DisplayName
                    });
                }
            }

            return outfits;
        }

        private static bool HasExpectedStructure(GameObject module, MaRequiredTypes maTypes)
        {
            if (module == null)
            {
                return false;
            }

            bool hasParameters = module.GetComponent(maTypes.ParametersType) != null;
            bool hasMergeAnimator = module.GetComponent(maTypes.MergeAnimatorType) != null;
            bool hasMenuItems = module.GetComponentsInChildren(maTypes.MenuItemType, true).Length > 0;
            bool hasObjectToggles = module.GetComponentsInChildren(maTypes.ObjectToggleType, true).Length > 0;

            return hasParameters && hasMergeAnimator && hasMenuItems && hasObjectToggles;
        }

        private static void ValidateParameterCollision(GameObject avatarRoot, IReadOnlyList<OutfitInput> outfits, List<PipelineMessage> messages)
        {
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < outfits.Count; i++)
            {
                string key = BuildParameterKey(avatarRoot, outfits[i], i);
                seen.TryGetValue(key, out int count);
                seen[key] = count + 1;
            }

            foreach (KeyValuePair<string, int> pair in seen)
            {
                if (pair.Value > 1)
                {
                    AddMessage(messages, MessageSeverity.Error, $"Parameter key collision detected: {pair.Key}");
                }
            }
        }

        private static List<OutfitInput> NormalizeOutfits(IReadOnlyList<OutfitInput> source)
        {
            if (source == null)
            {
                return new List<OutfitInput>();
            }

            return source
                .Where(outfit => outfit != null && outfit.TargetGameObject != null)
                .Select(outfit => new OutfitInput
                {
                    DisplayName = outfit.DisplayName ?? string.Empty,
                    TargetGameObject = outfit.TargetGameObject,
                    OptionalGroupName = outfit.OptionalGroupName ?? string.Empty
                })
                .ToList();
        }

        private static bool AreOutfitSetsEquivalent(IReadOnlyList<OutfitInput> a, IReadOnlyList<OutfitInput> b)
        {
            List<string> sa = a.Select(BuildOutfitSignature).OrderBy(x => x, StringComparer.Ordinal).ToList();
            List<string> sb = b.Select(BuildOutfitSignature).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (sa.Count != sb.Count)
            {
                return false;
            }

            for (int i = 0; i < sa.Count; i++)
            {
                if (!string.Equals(sa[i], sb[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildOutfitSignature(OutfitInput input)
        {
            int targetId = input.TargetGameObject != null ? input.TargetGameObject.GetInstanceID() : 0;
            string display = string.IsNullOrWhiteSpace(input.DisplayName) ? "<empty>" : input.DisplayName.Trim();
            string group = string.IsNullOrWhiteSpace(input.OptionalGroupName) ? "<empty>" : input.OptionalGroupName.Trim();
            return $"{targetId}|{display}|{group}";
        }

        private static void AddMessage(List<PipelineMessage> messages, MessageSeverity severity, string text)
        {
            messages.Add(new PipelineMessage
            {
                Severity = severity,
                Text = text
            });
        }

        private static string BuildSummary(IReadOnlyList<PipelineMessage> messages, bool applied, bool hasError)
        {
            int errors = messages.Count(m => m.Severity == MessageSeverity.Error);
            int warnings = messages.Count(m => m.Severity == MessageSeverity.Warning);
            int infos = messages.Count(m => m.Severity == MessageSeverity.Info);

            if (hasError)
            {
                return $"Pipeline failed. Errors: {errors}, Warnings: {warnings}, Info: {infos}.";
            }

            return applied
                ? $"Pipeline done. Errors: {errors}, Warnings: {warnings}, Info: {infos}."
                : $"Pipeline done without apply. Errors: {errors}, Warnings: {warnings}, Info: {infos}.";
        }

        private static void SetStatus(string status, Action<string> callback)
        {
            callback?.Invoke(status);
        }

        private static bool IsUnderAvatarRoot(GameObject avatarRoot, GameObject target)
        {
            if (avatarRoot == null || target == null || target == avatarRoot)
            {
                return false;
            }

            return target.transform.IsChildOf(avatarRoot.transform);
        }

        private static string GetOutfitName(OutfitInput input)
        {
            if (!string.IsNullOrWhiteSpace(input.DisplayName))
            {
                return input.DisplayName.Trim();
            }

            return input.TargetGameObject != null ? input.TargetGameObject.name : "<null>";
        }

        private static List<GameObject> FindModuleCandidates(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return new List<GameObject>();
            }

            List<GameObject> modules = new List<GameObject>();
            for (int i = 0; i < avatarRoot.transform.childCount; i++)
            {
                Transform child = avatarRoot.transform.GetChild(i);
                if (child.name == ModuleObjectName)
                {
                    modules.Add(child.gameObject);
                }
            }

            return modules;
        }

        private static AvatarClosetRegistrationStore FindRegistrationStore(GameObject avatarRoot)
        {
            return avatarRoot != null ? avatarRoot.GetComponentInChildren<AvatarClosetRegistrationStore>(true) : null;
        }

        private static List<OutfitInput> LoadOutfitsFromStore(AvatarClosetRegistrationStore store)
        {
            List<OutfitInput> outfits = new List<OutfitInput>();
            if (store == null)
            {
                return outfits;
            }

            for (int i = 0; i < store.Outfits.Count; i++)
            {
                AvatarClosetRegistrationStore.OutfitRecord record = store.Outfits[i];
                if (record == null || record.TargetGameObject == null)
                {
                    continue;
                }

                outfits.Add(new OutfitInput
                {
                    DisplayName = record.DisplayName ?? string.Empty,
                    TargetGameObject = record.TargetGameObject,
                    OptionalGroupName = record.OptionalGroupName ?? string.Empty
                });
            }

            return outfits;
        }

        private static AvatarClosetRegistrationStore CreateRegistrationStore(GameObject avatarRoot)
        {
            GameObject holder = new GameObject(RegistrationStoreObjectName);
            Undo.RegisterCreatedObjectUndo(holder, "Create Avatar Closet Registration Store");
            Undo.SetTransformParent(holder.transform, avatarRoot.transform, "Parent Registration Store");
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            AvatarClosetRegistrationStore store = Undo.AddComponent<AvatarClosetRegistrationStore>(holder);
            EditorUtility.SetDirty(store);
            return store;
        }

        private static void SaveOutfitsToStore(AvatarClosetRegistrationStore store, GameObject avatarRoot, IReadOnlyList<OutfitInput> outfits)
        {
            store.Outfits.Clear();
            for (int i = 0; i < outfits.Count; i++)
            {
                OutfitInput outfit = outfits[i];
                if (!IsUnderAvatarRoot(avatarRoot, outfit.TargetGameObject))
                {
                    continue;
                }

                store.Outfits.Add(new AvatarClosetRegistrationStore.OutfitRecord
                {
                    DisplayName = outfit.DisplayName ?? string.Empty,
                    TargetGameObject = outfit.TargetGameObject,
                    OptionalGroupName = outfit.OptionalGroupName ?? string.Empty,
                    ParameterKey = BuildParameterKey(avatarRoot, outfit, i),
                    LastBindingFingerprint = BuildBindingFingerprint(outfit.TargetGameObject)
                });
            }

            EditorUtility.SetDirty(store);
        }

        private static GameObject CreateModuleObject(GameObject avatarRoot)
        {
            GameObject module = new GameObject(ModuleObjectName);
            Undo.RegisterCreatedObjectUndo(module, "Create Avatar Closet Module");
            Undo.SetTransformParent(module.transform, avatarRoot.transform, "Parent Avatar Closet Module");
            module.transform.localPosition = Vector3.zero;
            module.transform.localRotation = Quaternion.identity;
            module.transform.localScale = Vector3.one;
            return module;
        }

        private static void RebuildModuleContents(GameObject module, GameObject avatarRoot, IReadOnlyList<OutfitInput> outfits)
        {
            MaRequiredTypes required = GetRequiredMaTypesOrThrow();

            ClearModuleChildren(module);
            RemoveComponentIfExists(module, required.ParametersType);
            RemoveComponentIfExists(module, required.MergeAnimatorType);

            Component parametersComponent = EnsureComponentOrThrow(module, required.ParametersType, "MA Parameters");
            EnsureComponentOrThrow(module, required.MergeAnimatorType, "MA MergeAnimator");

            List<GeneratedOutfitData> generated = new List<GeneratedOutfitData>(outfits.Count);
            for (int i = 0; i < outfits.Count; i++)
            {
                OutfitInput outfit = outfits[i];
                string displayName = string.IsNullOrWhiteSpace(outfit.DisplayName) ? outfit.TargetGameObject.name : outfit.DisplayName.Trim();
                string parameterKey = BuildParameterKey(avatarRoot, outfit, i);

                GameObject outfitNode = new GameObject($"Outfit_{SanitizeName(displayName)}_{i + 1}");
                Undo.RegisterCreatedObjectUndo(outfitNode, "Create Outfit Node");
                Undo.SetTransformParent(outfitNode.transform, module.transform, "Parent Outfit Node");

                Component toggleComponent = EnsureComponentOrThrow(outfitNode, required.ObjectToggleType, "MA Object Toggle");
                ConfigureObjectToggle(toggleComponent, outfit.TargetGameObject, parameterKey);

                Component menuItemComponent = EnsureComponentOrThrow(outfitNode, required.MenuItemType, "MA Menu Item");
                ConfigureMenuItem(menuItemComponent, displayName, parameterKey);

                generated.Add(new GeneratedOutfitData
                {
                    DisplayName = displayName,
                    ParameterKey = parameterKey
                });
            }

            List<GeneratedParameterData> generatedParameters = generated
                .Select(entry => GeneratedParameterData.Bool(entry.ParameterKey, false))
                .ToList();
            ConfigureParameters(parametersComponent, generatedParameters);
            EnsureModuleMetadata(module, outfits.Count);
        }

        private static void RebuildModuleContentsFromInventory(GameObject module, GameObject avatarRoot, IReadOnlyList<InventoryRoot> roots)
        {
            MaRequiredTypes required = GetRequiredMaTypesOrThrow();

            ClearModuleChildren(module);
            RemoveComponentIfExists(module, required.ParametersType);
            RemoveComponentIfExists(module, required.MergeAnimatorType);

            Component parametersComponent = EnsureComponentOrThrow(module, required.ParametersType, "MA Parameters");
            EnsureComponentOrThrow(module, required.MergeAnimatorType, "MA MergeAnimator");

            List<GeneratedParameterData> generatedParameters = new List<GeneratedParameterData>();

            for (int r = 0; r < roots.Count; r++)
            {
                InventoryRoot root = roots[r];
                generatedParameters.Add(GeneratedParameterData.Int(root.SetParameterName, 0));

                GameObject rootNode = new GameObject($"MenuRoot_{SanitizeName(root.DisplayName)}");
                Undo.RegisterCreatedObjectUndo(rootNode, "Create Menu Root Node");
                Undo.SetTransformParent(rootNode.transform, module.transform, "Parent Menu Root Node");

                for (int s = 0; s < root.Sets.Count; s++)
                {
                    InventorySet set = root.Sets[s];
                    GameObject setNode = new GameObject($"Set_{set.Component.SetIndex}_{SanitizeName(set.DisplayName)}");
                    Undo.RegisterCreatedObjectUndo(setNode, "Create Outfit Set Node");
                    Undo.SetTransformParent(setNode.transform, rootNode.transform, "Parent Outfit Set Node");

                    string setToggleParam = $"{root.SetParameterName}_IS_{set.Component.SetIndex}";
                    generatedParameters.Add(GeneratedParameterData.Bool(setToggleParam, set.Component.DefaultOn));

                    Component setToggle = EnsureComponentOrThrow(setNode, required.ObjectToggleType, "MA Object Toggle");
                    ConfigureObjectToggle(setToggle, set.Component.gameObject, setToggleParam);

                    Component setMenuItem = EnsureComponentOrThrow(setNode, required.MenuItemType, "MA Menu Item");
                    ConfigureMenuItem(setMenuItem, set.DisplayName, setToggleParam);

                    if (set.Parts.Count == 0)
                    {
                        continue;
                    }

                    GameObject partsMenuNode = new GameObject($"Parts_{SanitizeName(set.DisplayName)}");
                    Undo.RegisterCreatedObjectUndo(partsMenuNode, "Create Parts Menu Node");
                    Undo.SetTransformParent(partsMenuNode.transform, setNode.transform, "Parent Parts Menu Node");

                    for (int p = 0; p < set.Parts.Count; p++)
                    {
                        InventoryPart part = set.Parts[p];
                        string partName = part.Component.EffectiveDisplayName;
                        generatedParameters.Add(GeneratedParameterData.Bool(part.ParameterName, part.Component.DefaultOn));

                        GameObject partNode = new GameObject($"Part_{SanitizeName(partName)}_{p + 1}");
                        Undo.RegisterCreatedObjectUndo(partNode, "Create Part Node");
                        Undo.SetTransformParent(partNode.transform, partsMenuNode.transform, "Parent Part Node");

                        Component partToggle = EnsureComponentOrThrow(partNode, required.ObjectToggleType, "MA Object Toggle");
                        ConfigureObjectToggle(partToggle, part.Component.gameObject, part.ParameterName);

                        Component partMenuItem = EnsureComponentOrThrow(partNode, required.MenuItemType, "MA Menu Item");
                        ConfigureMenuItem(partMenuItem, partName, part.ParameterName);
                    }
                }
            }

            ConfigureParameters(parametersComponent, generatedParameters);
            EnsureModuleMetadata(module, generatedParameters.Count);
        }

        private static void EnsureModuleMetadata(GameObject module, int outfitCount)
        {
            AvatarClosetModuleMetadata metadata = module.GetComponent<AvatarClosetModuleMetadata>();
            if (metadata == null)
            {
                metadata = Undo.AddComponent<AvatarClosetModuleMetadata>(module);
            }

            metadata.SchemaVersion = CurrentModuleSchemaVersion;
            metadata.GeneratedOutfitCount = outfitCount;
            metadata.GeneratorVersion = AvatarClosetRuntimeMarker.PackageId;
            metadata.MarkerId = ExpectedModuleMarker;
            EditorUtility.SetDirty(metadata);
        }

        private static void ClearModuleChildren(GameObject module)
        {
            for (int i = module.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(module.transform.GetChild(i).gameObject);
            }
        }

        private static void RemoveComponentIfExists(GameObject target, Type componentType)
        {
            if (target == null || componentType == null)
            {
                return;
            }

            Component existing = target.GetComponent(componentType);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }
        }

        private static Component EnsureComponent(GameObject target, Type componentType)
        {
            if (target == null || componentType == null)
            {
                return null;
            }

            Component existing = target.GetComponent(componentType);
            return existing != null ? existing : Undo.AddComponent(target, componentType);
        }

        private static Component EnsureComponentOrThrow(GameObject target, Type componentType, string componentLabel)
        {
            Component component = EnsureComponent(target, componentType);
            if (component == null)
            {
                throw new InvalidOperationException($"Failed to add required {componentLabel} component.");
            }

            return component;
        }

        private static MaRequiredTypes GetRequiredMaTypesOrThrow()
        {
            if (TryGetRequiredMaTypes(out MaRequiredTypes required, out string missingType))
            {
                return required;
            }

            throw new InvalidOperationException($"{MaInstallGuidance} 누락 타입: {missingType}");
        }

        private static bool TryGetRequiredMaTypes(out MaRequiredTypes required, out string missingType)
        {
            required = new MaRequiredTypes
            {
                ParametersType = ResolveType(MaParametersTypeNames),
                MenuItemType = ResolveType(MaMenuItemTypeNames),
                ObjectToggleType = ResolveType(MaObjectToggleTypeNames),
                MergeAnimatorType = ResolveType(MaMergeAnimatorTypeNames)
            };

            if (required.ParametersType == null)
            {
                missingType = "ModularAvatarParameters";
                return false;
            }

            if (required.MenuItemType == null)
            {
                missingType = "ModularAvatarMenuItem";
                return false;
            }

            if (required.ObjectToggleType == null)
            {
                missingType = "ModularAvatarObjectToggle";
                return false;
            }

            if (required.MergeAnimatorType == null)
            {
                missingType = "ModularAvatarMergeAnimator";
                return false;
            }

            missingType = string.Empty;
            return true;
        }

        private static bool TryResolveType(string typeName, out Type type)
        {
            type = Type.GetType(typeName, false);
            if (type != null)
            {
                return true;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, false);
                if (type != null)
                {
                    return true;
                }
            }

            type = null;
            return false;
        }

        private static Type ResolveType(IEnumerable<string> candidates)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (string candidate in candidates)
            {
                Type direct = Type.GetType(candidate, false);
                if (direct != null)
                {
                    return direct;
                }

                foreach (Assembly assembly in assemblies)
                {
                    Type fromAssembly = assembly.GetType(candidate, false);
                    if (fromAssembly != null)
                    {
                        return fromAssembly;
                    }

                    try
                    {
                        Type byName = assembly.GetTypes().FirstOrDefault(type => type.Name.Equals(candidate, StringComparison.Ordinal));
                        if (byName != null)
                        {
                            return byName;
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // Ignore partially loadable assemblies.
                    }
                }
            }

            return null;
        }

        private static string BuildParameterKey(GameObject avatarRoot, OutfitInput outfit, int index)
        {
            string rootName = avatarRoot != null ? avatarRoot.name : "Avatar";
            string targetPath = outfit.TargetGameObject != null ? GetRelativePath(outfit.TargetGameObject.transform) : "<null>";
            string displayName = string.IsNullOrWhiteSpace(outfit.DisplayName) ? "<empty>" : outfit.DisplayName.Trim();
            string groupName = string.IsNullOrWhiteSpace(outfit.OptionalGroupName) ? "<empty>" : outfit.OptionalGroupName.Trim();
            string seed = $"{rootName}|{targetPath}|{displayName}|{groupName}|{index}";
            string hash = Hash128.Compute(seed).ToString().Substring(0, 8).ToUpperInvariant();
            return $"ACT_{hash}";
        }

        private static string BuildBindingFingerprint(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return "<null>";
            }

            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                sb.Append(GetRelativePath(renderer.transform));
                sb.Append('|');
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material mat = materials[m];
                    string matName = mat != null ? mat.name : "<null-mat>";
                    string texName = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "<null-tex>";
                    sb.Append(matName);
                    sb.Append(':');
                    sb.Append(texName);
                    sb.Append(';');
                }

                sb.Append("||");
            }

            return Hash128.Compute(sb.ToString()).ToString();
        }

        private static string GetRelativePath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            StringBuilder sb = new StringBuilder(target.name);
            Transform current = target.parent;
            while (current != null)
            {
                sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }

            return sb.ToString();
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Outfit";
            }

            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return sb.ToString();
        }

        private static void ConfigureObjectToggle(Component toggleComponent, GameObject targetObject, string parameterKey)
        {
            if (toggleComponent == null)
            {
                throw new InvalidOperationException("Failed to configure MA Object Toggle: component is null.");
            }

            TrySetObjectRefOrThrow(toggleComponent, new[] { "targetObject", "TargetObject", "objectReference", "Object" }, targetObject, "target object");
            TrySetStringOrThrow(toggleComponent, new[] { "parameter", "Parameter", "parameterName", "internalParameter" }, parameterKey, "parameter key");
            TrySetBoolOrThrow(toggleComponent, new[] { "saved", "Saved", "isSaved" }, true, "saved flag");
            TrySetBoolOrThrow(toggleComponent, new[] { "synced", "Synced", "isSynced", "networkSynced" }, true, "synced flag");
            EditorUtility.SetDirty(toggleComponent);
        }

        private static void ConfigureMenuItem(Component menuItemComponent, string displayName, string parameterKey)
        {
            if (menuItemComponent == null)
            {
                throw new InvalidOperationException("Failed to configure MA Menu Item: component is null.");
            }

            TrySetStringOrThrow(menuItemComponent, new[] { "name", "Name", "menuName", "MenuName", "label", "Label" }, displayName, "menu display name");
            TrySetStringOrThrow(menuItemComponent, new[] { "parameter", "Parameter", "parameterName", "internalParameter" }, parameterKey, "menu parameter key");
            EditorUtility.SetDirty(menuItemComponent);
        }

        private static void ConfigureParameters(Component parametersComponent, IReadOnlyList<GeneratedParameterData> parameters)
        {
            if (parametersComponent == null)
            {
                throw new InvalidOperationException("Failed to configure MA Parameters: component is null.");
            }

            SerializedObject serialized = new SerializedObject(parametersComponent);
            SerializedProperty arrayProperty = FindFirstArrayProperty(serialized, "parameters", "parameterList", "params", "Parameters");
            if (arrayProperty == null)
            {
                throw new InvalidOperationException("Failed to configure MA Parameters: parameter array field was not found.");
            }

            arrayProperty.ClearArray();
            for (int i = 0; i < parameters.Count; i++)
            {
                arrayProperty.InsertArrayElementAtIndex(i);
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                if (element == null)
                {
                    throw new InvalidOperationException("Failed to configure MA Parameters: inserted element is null.");
                }

                SetStringFieldOrThrow(element, parameters[i].DisplayName, "parameter display name", "name", "Name", "parameter", "Parameter", "parameterName");
                SetStringFieldOrThrow(element, parameters[i].ParameterName, "parameter key", "internalParameter", "InternalParameter", "key", "Key", "internalName");
                SetBoolFieldOrThrow(element, true, "saved flag", "saved", "Saved", "isSaved");
                SetBoolFieldOrThrow(element, true, "synced flag", "synced", "Synced", "isSynced", "networkSynced");
                SetFloatFieldOrThrow(element, parameters[i].DefaultValue, "default value", "defaultValue", "DefaultValue", "value");
                TrySetBoolField(element, parameters[i].IsBool, "isBool", "IsBool");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(parametersComponent);
        }

        private static SerializedProperty FindFirstArrayProperty(SerializedObject serialized, params string[] names)
        {
            foreach (string name in names)
            {
                SerializedProperty prop = serialized.FindProperty(name);
                if (prop != null && prop.isArray)
                {
                    return prop;
                }
            }

            return null;
        }

        private static void SetStringFieldOrThrow(SerializedProperty element, string value, string fieldLabel, params string[] fieldNames)
        {
            if (!TrySetStringField(element, value, fieldNames))
            {
                throw new InvalidOperationException($"Failed to configure MA Parameters: {fieldLabel} field was not found.");
            }
        }

        private static void SetBoolFieldOrThrow(SerializedProperty element, bool value, string fieldLabel, params string[] fieldNames)
        {
            if (!TrySetBoolField(element, value, fieldNames))
            {
                throw new InvalidOperationException($"Failed to configure MA Parameters: {fieldLabel} field was not found.");
            }
        }

        private static void SetFloatFieldOrThrow(SerializedProperty element, float value, string fieldLabel, params string[] fieldNames)
        {
            if (!TrySetFloatField(element, value, fieldNames))
            {
                throw new InvalidOperationException($"Failed to configure MA Parameters: {fieldLabel} field was not found.");
            }
        }

        private static bool TrySetStringField(SerializedProperty element, string value, params string[] fieldNames)
        {
            foreach (string fieldName in fieldNames)
            {
                SerializedProperty field = element.FindPropertyRelative(fieldName);
                if (field != null && field.propertyType == SerializedPropertyType.String)
                {
                    field.stringValue = value;
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetBoolField(SerializedProperty element, bool value, params string[] fieldNames)
        {
            foreach (string fieldName in fieldNames)
            {
                SerializedProperty field = element.FindPropertyRelative(fieldName);
                if (field != null && field.propertyType == SerializedPropertyType.Boolean)
                {
                    field.boolValue = value;
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetFloatField(SerializedProperty element, float value, params string[] fieldNames)
        {
            foreach (string fieldName in fieldNames)
            {
                SerializedProperty field = element.FindPropertyRelative(fieldName);
                if (field == null)
                {
                    continue;
                }

                if (field.propertyType == SerializedPropertyType.Float)
                {
                    field.floatValue = value;
                    return true;
                }

                if (field.propertyType == SerializedPropertyType.Integer)
                {
                    field.intValue = Mathf.RoundToInt(value);
                    return true;
                }
            }

            return false;
        }

        private static void TrySetStringOrThrow(Component component, IEnumerable<string> memberNames, string value, string fieldLabel)
        {
            foreach (string memberName in memberNames)
            {
                if (TrySetMember(component, memberName, value))
                {
                    return;
                }
            }

            throw new InvalidOperationException($"Failed to configure {component.GetType().Name}: could not set {fieldLabel}.");
        }

        private static void TrySetBoolOrThrow(Component component, IEnumerable<string> memberNames, bool value, string fieldLabel)
        {
            foreach (string memberName in memberNames)
            {
                if (TrySetMember(component, memberName, value))
                {
                    return;
                }
            }

            throw new InvalidOperationException($"Failed to configure {component.GetType().Name}: could not set {fieldLabel}.");
        }

        private static void TrySetObjectRefOrThrow(Component component, IEnumerable<string> memberNames, UnityEngine.Object value, string fieldLabel)
        {
            foreach (string memberName in memberNames)
            {
                if (TrySetMember(component, memberName, value))
                {
                    return;
                }
            }

            throw new InvalidOperationException($"Failed to configure {component.GetType().Name}: could not set {fieldLabel}.");
        }

        private static bool TrySetMember(Component component, string memberName, object value)
        {
            if (component == null)
            {
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = component.GetType();

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                if (value == null)
                {
                    if (!field.FieldType.IsValueType || Nullable.GetUnderlyingType(field.FieldType) != null)
                    {
                        field.SetValue(component, null);
                        return true;
                    }
                }
                else if (field.FieldType.IsAssignableFrom(value.GetType()))
                {
                    field.SetValue(component, value);
                    return true;
                }
            }

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite)
            {
                Type targetType = property.PropertyType;
                if (value == null || targetType.IsAssignableFrom(value.GetType()))
                {
                    property.SetValue(component, value);
                    return true;
                }
            }

            return false;
        }

        private static string BuildPartParameterName(InventoryRoot root, ClosetOutfitSet set, ClosetOutfitPart part)
        {
            string rootPrefix = string.IsNullOrWhiteSpace(root.NamespacePrefix) ? "ACT" : root.NamespacePrefix;
            string setName = SanitizeName(set.EffectiveDisplayName);
            string partName = SanitizeName(part.EffectiveDisplayName);
            string hash = Hash128.Compute($"{root.Component.GetInstanceID()}|{set.GetInstanceID()}|{part.GetInstanceID()}").ToString().Substring(0, 6).ToUpperInvariant();
            return $"{rootPrefix}_PART_{setName}_{partName}_{hash}";
        }

        private sealed class GeneratedOutfitData
        {
            public string DisplayName;
            public string ParameterKey;
        }

        private sealed class GeneratedParameterData
        {
            public string DisplayName;
            public string ParameterName;
            public float DefaultValue;
            public bool IsBool;

            public static GeneratedParameterData Bool(string parameterName, bool defaultOn)
            {
                return new GeneratedParameterData
                {
                    DisplayName = parameterName,
                    ParameterName = parameterName,
                    DefaultValue = defaultOn ? 1f : 0f,
                    IsBool = true
                };
            }

            public static GeneratedParameterData Int(string parameterName, int defaultValue)
            {
                return new GeneratedParameterData
                {
                    DisplayName = parameterName,
                    ParameterName = parameterName,
                    DefaultValue = defaultValue,
                    IsBool = false
                };
            }
        }
    }
}
