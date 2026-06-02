using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ryx.Core.Tutorials.Editor
{
    [CustomEditor(typeof(TutorialManager))]
    public class TutorialManagerEditor : UnityEditor.Editor
    {
        // Serialized properties
        SerializedProperty _tutorialPanel;
        SerializedProperty _taskText;
        SerializedProperty _hintPanel;
        SerializedProperty _hintText;
        SerializedProperty _pathLine;
        SerializedProperty _pathHeight;
        SerializedProperty _platformTutorials;
        SerializedProperty _forceMobileTutorials;
        SerializedProperty _forceDesktopTutorials;
        SerializedProperty _checkboxItemPrefab;
        SerializedProperty _checkboxContainer;
        SerializedProperty _autoStartTutorial;
        SerializedProperty _persistProgress;

        // Reflection cache
        FieldInfo _activeTutorialChaptersField;
        FieldInfo _currentChapterIndexField;

        // Foldout states
        static bool _configFoldout = true;
        static bool _runtimeStateFoldout = true;
        static bool _chapterOverviewFoldout = true;
        static bool _currentStepFoldout = true;
        static bool _debugActionsFoldout = true;

        // Jump-to controls
        int _jumpChapterIndex;
        int _jumpStepIndex;

        // Styles
        static GUIStyle _headerStyle;
        static GUIStyle _boldLabelStyle;
        static GUIStyle _completedStyle;
        static GUIStyle _currentStyle;
        static GUIStyle _pendingStyle;

        void OnEnable()
        {
            _tutorialPanel = serializedObject.FindProperty("tutorialPanel");
            _taskText = serializedObject.FindProperty("taskText");
            _hintPanel = serializedObject.FindProperty("hintPanel");
            _hintText = serializedObject.FindProperty("hintText");
            _pathLine = serializedObject.FindProperty("pathLine");
            _pathHeight = serializedObject.FindProperty("pathHeight");
            _platformTutorials = serializedObject.FindProperty("platformTutorials");
            _forceMobileTutorials = serializedObject.FindProperty("forceMobileTutorials");
            _forceDesktopTutorials = serializedObject.FindProperty("forceDesktopTutorials");
            _checkboxItemPrefab = serializedObject.FindProperty("checkboxItemPrefab");
            _checkboxContainer = serializedObject.FindProperty("checkboxContainer");
            _autoStartTutorial = serializedObject.FindProperty("autoStartTutorial");
            _persistProgress = serializedObject.FindProperty("persistProgress");

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _activeTutorialChaptersField = typeof(TutorialManager).GetField("activeTutorialChapters", flags);
            _currentChapterIndexField = typeof(TutorialManager).GetField("currentChapterIndex", flags);
        }

        static void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 8, 4)
            };

            _boldLabelStyle = new GUIStyle(EditorStyles.boldLabel);

            _completedStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
            };

            _currentStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.3f, 0.6f, 1f) }
            };

            _pendingStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.gray }
            };
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            DrawEditModeSection();

            if (Application.isPlaying)
            {
                var manager = (TutorialManager)target;
                var chapters = GetActiveChapters(manager);

                EditorGUILayout.Space(8);
                DrawSeparator("PLAY MODE DEBUG");
                EditorGUILayout.Space(4);

                DrawRuntimeStateSection(manager, chapters);
                DrawChapterOverviewSection(manager, chapters);
                DrawCurrentStepDetailsSection(manager);
                DrawDebugActionsSection(manager, chapters);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawEditModeSection()
        {
            _configFoldout = EditorGUILayout.Foldout(_configFoldout, "Tutorial Configuration", true, EditorStyles.foldoutHeader);
            if (!_configFoldout) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("UI References", _boldLabelStyle);
            EditorGUILayout.PropertyField(_tutorialPanel);
            EditorGUILayout.PropertyField(_taskText);
            EditorGUILayout.PropertyField(_hintPanel);
            EditorGUILayout.PropertyField(_hintText);
            EditorGUILayout.PropertyField(_pathLine);
            EditorGUILayout.PropertyField(_pathHeight);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Platform Tutorials", _boldLabelStyle);
            EditorGUILayout.PropertyField(_platformTutorials, true);
            EditorGUILayout.PropertyField(_forceMobileTutorials);
            EditorGUILayout.PropertyField(_forceDesktopTutorials);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Checkbox UI", _boldLabelStyle);
            EditorGUILayout.PropertyField(_checkboxItemPrefab);
            EditorGUILayout.PropertyField(_checkboxContainer);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Behavior", _boldLabelStyle);
            EditorGUILayout.PropertyField(_autoStartTutorial);
            EditorGUILayout.PropertyField(_persistProgress);

            DrawValidationWarnings();

            if (_tutorialPanel.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Tutorial Panel is not assigned.", MessageType.Warning);
            if (_taskText.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Task Text is not assigned.", MessageType.Warning);
            if (_pathLine.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Path Line Renderer is not assigned.", MessageType.Warning);

            EditorGUI.indentLevel--;
        }

        void DrawRuntimeStateSection(TutorialManager manager, List<TutorialChapter> chapters)
        {
            _runtimeStateFoldout = EditorGUILayout.Foldout(_runtimeStateFoldout, "Runtime State", true, EditorStyles.foldoutHeader);
            if (!_runtimeStateFoldout) return;

            EditorGUI.indentLevel++;

            // Platform
            string platform = manager.IsMobileTutorial ? "Mobile" : "Desktop";
            if (manager.forceMobileTutorials) platform += " (forced)";
            else if (manager.forceDesktopTutorials) platform += " (forced)";
            EditorGUILayout.HelpBox($"Platform: {platform}", MessageType.Info);

            int totalChapters = chapters?.Count ?? 0;

            EditorGUI.BeginDisabledGroup(true);

            // Chapter info
            var chapter = manager.CurrentChapter;
            if (chapter != null)
            {
                string chapterId = string.IsNullOrEmpty(chapter.chapterId) ? "(no id)" : chapter.chapterId;
                EditorGUILayout.TextField("Current Chapter",
                    $"{manager.CurrentChapterIndex + 1} / {totalChapters} — {chapterId}");
            }
            else
            {
                EditorGUILayout.TextField("Current Chapter", "No active tutorial");
            }

            // Step info
            var step = manager.CurrentStep;
            if (step != null && chapter != null)
            {
                string stepId = string.IsNullOrEmpty(step.stepId) ? "(no id)" : step.stepId;
                EditorGUILayout.TextField("Current Step",
                    $"{manager.CurrentStepIndex + 1} / {chapter.steps.Count} — {stepId}");
                EditorGUILayout.Toggle("Step Completed", step.isCompleted);
            }
            else
            {
                EditorGUILayout.TextField("Current Step", "—");
            }

            if (chapter != null)
                EditorGUILayout.Toggle("Chapter Completed", chapter.isCompleted);

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        void DrawChapterOverviewSection(TutorialManager manager, List<TutorialChapter> chapters)
        {
            _chapterOverviewFoldout = EditorGUILayout.Foldout(_chapterOverviewFoldout, "Chapter Overview", true, EditorStyles.foldoutHeader);
            if (!_chapterOverviewFoldout) return;

            if (chapters == null || chapters.Count == 0)
            {
                EditorGUILayout.HelpBox("No active tutorial chapters.", MessageType.Info);
                return;
            }

            int currentChapterIdx = manager.CurrentChapterIndex;

            for (int ci = 0; ci < chapters.Count; ci++)
            {
                var ch = chapters[ci];
                if (ch == null) continue;

                string chId = string.IsNullOrEmpty(ch.chapterId) ? $"Chapter {ci}" : ch.chapterId;
                string status;
                if (ch.isCompleted)
                    status = "  [COMPLETED]";
                else if (ci == currentChapterIdx)
                    status = "  [CURRENT]";
                else
                    status = "";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"{chId}{status}", _boldLabelStyle);

                if (ch.steps != null)
                {
                    for (int si = 0; si < ch.steps.Count; si++)
                    {
                        var s = ch.steps[si];
                        if (s == null) continue;

                        bool isCurrent = ci == currentChapterIdx && si == ch.currentStepIndex;
                        string sId = string.IsNullOrEmpty(s.stepId) ? $"Step {si}" : s.stepId;

                        var rect = EditorGUILayout.BeginHorizontal();

                        if (isCurrent)
                        {
                            EditorGUI.DrawRect(rect, new Color(0.2f, 0.5f, 0.9f, 0.15f));
                            GUILayout.Label($"  \u25B6 {sId}", _currentStyle);
                        }
                        else if (s.isCompleted)
                        {
                            GUILayout.Label($"  \u2713 {sId}", _completedStyle);
                        }
                        else
                        {
                            GUILayout.Label($"  \u25CB {sId}", _pendingStyle);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        void DrawCurrentStepDetailsSection(TutorialManager manager)
        {
            var step = manager.CurrentStep;
            if (step == null) return;

            _currentStepFoldout = EditorGUILayout.Foldout(_currentStepFoldout, "Current Step Details", true, EditorStyles.foldoutHeader);
            if (!_currentStepFoldout) return;

            EditorGUI.indentLevel++;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Step ID", step.stepId ?? "");
            EditorGUILayout.TextField("Description", step.description?.ToString() ?? "—");
            EditorGUILayout.TextField("Hint Text", step.hintText?.ToString() ?? "—");
            EditorGUILayout.ColorField("Outline Color", step.outlineColor);
            EditorGUILayout.FloatField("Outline Width", step.outlineWidth);
            EditorGUILayout.Toggle("Completed", step.isCompleted);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Object References", _boldLabelStyle);

            // Clickable object fields — enabled so clicking pings in Hierarchy
            DrawObjectField("Completion Trigger", step.completionTrigger, typeof(TutorialTrigger));
            DrawObjectField("Highlight UI Element", step.highlightUIElement, typeof(RectTransform));
            DrawObjectField("Target Destination", step.targetDestination, typeof(Transform));

            if (step.highlightGameObjects != null && step.highlightGameObjects.Length > 0)
            {
                EditorGUILayout.LabelField("Highlight GameObjects:");
                EditorGUI.indentLevel++;
                for (int i = 0; i < step.highlightGameObjects.Length; i++)
                    DrawObjectField($"[{i}]", step.highlightGameObjects[i], typeof(GameObject));
                EditorGUI.indentLevel--;
            }

            if (step.stepObjectsToEnable != null && step.stepObjectsToEnable.Length > 0)
            {
                EditorGUILayout.LabelField("Objects To Enable:");
                EditorGUI.indentLevel++;
                for (int i = 0; i < step.stepObjectsToEnable.Length; i++)
                    DrawObjectField($"[{i}]", step.stepObjectsToEnable[i], typeof(GameObject));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        void DrawDebugActionsSection(TutorialManager manager, List<TutorialChapter> chapters)
        {
            _debugActionsFoldout = EditorGUILayout.Foldout(_debugActionsFoldout, "Debug Actions", true, EditorStyles.foldoutHeader);
            if (!_debugActionsFoldout) return;

            EditorGUI.indentLevel++;

            // Row 1
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Complete Current Step"))
                manager.CompleteCurrentStep();
            if (GUILayout.Button("Next Chapter"))
                manager.NextChapter();
            EditorGUILayout.EndHorizontal();

            // Row 2
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Skip Tutorial"))
                manager.SkipTutorial();
            if (GUILayout.Button("Reset Progress"))
                manager.ResetTutorialProgress();
            EditorGUILayout.EndHorizontal();

            // Row 3
            if (GUILayout.Button("Restart Tutorial"))
                manager.StartTutorial();

            // Jump-To
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Jump To", _boldLabelStyle);

            int maxChapter = chapters != null && chapters.Count > 0 ? chapters.Count - 1 : 0;
            _jumpChapterIndex = Mathf.Clamp(_jumpChapterIndex, 0, maxChapter);
            _jumpChapterIndex = EditorGUILayout.IntSlider("Chapter", _jumpChapterIndex, 0, maxChapter);

            int maxStep = 0;
            if (chapters != null && _jumpChapterIndex < chapters.Count)
            {
                var ch = chapters[_jumpChapterIndex];
                if (ch?.steps != null && ch.steps.Count > 0)
                    maxStep = ch.steps.Count - 1;
            }
            _jumpStepIndex = Mathf.Clamp(_jumpStepIndex, 0, maxStep);
            _jumpStepIndex = EditorGUILayout.IntSlider("Step", _jumpStepIndex, 0, maxStep);

            if (GUILayout.Button("Jump"))
                JumpTo(manager, chapters, _jumpChapterIndex, _jumpStepIndex);

            EditorGUI.indentLevel--;
        }

        // --- Helpers ---

        List<TutorialChapter> GetActiveChapters(TutorialManager manager)
        {
            if (_activeTutorialChaptersField == null) return null;
            return _activeTutorialChaptersField.GetValue(manager) as List<TutorialChapter>;
        }

        void JumpTo(TutorialManager manager, List<TutorialChapter> chapters, int chapterIdx, int stepIdx)
        {
            if (chapters == null || chapterIdx < 0 || chapterIdx >= chapters.Count) return;

            var targetChapter = chapters[chapterIdx];
            if (targetChapter?.steps == null || stepIdx < 0 || stepIdx >= targetChapter.steps.Count) return;

            // Mark all previous chapters as completed
            for (int i = 0; i < chapterIdx; i++)
            {
                var ch = chapters[i];
                if (ch == null) continue;
                ch.isCompleted = true;
                ch.currentStepIndex = ch.steps.Count - 1;
                foreach (var s in ch.steps)
                    s.isCompleted = true;
            }

            // Reset current and future chapters
            for (int i = chapterIdx; i < chapters.Count; i++)
            {
                var ch = chapters[i];
                if (ch == null) continue;
                ch.isCompleted = false;
                ch.currentStepIndex = -1;
                foreach (var s in ch.steps)
                    s.isCompleted = false;
            }

            // Mark steps before target as completed
            for (int i = 0; i < stepIdx; i++)
                targetChapter.steps[i].isCompleted = true;

            // Set state so NextStep() will advance to the target
            targetChapter.currentStepIndex = stepIdx - 1;

            if (_currentChapterIndexField != null)
                _currentChapterIndexField.SetValue(manager, chapterIdx);

            manager.NextStep();
        }

        static void DrawObjectField(string label, Object obj, System.Type type)
        {
            EditorGUILayout.ObjectField(label, obj, type, true);
        }

        static void DrawSeparator(string title)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(title, _headerStyle, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        void DrawValidationWarnings()
        {
            var manager = (TutorialManager)target;
            var warnings = manager.GetConfigurationWarnings();
            foreach (var warning in warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
    }
}
