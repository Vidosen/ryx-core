using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Ryx.Core.Hands.Editor
{
    /// <summary>
    /// Manages the preview state for grab animations in the Unity Editor.
    /// Handles creation of temporary preview objects and animation playback without entering Play Mode.
    /// </summary>
    public class GrabAnimationPreviewController : IDisposable
    {
        // Static initialization to clean up any leftover preview hands on domain reload
        [InitializeOnLoadMethod]
        private static void CleanupLeftoverPreviewHands()
        {
            // Find and destroy any preview hands that might have been left over
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.StartsWith("[PREVIEW]") && obj.name.Contains("Hand"))
                {
                    Debug.Log($"Cleaning up leftover preview hand: {obj.name}");
                    Object.DestroyImmediate(obj);
                }
            }
        }
        // Preview state
        private bool _isPreviewActive;
        private float _currentTime;
        private float _animationLength;
        private bool _isPlaying;
        private float _playbackSpeed = 1.0f;
        private double _lastUpdateTime;
        private bool _isLooping = true;

        // Preview objects
        private GameObject _previewHandObject;
        private GameObject _previewItemObject;
        private Animator _handAnimator;
        private AnimationClip _currentClip;
        private GrabItem _targetItem;
        private RuntimeAnimatorController _originalController;

        // Public properties for editor access
        public GrabItem PreviewedItem => _targetItem;
        public AnimationClip AnimationClip => _currentClip;

        // Original state for restoration
        private Vector3 _originalItemPosition;
        private Quaternion _originalItemRotation;
        private Transform _originalParent;
        private Vector3 _originalHandPosition;
        private Quaternion _originalHandRotation;

        // Hand prefab reference
        private GameObject _handPrefab;
        private RuntimeAnimatorController _handAnimatorController;

        // Hand selection
        public enum HandSide { Right, Left }
        private HandSide _currentHandSide = HandSide.Right;
        public HandSide CurrentHandSide
        {
            get => _currentHandSide;
            set
            {
                if (_currentHandSide != value)
                {
                    _currentHandSide = value;
                    if (_isPreviewActive)
                    {
                        // Restart preview with new hand
                        var item = _targetItem;
                        var clip = _currentClip;
                        StopPreview();
                        StartPreview(item, clip, value);
                    }
                }
            }
        }

        // Preview settings
        public bool IsPreviewActive => _isPreviewActive;
        public bool IsPlaying => _isPlaying;
        public float CurrentTime => _currentTime;
        public float AnimationLength => _animationLength;
        public bool LoopAnimation { get; set; } = true;
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Mathf.Clamp(value, 0.1f, 3.0f);
        }

        public GrabAnimationPreviewController()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>
        /// Starts preview mode for a grab animation
        /// </summary>
        public void StartPreview(GrabItem item, AnimationClip clip = null, HandSide handSide = HandSide.Right)
        {
            if (_isPreviewActive)
            {
                StopPreview();
            }

            if (item == null)
            {
                Debug.LogError("Cannot start preview: GrabItem is null");
                return;
            }

            _targetItem = item;
            _currentHandSide = handSide;

            // Store original state
            _originalItemPosition = item.transform.position;
            _originalItemRotation = item.transform.rotation;
            _originalParent = item.transform.parent;

            // Find or use provided animation clip
            if (clip == null)
            {
                clip = FindAnimationClip(item);
            }

            // Allow preview without animation - just show hand position
            if (clip != null)
            {
                _currentClip = clip;
                _animationLength = clip.length;
            }
            else
            {
                Debug.LogWarning($"No animation clip found for {item.ItemID} - showing hand position only");
                _currentClip = null;
                _animationLength = 0;
            }

            // Load animator controller
            if (_handAnimatorController == null)
            {
                string[] controllerGuids = AssetDatabase.FindAssets("AC_Hand t:AnimatorController");
                if (controllerGuids.Length > 0)
                {
                    string controllerPath = AssetDatabase.GUIDToAssetPath(controllerGuids[0]);
                    _handAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                }
            }

            // Create or find preview hand
            CreateOrFindPreviewHand(item, handSide);

            // Enter animation mode
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            _isPreviewActive = true;
            _currentTime = 0f;
            _lastUpdateTime = EditorApplication.timeSinceStartup;

            // Initial update
            UpdatePreview(0f);
        }

        /// <summary>
        /// Stops the preview and cleans up
        /// </summary>
        public void StopPreview()
        {
            if (!_isPreviewActive)
                return;

            _isPreviewActive = false;
            _isPlaying = false;

            // Restore original item state (if we moved it)
            if (_targetItem != null && _targetItem.gameObject != null)
            {
                // Note: Currently we don't move the item, but keeping this for future flexibility
                _targetItem.transform.position = _originalItemPosition;
                _targetItem.transform.rotation = _originalItemRotation;
                _targetItem.transform.SetParent(_originalParent);
            }

            // Always destroy the temporary preview hand
            if (_previewHandObject != null)
            {
                Debug.Log($"Destroying temporary preview hand: {_previewHandObject.name}");

                // Make sure to destroy the gameobject completely
                Object.DestroyImmediate(_previewHandObject);
                _previewHandObject = null;
            }

            // Clean up references
            _handAnimator = null;
            _originalController = null;
            _currentClip = null;
            _targetItem = null;
            _previewItemObject = null;

            // Exit animation mode
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Updates the preview to a specific normalized time (0-1)
        /// </summary>
        public void UpdatePreview(float normalizedTime)
        {
            if (!_isPreviewActive || _previewHandObject == null)
                return;

            // Only update time and sample animation if we have a clip
            if (_currentClip != null && _animationLength > 0)
            {
                _currentTime = Mathf.Clamp01(normalizedTime) * _animationLength;

                // Sample animation on the hand
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_previewHandObject, _currentClip, _currentTime);
                AnimationMode.EndSampling();
            }

            // Always position hand at grab point (with or without animation)
            if (_targetItem != null && _targetItem.Transform != null)
            {
                // Move hand to item's grab point
                _previewHandObject.transform.position = _targetItem.Transform.position;
                _previewHandObject.transform.rotation = _targetItem.Transform.rotation;
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Scrubs to a specific time in seconds
        /// </summary>
        public void ScrubToTime(float time)
        {
            if (!_isPreviewActive || _animationLength <= 0)
                return;

            _currentTime = Mathf.Clamp(time, 0f, _animationLength);
            float normalizedTime = _currentTime / _animationLength;
            UpdatePreview(normalizedTime);
        }

        /// <summary>
        /// Toggles playback
        /// </summary>
        public void TogglePlayback()
        {
            // If we're at the end of the animation and not looping, restart from beginning
            if (!_isPlaying && _animationLength > 0 && _currentTime >= _animationLength - 0.001f && !LoopAnimation)
            {
                // Restart from beginning
                _currentTime = 0f;
                UpdatePreview(0f);
                _isPlaying = true;
                _lastUpdateTime = EditorApplication.timeSinceStartup;
            }
            else
            {
                _isPlaying = !_isPlaying;
                if (_isPlaying)
                {
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                }
            }
        }

        /// <summary>
        /// Steps forward one frame
        /// </summary>
        public void StepForward()
        {
            if (!_isPreviewActive)
                return;

            float frameTime = 1f / 30f; // Assume 30fps
            ScrubToTime(_currentTime + frameTime);
        }

        /// <summary>
        /// Steps backward one frame
        /// </summary>
        public void StepBackward()
        {
            if (!_isPreviewActive)
                return;

            float frameTime = 1f / 30f; // Assume 30fps
            ScrubToTime(_currentTime - frameTime);
        }

        /// <summary>
        /// Resets to the beginning
        /// </summary>
        public void Reset()
        {
            ScrubToTime(0f);
        }

        /// <summary>
        /// Sets whether the animation should loop
        /// </summary>
        public void SetLooping(bool loop)
        {
            _isLooping = loop;
            LoopAnimation = loop;
        }

        /// <summary>
        /// Sets the playback speed
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            PlaybackSpeed = speed;
        }

        /// <summary>
        /// Forces an update of the hand position to match the current grab point
        /// Used when the grab point is moved interactively in the editor
        /// </summary>
        public void ForceUpdateHandPosition()
        {
            if (_previewHandObject == null || _targetItem == null || _targetItem.Transform == null)
                return;

            // Update the hand position to match the new grab point
            _previewHandObject.transform.position = _targetItem.Transform.position;
            _previewHandObject.transform.rotation = _targetItem.Transform.rotation;

            // If animating, resample at current time
            if (_currentClip != null && _isPlaying)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_previewHandObject, _currentClip, _currentTime);
                AnimationMode.EndSampling();
            }

            SceneView.RepaintAll();
        }

        private void OnEditorUpdate()
        {
            if (!_isPreviewActive || !_isPlaying || _animationLength <= 0)
                return;

            // Safety check: ensure preview hand still exists
            if (_previewHandObject == null)
            {
                Debug.LogWarning("Preview hand was destroyed unexpectedly - stopping preview");
                StopPreview();
                return;
            }

            // Calculate delta time
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime) * _playbackSpeed;
            _lastUpdateTime = currentTime;

            // Update animation time
            _currentTime += deltaTime;

            // Handle looping or stopping at end
            if (_currentTime >= _animationLength)
            {
                if (LoopAnimation)
                {
                    _currentTime = _currentTime % _animationLength;
                }
                else
                {
                    _currentTime = _animationLength;
                    _isPlaying = false; // Stop at end if not looping
                }
            }

            // Update preview
            float normalizedTime = _currentTime / _animationLength;
            UpdatePreview(normalizedTime);
        }

        private void CreateOrFindPreviewHand(GrabItem item, HandSide handSide)
        {
            // Always create a temporary preview hand - never use scene hands
            // This ensures we don't interfere with the actual scene setup

            // Try to find hand prefab based on what exists in the scene
            if (_handPrefab == null)
            {
                // First try to find the right hand prefab (we'll mirror it for left hand)
                string[] guids = AssetDatabase.FindAssets("RightHandInterctive t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }

                // If no prefab found, try to find an existing hand in scene to use as template
                if (_handPrefab == null)
                {
                    // Look for any hand in the scene to use as template
                    string[] handNames = new string[] { "RightHandInterctive", "Left Hand Interactive", "Hand" };
                    GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();

                    foreach (var handName in handNames)
                    {
                        foreach (var obj in sceneObjects)
                        {
                            if (obj.name.Contains(handName) && obj.GetComponent<PlayerHand>() != null)
                            {
                                // Found a hand in scene - use it as a template
                                _handPrefab = obj;
                                break;
                            }
                        }
                        if (_handPrefab != null) break;
                    }
                }
            }

            // Create preview hand
            if (_handPrefab != null)
            {
                // Instantiate from prefab or scene template
                _previewHandObject = Object.Instantiate(_handPrefab);

                // Disable PlayerHand script to prevent it from running update logic
                PlayerHand playerHand = _previewHandObject.GetComponent<PlayerHand>();
                if (playerHand != null)
                {
                    playerHand.enabled = false;
                }
            }
            else
            {
                // Create a simple placeholder hand if no prefab found
                _previewHandObject = CreatePlaceholderHand();
            }

            // Configure preview hand
            _previewHandObject.name = $"[PREVIEW] {handSide} Hand (Temporary - Not Saved)";

            // IMPORTANT: Set flags to prevent saving in scene
            _previewHandObject.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;

            // Also set flags on all children to ensure nothing gets saved
            Transform[] children = _previewHandObject.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
            }

            // MIRROR THE HAND FOR LEFT SIDE
            // Since we use the right hand prefab for both hands, mirror it for left
            if (handSide == HandSide.Left)
            {
                // Apply negative Y scale to mirror the hand horizontally
                Vector3 scale = _previewHandObject.transform.localScale;
                scale.y = -Mathf.Abs(scale.y); // Ensure negative Y scale
                _previewHandObject.transform.localScale = scale;

                Debug.Log($"Mirrored hand for left side with scale: {scale}");
            }
            else
            {
                // Ensure right hand has positive scale
                Vector3 scale = _previewHandObject.transform.localScale;
                scale.y = Mathf.Abs(scale.y); // Ensure positive Y scale
                _previewHandObject.transform.localScale = scale;
            }

            // Get animator
            _handAnimator = _previewHandObject.GetComponent<Animator>();
            if (_handAnimator == null)
            {
                _handAnimator = _previewHandObject.AddComponent<Animator>();
            }

            // Set animator controller
            if (_handAnimator != null && _handAnimatorController != null)
            {
                _handAnimator.runtimeAnimatorController = _handAnimatorController;
                _handAnimator.enabled = true; // Ensure animator is enabled for preview
            }

            // Position hand at grab point initially
            if (item.Transform != null)
            {
                _previewHandObject.transform.position = item.Transform.position;
                _previewHandObject.transform.rotation = item.Transform.rotation;
            }

            // Don't duplicate the item - just track it
            _previewItemObject = item.gameObject;

            Debug.Log($"Created temporary preview hand: {_previewHandObject.name}");
        }

        private GameObject CreatePlaceholderHand()
        {
            GameObject hand = new GameObject("Placeholder Hand");

            // Create a simple hand structure
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            palm.transform.SetParent(hand.transform);
            palm.transform.localScale = new Vector3(0.1f, 0.15f, 0.05f);

            // Create fingers
            for (int i = 0; i < 5; i++)
            {
                GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                finger.name = $"Finger_{i}";
                finger.transform.SetParent(palm.transform);
                finger.transform.localPosition = new Vector3(-0.04f + (i * 0.02f), 0.08f, 0);
                finger.transform.localScale = new Vector3(0.01f, 0.04f, 0.01f);
            }

            return hand;
        }

        private Transform FindHandGrabBone()
        {
            if (_previewHandObject == null)
                return null;

            // Try to find the hand bone in the rig hierarchy
            // Common bone names: hand.R, hand.L, palm, etc.
            Transform[] children = _previewHandObject.GetComponentsInChildren<Transform>();

            // Priority search for specific bones
            string[] boneNames = new string[] { "hand.R", "hand.L", "palm", "hand_r", "hand_l", "Hand" };

            foreach (string boneName in boneNames)
            {
                foreach (var child in children)
                {
                    if (child.name.Contains(boneName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return child;
                    }
                }
            }

            // If no specific bone found, return the root
            return _previewHandObject.transform;
        }

        private AnimationClip FindAnimationClip(GrabItem item)
        {
            string animationName = item.GetGrabAnimationName();
            string[] guids = AssetDatabase.FindAssets($"{animationName} t:AnimationClip");

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }

            // Try without prefix
            guids = AssetDatabase.FindAssets($"{item.ItemID} t:AnimationClip");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }

            return null;
        }

        public void Dispose()
        {
            EditorApplication.update -= OnEditorUpdate;

            // Ensure we clean up any preview hands
            if (_isPreviewActive)
            {
                StopPreview();
            }

            // Extra safety: destroy preview hand if it somehow still exists
            if (_previewHandObject != null)
            {
                Debug.LogWarning("Preview hand still existed during Dispose - cleaning up");
                Object.DestroyImmediate(_previewHandObject);
                _previewHandObject = null;
            }
        }
    }
}