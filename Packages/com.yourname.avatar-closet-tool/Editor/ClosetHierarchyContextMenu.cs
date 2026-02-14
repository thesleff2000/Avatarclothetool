using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YourName.AvatarClosetTool.Runtime;

namespace YourName.AvatarClosetTool.Editor
{
    public static class ClosetHierarchyContextMenu
    {
        private const bool Enabled = false;

        [MenuItem("GameObject/Inventory 기능/메뉴 지정", false, 10)]
        private static void AssignMenuRootFromMenu()
        {
            TryAssignMenuRoot(Selection.activeGameObject, out _);
        }

        [MenuItem("GameObject/Inventory 기능/메뉴 지정", true)]
        private static bool ValidateAssignMenuRootFromMenu()
        {
            return Enabled && Selection.activeGameObject != null;
        }

        [MenuItem("GameObject/Inventory 기능/옷 지정", false, 11)]
        private static void AssignOutfitSetFromMenu()
        {
            TryAssignOutfitSet(Selection.activeGameObject, out _);
        }

        [MenuItem("GameObject/Inventory 기능/옷 지정", true)]
        private static bool ValidateAssignOutfitSetFromMenu()
        {
            return Enabled && Selection.activeGameObject != null;
        }

        [MenuItem("GameObject/Inventory 기능/옷 파츠 지정", false, 12)]
        private static void AssignOutfitPartFromMenu()
        {
            TryAssignOutfitPart(Selection.activeGameObject, out _);
        }

        [MenuItem("GameObject/Inventory 기능/옷 파츠 지정", true)]
        private static bool ValidateAssignOutfitPartFromMenu()
        {
            return Enabled && Selection.activeGameObject != null;
        }

        [MenuItem("GameObject/Inventory 기능/옷 파츠 지정 해제", false, 13)]
        private static void UnassignOutfitPartFromMenu()
        {
            TryUnassignOutfitPart(Selection.activeGameObject, out _);
        }

        [MenuItem("GameObject/Inventory 기능/옷 파츠 지정 해제", true)]
        private static bool ValidateUnassignOutfitPartFromMenu()
        {
            return Enabled && Selection.activeGameObject != null;
        }

        public static bool TryAssignMenuRoot(GameObject target, out string message)
        {
            if (target == null)
            {
                message = "대상을 먼저 선택하세요.";
                ShowError(message);
                return false;
            }

            ClosetMenuRoot existing = target.GetComponent<ClosetMenuRoot>();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                message = "이미 ClosetMenuRoot가 지정되어 있습니다. 기존 컴포넌트를 선택했습니다.";
                ShowInfo(message);
                return true;
            }

            Undo.RegisterCompleteObjectUndo(target, "Assign Closet Menu Root");
            ClosetMenuRoot created = Undo.AddComponent<ClosetMenuRoot>(target);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            message = "ClosetMenuRoot를 추가했습니다.";
            ShowInfo(message);
            return true;
        }

        public static bool TryAssignOutfitSet(GameObject target, out string message)
        {
            if (target == null)
            {
                message = "대상을 먼저 선택하세요.";
                ShowError(message);
                return false;
            }

            ClosetMenuRoot root = target.transform.parent != null ? target.transform.parent.GetComponentInParent<ClosetMenuRoot>(true) : null;
            if (root == null || !target.transform.IsChildOf(root.transform))
            {
                message = "옷 지정은 ClosetMenuRoot 하위 오브젝트에서만 가능합니다.";
                ShowError(message);
                return false;
            }

            ClosetOutfitSet set = target.GetComponent<ClosetOutfitSet>();
            if (set == null)
            {
                Undo.RegisterCompleteObjectUndo(target, "Assign Closet Outfit Set");
                set = Undo.AddComponent<ClosetOutfitSet>(target);
            }

            Undo.RecordObject(set, "Auto Assign Outfit Set Index");
            set.SetIndex = FindNextSetIndex(root, set);
            EditorUtility.SetDirty(set);
            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);

            message = $"ClosetOutfitSet를 지정했습니다. setIndex={set.SetIndex}";
            ShowInfo(message);
            return true;
        }

        public static bool TryAssignOutfitPart(GameObject target, out string message)
        {
            if (target == null)
            {
                message = "대상을 먼저 선택하세요.";
                ShowError(message);
                return false;
            }

            ClosetOutfitSet set = target.transform.parent != null ? target.transform.parent.GetComponentInParent<ClosetOutfitSet>(true) : null;
            if (set == null || !target.transform.IsChildOf(set.transform))
            {
                message = "옷 파츠 지정은 ClosetOutfitSet 하위 오브젝트에서만 가능합니다.";
                ShowError(message);
                return false;
            }

            ClosetOutfitPart existing = target.GetComponent<ClosetOutfitPart>();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                message = "이미 ClosetOutfitPart가 지정되어 있습니다.";
                ShowInfo(message);
                return true;
            }

            Undo.RegisterCompleteObjectUndo(target, "Assign Closet Outfit Part");
            ClosetOutfitPart created = Undo.AddComponent<ClosetOutfitPart>(target);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            message = "ClosetOutfitPart를 추가했습니다.";
            ShowInfo(message);
            return true;
        }

        public static bool TryUnassignOutfitPart(GameObject target, out string message)
        {
            if (target == null)
            {
                message = "대상을 먼저 선택하세요.";
                ShowError(message);
                return false;
            }

            ClosetOutfitPart existing = target.GetComponent<ClosetOutfitPart>();
            if (existing == null)
            {
                message = "해제할 ClosetOutfitPart가 없습니다.";
                ShowInfo(message);
                return false;
            }

            Undo.DestroyObjectImmediate(existing);
            message = "ClosetOutfitPart 지정을 해제했습니다.";
            ShowInfo(message);
            return true;
        }

        public static int FindNextSetIndex(ClosetMenuRoot root, ClosetOutfitSet self)
        {
            HashSet<int> used = new HashSet<int>();
            ClosetOutfitSet[] sets = root.GetComponentsInChildren<ClosetOutfitSet>(true);
            for (int i = 0; i < sets.Length; i++)
            {
                ClosetOutfitSet set = sets[i];
                if (set == null || set == self)
                {
                    continue;
                }

                used.Add(set.SetIndex);
            }

            int index = 0;
            while (used.Contains(index))
            {
                index++;
            }

            return index;
        }

        private static void ShowError(string message)
        {
            Debug.LogError($"[ClosetHierarchyContextMenu] {message}");
            EditorUtility.DisplayDialog("Inventory 기능", message, "OK");
        }

        private static void ShowInfo(string message)
        {
            Debug.Log($"[ClosetHierarchyContextMenu] {message}");
        }
    }
}
