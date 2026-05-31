using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ryx.Core.Hands.Editor
{
    /// <summary>
    /// Main editor window for previewing and editing grab animations
    /// </summary>
    public class GrabAnimationEditorWindow : EditorWindow
    {
        // UI References
        private VisualElement _root;
        private VisualElement _previewContainer;
        private Label _statusLabel;
        private Slider _timelineSlider;
        private Label _timeLabel;
        private Button _playButton;
        private FloatField _speedField;
        private Slider _speedSlider;
        private Toggle _loopToggle;
        private ObjectField _itemField;
        private ObjectField _animationField;
        private EnumField _handSideField;

        // Preview controller
        private GrabAnimationPreviewController _previewController;

        // Current selection
        private GrabItem _currentItem;
        private AnimationClip _currentClip;

        // Preview settings
        private bool _autoSelectFromScene = true;
        private bool _showPathInPreview = true;
        private bool _showHandGizmo = true;

        // Window state
        private enum WindowMode { Preview, Edit, Path, Bake }
        private WindowMode _currentMode = WindowMode.Preview;

        [MenuItem("Window/Grab Animation Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<GrabAnimationEditorWindow>("Grab Animation Editor");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _previewController = new GrabAnimationPreviewController();

            // Create UI first before handling selection
            CreateUI();

            // Subscribe to selection changes
            Selection.selectionChanged += OnSelectionChanged;

            // Auto-select from scene after UI is created
            if (_autoSelectFromScene)
            {
                OnSelectionChanged();
            }
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;

            _previewController?.Dispose();
            _previewController = null;
        }

        private void CreateUI()
        {
            _root = rootVisualElement;
            _root.Clear();

            // Main container
            var mainContainer = new VisualElement();
            mainContainer.style.flexGrow = 1;
            mainContainer.style.flexDirection = FlexDirection.Column;
            _root.Add(mainContainer);

            // Toolbar
            CreateToolbar(mainContainer);

            // Content area with two columns
            var contentArea = new VisualElement();
            contentArea.style.flexGrow = 1;
            contentArea.style.flexDirection = FlexDirection.Row;
            mainContainer.Add(contentArea);

            // Left panel - Preview
            CreatePreviewPanel(contentArea);

            // Right panel - Properties
            CreatePropertiesPanel(contentArea);

            // Bottom panel - Timeline
            CreateTimelinePanel(mainContainer);

            UpdateUI();
        }

        private void CreateToolbar(VisualElement parent)
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 30;

            // Mode buttons
            var previewButton = new ToolbarButton(() => SetMode(WindowMode.Preview)) { text = "Preview" };
            var editButton = new ToolbarButton(() => SetMode(WindowMode.Edit)) { text = "Edit" };
            var pathButton = new ToolbarButton(() => SetMode(WindowMode.Path)) { text = "Path" };
            var bakeButton = new ToolbarButton(() => SetMode(WindowMode.Bake)) { text = "Bake" };

            toolbar.Add(previewButton);
            toolbar.Add(editButton);
            toolbar.Add(pathButton);
            toolbar.Add(bakeButton);

            // Spacer
            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            // Settings button
            var settingsButton = new ToolbarButton(ShowSettings) { text = "Settings" };
            toolbar.Add(settingsButton);

            parent.Add(toolbar);
        }

        private void CreatePreviewPanel(VisualElement parent)
        {
            var previewPanel = new VisualElement();
            previewPanel.style.flexGrow = 1;
            previewPanel.style.borderRightWidth = 1;
            previewPanel.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            previewPanel.style.paddingLeft = 5;
            previewPanel.style.paddingRight = 5;
            previewPanel.style.paddingTop = 5;

            // Preview header
            var header = new Label("Preview");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginBottom = 10;
            previewPanel.Add(header);

            // Preview container (for scene view rendering)
            _previewContainer = new IMGUIContainer(OnPreviewGUI);
            _previewContainer.style.flexGrow = 1;
            _previewContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _previewContainer.style.borderLeftWidth = 1;
            _previewContainer.style.borderRightWidth = 1;
            _previewContainer.style.borderTopWidth = 1;
            _previewContainer.style.borderBottomWidth = 1;
            _previewContainer.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            _previewContainer.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            _previewContainer.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
            _previewContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            previewPanel.Add(_previewContainer);

            // Status label
            _statusLabel = new Label("No item selected");
            _statusLabel.style.marginTop = 5;
            _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            previewPanel.Add(_statusLabel);

            parent.Add(previewPanel);
        }

        private void CreatePropertiesPanel(VisualElement parent)
        {
            var propertiesPanel = new ScrollView();
            propertiesPanel.style.width = 300;
            propertiesPanel.style.paddingLeft = 10;
            propertiesPanel.style.paddingRight = 10;
            propertiesPanel.style.paddingTop = 5;

            // Properties header
            var header = new Label("Properties");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginBottom = 10;
            propertiesPanel.Add(header);

            // Item selection
            _itemField = new ObjectField("Grab Item");
            _itemField.objectType = typeof(GrabItem);
            _itemField.RegisterValueChangedCallback(evt =>
            {
                SelectItem(evt.newValue as GrabItem);
            });
            propertiesPanel.Add(_itemField);

            // Animation selection
            _animationField = new ObjectField("Animation Clip");
            _animationField.objectType = typeof(AnimationClip);
            _animationField.RegisterValueChangedCallback(evt =>
            {
                SelectAnimationClip(evt.newValue as AnimationClip);
            });
            propertiesPanel.Add(_animationField);

            // Hand side selection
            _handSideField = new EnumField("Hand Side", GrabAnimationPreviewController.HandSide.Right);
            _handSideField.RegisterValueChangedCallback(evt =>
            {
                if (_previewController != null)
                {
                    _previewController.CurrentHandSide = (GrabAnimationPreviewController.HandSide)evt.newValue;
                }
            });
            propertiesPanel.Add(_handSideField);

            // Separator
            propertiesPanel.Add(CreateSeparator());

            // Preview settings
            var settingsLabel = new Label("Preview Settings");
            settingsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            settingsLabel.style.marginTop = 10;
            settingsLabel.style.marginBottom = 5;
            propertiesPanel.Add(settingsLabel);

            var autoSelectToggle = new Toggle("Auto-select from Scene");
            autoSelectToggle.value = _autoSelectFromScene;
            autoSelectToggle.RegisterValueChangedCallback(evt => _autoSelectFromScene = evt.newValue);
            propertiesPanel.Add(autoSelectToggle);

            var showPathToggle = new Toggle("Show Path Preview");
            showPathToggle.value = _showPathInPreview;
            showPathToggle.RegisterValueChangedCallback(evt => _showPathInPreview = evt.newValue);
            propertiesPanel.Add(showPathToggle);

            var showHandToggle = new Toggle("Show Hand Gizmo");
            showHandToggle.value = _showHandGizmo;
            showHandToggle.RegisterValueChangedCallback(evt => _showHandGizmo = evt.newValue);
            propertiesPanel.Add(showHandToggle);

            // Separator
            propertiesPanel.Add(CreateSeparator());

            // Item info
            var infoLabel = new Label("Item Information");
            infoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoLabel.style.marginTop = 10;
            infoLabel.style.marginBottom = 5;
            propertiesPanel.Add(infoLabel);

            var itemInfoContainer = new VisualElement();
            itemInfoContainer.name = "ItemInfo";
            propertiesPanel.Add(itemInfoContainer);

            parent.Add(propertiesPanel);
        }

        private void CreateTimelinePanel(VisualElement parent)
        {
            var timelinePanel = new VisualElement();
            timelinePanel.style.height = 100;
            timelinePanel.style.borderTopWidth = 1;
            timelinePanel.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            timelinePanel.style.paddingLeft = 10;
            timelinePanel.style.paddingRight = 10;
            timelinePanel.style.paddingTop = 10;
            timelinePanel.style.paddingBottom = 10;

            // Playback controls
            var controlsContainer = new VisualElement();
            controlsContainer.style.flexDirection = FlexDirection.Row;
            controlsContainer.style.alignItems = Align.Center;
            controlsContainer.style.marginBottom = 10;

            // Play/Pause button
            _playButton = new Button(TogglePlayback);
            _playButton.text = "▶";
            _playButton.tooltip = "Play/Pause";
            _playButton.style.width = 35;
            _playButton.style.height = 30;
            _playButton.style.fontSize = 16;
            controlsContainer.Add(_playButton);

            // Step buttons
            var stepBackButton = new Button(StepBackward);
            stepBackButton.text = "⏮";
            stepBackButton.tooltip = "Step Backward";
            stepBackButton.style.width = 30;
            stepBackButton.style.height = 30;
            stepBackButton.style.marginLeft = 5;
            controlsContainer.Add(stepBackButton);

            var stepForwardButton = new Button(StepForward);
            stepForwardButton.text = "⏭";
            stepForwardButton.tooltip = "Step Forward";
            stepForwardButton.style.width = 30;
            stepForwardButton.style.height = 30;
            stepForwardButton.style.marginLeft = 5;
            controlsContainer.Add(stepForwardButton);

            // Reset button
            var resetButton = new Button(ResetAnimation);
            resetButton.text = "⏹";
            resetButton.tooltip = "Reset";
            resetButton.style.width = 30;
            resetButton.style.height = 30;
            resetButton.style.marginLeft = 5;
            controlsContainer.Add(resetButton);

            // Loop toggle
            _loopToggle = new Toggle("Loop");
            _loopToggle.value = true;
            _loopToggle.style.marginLeft = 15;
            _loopToggle.RegisterValueChangedCallback(evt =>
            {
                if (_previewController != null)
                    _previewController.LoopAnimation = evt.newValue;
            });
            controlsContainer.Add(_loopToggle);

            // Speed control - Label
            var speedLabel = new Label("Speed:");
            speedLabel.style.marginLeft = 20;
            speedLabel.style.marginRight = 5;
            controlsContainer.Add(speedLabel);

            // Speed slider
            _speedSlider = new Slider(0.1f, 3.0f);
            _speedSlider.value = 1.0f;
            _speedSlider.style.width = 100;
            _speedSlider.RegisterValueChangedCallback(evt =>
            {
                if (_speedField != null)
                    _speedField.SetValueWithoutNotify(evt.newValue);
                if (_previewController != null)
                    _previewController.PlaybackSpeed = evt.newValue;
            });
            controlsContainer.Add(_speedSlider);

            // Speed field
            _speedField = new FloatField();
            _speedField.value = 1.0f;
            _speedField.style.width = 45;
            _speedField.style.marginLeft = 5;
            _speedField.RegisterValueChangedCallback(evt =>
            {
                float clampedValue = Mathf.Clamp(evt.newValue, 0.1f, 3.0f);
                if (_speedSlider != null)
                    _speedSlider.SetValueWithoutNotify(clampedValue);
                if (_previewController != null)
                    _previewController.PlaybackSpeed = clampedValue;
            });
            controlsContainer.Add(_speedField);

            // Time label
            _timeLabel = new Label("0:00 / 0:00");
            _timeLabel.style.marginLeft = 20;
            controlsContainer.Add(_timeLabel);

            timelinePanel.Add(controlsContainer);

            // Timeline slider
            _timelineSlider = new Slider(0, 1);
            _timelineSlider.style.flexGrow = 1;
            _timelineSlider.RegisterValueChangedCallback(evt =>
            {
                if (_previewController != null && _previewController.IsPreviewActive)
                {
                    _previewController.UpdatePreview(evt.newValue);
                    UpdateTimeDisplay();
                }
            });
            timelinePanel.Add(_timelineSlider);

            parent.Add(timelinePanel);
        }

        private VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            separator.style.marginTop = 10;
            separator.style.marginBottom = 10;
            return separator;
        }

        private void OnPreviewGUI()
        {
            var rect = _previewContainer.contentRect;

            if (_currentItem == null)
            {
                GUI.Label(new Rect(rect.center.x - 50, rect.center.y - 10, 100, 20),
                    "No item selected", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // This is where we would render a preview camera view
            // For now, show instructions
            EditorGUI.HelpBox(new Rect(10, 10, rect.width - 20, 40),
                "Preview rendering will show here. Use Scene view for live preview.", MessageType.Info);

            // Show current animation info
            if (_currentClip != null)
            {
                GUI.Label(new Rect(10, 60, rect.width - 20, 20),
                    $"Animation: {_currentClip.name}", EditorStyles.boldLabel);
                GUI.Label(new Rect(10, 80, rect.width - 20, 20),
                    $"Length: {_currentClip.length:F2}s", EditorStyles.label);
            }
        }

        private void OnSelectionChanged()
        {
            if (!_autoSelectFromScene)
                return;

            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                GrabItem grabItem = selected.GetComponent<GrabItem>();
                if (grabItem != null)
                {
                    SelectItem(grabItem);
                }
            }
        }

        private void SelectItem(GrabItem item)
        {
            _currentItem = item;

            // Null check for UI elements
            if (_itemField != null)
                _itemField.value = item;

            if (item != null)
            {
                // Try to find associated animation
                string animName = item.GetGrabAnimationName();
                string[] guids = AssetDatabase.FindAssets($"{animName} t:AnimationClip");

                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    SelectAnimationClip(clip);
                }
                else
                {
                    if (_statusLabel != null)
                        _statusLabel.text = $"No animation found for {item.ItemID} - showing hand position";
                    _currentClip = null;
                    if (_animationField != null)
                        _animationField.value = null;

                    // Still start preview to show hand position even without animation
                    if (_previewController != null)
                    {
                        _previewController.StopPreview();
                        var handSide = GrabAnimationPreviewController.HandSide.Right;
                        if (_handSideField != null && _handSideField.value != null)
                        {
                            handSide = (GrabAnimationPreviewController.HandSide)_handSideField.value;
                        }
                        _previewController.StartPreview(item, null, handSide);
                    }
                }

                UpdateItemInfo();
            }
            else
            {
                if (_statusLabel != null)
                    _statusLabel.text = "No item selected";
                _currentClip = null;
                if (_animationField != null)
                    _animationField.value = null;
            }

            UpdateUI();
        }

        private void SelectAnimationClip(AnimationClip clip)
        {
            _currentClip = clip;

            if (_animationField != null)
                _animationField.value = clip;

            if (_previewController != null && _currentItem != null && clip != null)
            {
                _previewController.StopPreview();
                var handSide = GrabAnimationPreviewController.HandSide.Right;
                if (_handSideField != null && _handSideField.value != null)
                {
                    handSide = (GrabAnimationPreviewController.HandSide)_handSideField.value;
                }
                _previewController.StartPreview(_currentItem, clip, handSide);
                if (_statusLabel != null)
                    _statusLabel.text = $"Previewing: {clip.name} ({handSide} Hand)";
            }

            UpdateUI();
        }

        private void UpdateItemInfo()
        {
            var infoContainer = _root.Q<VisualElement>("ItemInfo");
            if (infoContainer == null)
                return;

            infoContainer.Clear();

            if (_currentItem == null)
                return;

            // Display item properties
            var itemIdLabel = new Label($"ID: {_currentItem.ItemID}");
            infoContainer.Add(itemIdLabel);

            var itemTypeLabel = new Label($"Tag: {_currentItem.ItemTag}");
            infoContainer.Add(itemTypeLabel);

            var grabPointLabel = new Label($"Grab Point: {(_currentItem.Transform != null ? _currentItem.Transform.name : "None")}");
            infoContainer.Add(grabPointLabel);

            var returnLabel = new Label($"Return to Origin: {_currentItem.ReturnToOrigin}");
            infoContainer.Add(returnLabel);

            if (_currentItem.EnableCustomGrabMaxSpeed)
            {
                var speedLabel = new Label($"Custom Speed: {_currentItem.CustomGrabMaxSpeed}");
                infoContainer.Add(speedLabel);
            }
        }

        private void UpdateUI()
        {
            bool hasPreview = _previewController != null && _previewController.IsPreviewActive;

            if (_playButton != null)
                _playButton.SetEnabled(hasPreview);
            if (_timelineSlider != null)
                _timelineSlider.SetEnabled(hasPreview);
            if (_speedField != null)
                _speedField.SetEnabled(hasPreview);

            if (hasPreview)
            {
                if (_playButton != null)
                {
                    _playButton.text = _previewController.IsPlaying ? "⏸" : "▶";
                    _playButton.tooltip = _previewController.IsPlaying ? "Pause" : "Play";
                }
                UpdateTimeDisplay();
            }
            else
            {
                if (_timeLabel != null)
                    _timeLabel.text = "0:00 / 0:00";
                if (_timelineSlider != null)
                    _timelineSlider.value = 0;
            }
        }

        private void Update()
        {
            if (_previewController != null && _previewController.IsPreviewActive)
            {
                // Update timeline
                if (_previewController.AnimationLength > 0 && _timelineSlider != null)
                {
                    float normalizedTime = _previewController.CurrentTime / _previewController.AnimationLength;
                    _timelineSlider.SetValueWithoutNotify(normalizedTime);
                }

                UpdateTimeDisplay();

                // Update UI when playback stops automatically
                if (!_previewController.IsPlaying && _playButton != null)
                {
                    _playButton.text = "▶";
                    _playButton.tooltip = "Play";
                }

                // Force repaint for smooth playback
                if (_previewController.IsPlaying)
                {
                    Repaint();
                }
            }
        }

        private void UpdateTimeDisplay()
        {
            if (_previewController != null && _previewController.IsPreviewActive && _timeLabel != null)
            {
                float currentTime = _previewController.CurrentTime;
                float totalTime = _previewController.AnimationLength;

                _timeLabel.text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
            }
        }

        private string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60);
            int secs = Mathf.FloorToInt(seconds % 60);
            int ms = Mathf.FloorToInt((seconds % 1) * 100);
            return $"{minutes}:{secs:00}.{ms:00}";
        }

        private void TogglePlayback()
        {
            if (_previewController != null)
            {
                _previewController.TogglePlayback();
                UpdateUI();

                // Force immediate UI update when playback state changes
                if (_playButton != null)
                {
                    _playButton.text = _previewController.IsPlaying ? "⏸" : "▶";
                    _playButton.tooltip = _previewController.IsPlaying ? "Pause" : "Play";
                }
            }
        }

        private void StepForward()
        {
            _previewController?.StepForward();
            UpdateUI();
        }

        private void StepBackward()
        {
            _previewController?.StepBackward();
            UpdateUI();
        }

        private void ResetAnimation()
        {
            _previewController?.Reset();
            UpdateUI();
        }

        private void SetMode(WindowMode mode)
        {
            _currentMode = mode;
            // Mode-specific UI updates would go here
            Debug.Log($"Switched to {mode} mode");
        }

        private void ShowSettings()
        {
            // Open settings popup
            Debug.Log("Settings clicked");
        }

        /// <summary>
        /// Called from the GrabItem inspector to select an item
        /// </summary>
        public void SelectItemFromInspector(GrabItem item)
        {
            if (item != null)
            {
                _autoSelectFromScene = false; // Temporarily disable auto-select
                SelectItem(item);
                _autoSelectFromScene = true; // Re-enable auto-select
            }
        }

        /// <summary>
        /// Called from the GrabItem inspector to select an animation clip
        /// </summary>
        public void SelectAnimationClipFromInspector(AnimationClip clip)
        {
            if (clip != null)
            {
                SelectAnimationClip(clip);
            }
        }
    }
}
