using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YourName.AvatarClosetTool.Runtime;

namespace YourName.AvatarClosetTool.Editor
{
    public sealed class AvatarClosetWindow : EditorWindow
    {
        [Serializable]
        private sealed class OutfitEntry
        {
            public string DisplayName = string.Empty;
            public GameObject TargetGameObject;
            public string OptionalGroupName = string.Empty;
        }

        private GameObject _avatarRoot;
        [SerializeField] private List<OutfitEntry> _outfits = new List<OutfitEntry>();
        private readonly List<ClosetPipeline.PipelineMessage> _messages = new List<ClosetPipeline.PipelineMessage>();
        private string _summary = "Press Apply to run pipeline.";
        private string _status = "Idle";
        private Vector2 _scroll;

        [MenuItem("Tools/Avatar Closet/Open Window")]
        public static void OpenWindow()
        {
            AvatarClosetWindow window = GetWindow<AvatarClosetWindow>("Avatar Closet");
            window.minSize = new Vector2(520f, 360f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Avatar Closet", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            GameObject selectedRoot = (GameObject)EditorGUILayout.ObjectField(
                "Avatar Root",
                _avatarRoot,
                typeof(GameObject),
                true);
            if (selectedRoot != _avatarRoot)
            {
                _avatarRoot = selectedRoot;
                LoadOutfitsFromStore();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Outfits", EditorStyles.boldLabel);
            DrawOutfitList();

            EditorGUILayout.Space(12f);
            DrawActionSection();

            EditorGUILayout.Space(12f);
            DrawResultSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawOutfitList()
        {
            for (int i = 0; i < _outfits.Count; i++)
            {
                OutfitEntry entry = _outfits[i];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Outfit #{i + 1}", EditorStyles.boldLabel);
                    entry.DisplayName = EditorGUILayout.TextField("Display Name", entry.DisplayName);
                    entry.TargetGameObject = (GameObject)EditorGUILayout.ObjectField(
                        "Target GameObject",
                        entry.TargetGameObject,
                        typeof(GameObject),
                        true);
                    entry.OptionalGroupName = EditorGUILayout.TextField("Optional Group Name", entry.OptionalGroupName);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Delete", GUILayout.Width(80f)))
                        {
                            Undo.RecordObject(this, "Delete Outfit Entry");
                            _outfits.RemoveAt(i);
                            EditorUtility.SetDirty(this);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            if (GUILayout.Button("Add Outfit"))
            {
                Undo.RecordObject(this, "Add Outfit Entry");
                _outfits.Add(new OutfitEntry());
                EditorUtility.SetDirty(this);
            }
        }

        private void DrawActionSection()
        {
            EditorGUILayout.LabelField($"Pipeline Status: {_status}", EditorStyles.boldLabel);
            if (GUILayout.Button("Apply"))
            {
                RunPipeline();
            }
        }

        private void RunPipeline()
        {
            ClosetPipeline pipeline = new ClosetPipeline();
            ClosetPipeline.PipelineRequest request = new ClosetPipeline.PipelineRequest
            {
                AvatarRoot = _avatarRoot,
                UserOutfits = _outfits
                    .Select(entry => new ClosetPipeline.OutfitInput
                    {
                        DisplayName = entry.DisplayName,
                        TargetGameObject = entry.TargetGameObject,
                        OptionalGroupName = entry.OptionalGroupName
                    })
                    .ToList()
            };

            ClosetPipeline.PipelineResult result = pipeline.RunPipeline(request, status =>
            {
                _status = status;
                Repaint();
            });

            _messages.Clear();
            _messages.AddRange(result.Messages);
            _summary = result.Summary;
            _status = result.FinalStatus;

            if (!result.HasError)
            {
                LoadOutfitsFromStore();
            }

            Debug.Log($"[AvatarClosetWindow] Pipeline finished. Status={result.FinalStatus}, Applied={result.Applied}, HasError={result.HasError}");
        }

        private void DrawResultSection()
        {
            EditorGUILayout.LabelField("Pipeline Results", EditorStyles.boldLabel);
            MessageType summaryType = _messages.Any(m => m.Severity == ClosetPipeline.MessageSeverity.Error)
                ? MessageType.Error
                : (_messages.Any(m => m.Severity == ClosetPipeline.MessageSeverity.Warning) ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox(_summary, summaryType);

            foreach (ClosetPipeline.PipelineMessage message in _messages)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"[{message.Severity}]", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(message.Text, EditorStyles.wordWrappedLabel);
                }
            }
        }

        private void LoadOutfitsFromStore()
        {
            List<OutfitEntry> loaded = new List<OutfitEntry>();
            if (_avatarRoot != null)
            {
                AvatarClosetRegistrationStore store = _avatarRoot.GetComponentInChildren<AvatarClosetRegistrationStore>(true);
                if (store != null)
                {
                    for (int i = 0; i < store.Outfits.Count; i++)
                    {
                        AvatarClosetRegistrationStore.OutfitRecord record = store.Outfits[i];
                        if (record == null)
                        {
                            continue;
                        }

                        loaded.Add(new OutfitEntry
                        {
                            DisplayName = record.DisplayName ?? string.Empty,
                            TargetGameObject = record.TargetGameObject,
                            OptionalGroupName = record.OptionalGroupName ?? string.Empty
                        });
                    }
                }
            }

            Undo.RecordObject(this, "Load Outfit Entries");
            _outfits = loaded;
            EditorUtility.SetDirty(this);
        }
    }
}
