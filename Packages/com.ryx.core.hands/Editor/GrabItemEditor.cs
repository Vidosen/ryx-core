using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Splines;

namespace Ryx.Core.Hands.Editor
{
    [CustomEditor(typeof(GrabItem), true)]
    [CanEditMultipleObjects]
    public class GrabItemEditor : UnityEditor.Editor
    {
        // Serialized properties
        private SerializedProperty _itemIDProp;
        private SerializedProperty _itemTypeProp;
        private SerializedProperty _grabPointProp;
        private SerializedProperty _enableCustomGrabMaxSpeedProp;
        private SerializedProperty _customGrabMaxSpeedProp;
        private SerializedProperty _returnToOriginProp;

        // Preview settings
        private static bool _showGrabPointGizmo = true;
        private static bool _showPathPreview = true;
        private static bool _showHandWireframe = true;
        private static Color _gizmoColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        private static Color _pathColor = new Color(1f, 0.8f, 0.2f, 0.6f);

        // Hand visualization
        private static readonly Vector3 HandSize = new Vector3(0.1f, 0.15f, 0.05f);
        private static readonly float FingerLength = 0.08f;

        // Foldout states
        private bool _showVisualizationSettings = false;
        private bool _showValidation = false;
        private bool _showAlignmentTools = false;
        private bool _showExportSettings = false;

        // Export options
        private FBXAnimationExporter.ExportOptions _exportOptions = new();

        // Real-time preview
        private static GrabAnimationPreviewController _livePreviewController;
        private static bool _enableLivePreview = false;
        private static GrabAnimationPreviewController.HandSide _livePreviewHandSide = GrabAnimationPreviewController.HandSide.Right;
        private Vector3 _lastGrabPointPosition;
        private Quaternion _lastGrabPointRotation;

        // Snapping settings
        private static bool _enableGridSnap = false;
        private static float _gridSnapSize = 0.1f;
        private static bool _enableRotationSnap = false;
        private static float _rotationSnapAngle = 15f;

        private void OnEnable()
        {
            _itemIDProp = serializedObject.FindProperty("_itemID");
            _itemTypeProp = serializedObject.FindProperty("_itemType");
            _grabPointProp = serializedObject.FindProperty("_grabPoint");
            _enableCustomGrabMaxSpeedProp = serializedObject.FindProperty("_enableCustomGrabMaxSpeed");
            _customGrabMaxSpeedProp = serializedObject.FindProperty("_customGrabMaxSpeed");
            _returnToOriginProp = serializedObject.FindProperty("_returnToOrigin");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Title
            EditorGUILayout.LabelField("Grab Item Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Core properties
            EditorGUILayout.PropertyField(_itemIDProp, new GUIContent("Item ID", "Unique identifier used for animation naming (Grab_[ID])"));
            EditorGUILayout.PropertyField(_itemTypeProp, new GUIContent("Item Type", "Category of this grabbable item"));
            EditorGUILayout.PropertyField(_grabPointProp, new GUIContent("Grab Point", "Transform where the hand attaches when grabbing"));
            EditorGUILayout.PropertyField(_returnToOriginProp, new GUIContent("Return to Origin", "Whether item returns to original position after drop"));

            EditorGUILayout.Space(5);

            // Custom speed settings
            EditorGUILayout.PropertyField(_enableCustomGrabMaxSpeedProp, new GUIContent("Custom Grab Speed"));
            if (_enableCustomGrabMaxSpeedProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_customGrabMaxSpeedProp, new GUIContent("Max Speed", "Custom maximum grab speed"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Visualization Settings
            _showVisualizationSettings = EditorGUILayout.Foldout(_showVisualizationSettings, "Visualization Settings", true);
            if (_showVisualizationSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                _showGrabPointGizmo = EditorGUILayout.Toggle("Show Grab Point", _showGrabPointGizmo);
                _showHandWireframe = EditorGUILayout.Toggle("Show Hand Preview", _showHandWireframe);
                _showPathPreview = EditorGUILayout.Toggle("Show Path Preview", _showPathPreview);

                EditorGUILayout.Space(5);

                // Live preview toggle
                bool prevLivePreview = _enableLivePreview;
                _enableLivePreview = EditorGUILayout.Toggle(new GUIContent("Live Animation Preview",
                    "Automatically preview animation when moving grab point"), _enableLivePreview);

                if (_enableLivePreview)
                {
                    EditorGUI.indentLevel++;
                    var prevHandSide = _livePreviewHandSide;
                    _livePreviewHandSide = (GrabAnimationPreviewController.HandSide)EditorGUILayout.EnumPopup(
                        new GUIContent("Preview Hand", "Which hand to show in live preview"),
                        _livePreviewHandSide);

                    // Restart preview if hand side changed
                    if (prevHandSide != _livePreviewHandSide && _livePreviewController != null)
                    {
                        StopLivePreview();
                        StartLivePreview();
                    }
                    EditorGUI.indentLevel--;
                }

                if (_enableLivePreview != prevLivePreview)
                {
                    if (_enableLivePreview)
                        StartLivePreview();
                    else
                        StopLivePreview();
                }

                if (_showGrabPointGizmo || _showHandWireframe)
                {
                    _gizmoColor = EditorGUILayout.ColorField("Gizmo Color", _gizmoColor);
                }

                if (_showPathPreview)
                {
                    _pathColor = EditorGUILayout.ColorField("Path Color", _pathColor);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Validation
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true);
            if (_showValidation)
            {
                EditorGUI.indentLevel++;
                ValidateSetup();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Alignment Tools
            _showAlignmentTools = EditorGUILayout.Foldout(_showAlignmentTools, "Alignment & Snapping Tools", true);
            if (_showAlignmentTools)
            {
                EditorGUI.indentLevel++;

                // Snapping settings
                EditorGUILayout.LabelField("Snapping", EditorStyles.boldLabel);
                _enableGridSnap = EditorGUILayout.Toggle("Enable Grid Snap", _enableGridSnap);
                if (_enableGridSnap)
                {
                    EditorGUI.indentLevel++;
                    _gridSnapSize = EditorGUILayout.FloatField("Grid Size", _gridSnapSize);
                    _gridSnapSize = Mathf.Max(0.01f, _gridSnapSize);
                    EditorGUI.indentLevel--;
                }

                _enableRotationSnap = EditorGUILayout.Toggle("Enable Rotation Snap", _enableRotationSnap);
                if (_enableRotationSnap)
                {
                    EditorGUI.indentLevel++;
                    _rotationSnapAngle = EditorGUILayout.FloatField("Angle (degrees)", _rotationSnapAngle);
                    _rotationSnapAngle = Mathf.Clamp(_rotationSnapAngle, 1f, 90f);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                // Alignment buttons
                EditorGUILayout.LabelField("Quick Alignment", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Center on Object"))
                {
                    AlignGrabPointToCenter();
                }
                if (GUILayout.Button("Snap to Surface"))
                {
                    SnapGrabPointToSurface();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Align to World"))
                {
                    AlignGrabPointToWorld();
                }
                if (GUILayout.Button("Align to Parent"))
                {
                    AlignGrabPointToParent();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Action buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview Animation", GUILayout.Height(30)))
            {
                PreviewAnimation();
            }

            if (GUILayout.Button("Align Grab Point", GUILayout.Height(30)))
            {
                AlignGrabPoint();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Export for Animation Section
            _showExportSettings = EditorGUILayout.Foldout(_showExportSettings, "Export for Animation", true);
            if (_showExportSettings)
            {
                EditorGUI.indentLevel++;

                if (!FBXAnimationExporter.IsFBXExporterAvailable())
                {
                    EditorGUILayout.HelpBox(
                        "FBX Exporter package (com.unity.formats.fbx) is required.\n" +
                        "Install via Package Manager to enable export.",
                        MessageType.Warning);

                    if (GUILayout.Button("Open Package Manager"))
                    {
                        UnityEditor.PackageManager.UI.Window.Open("com.unity.formats.fbx");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Hand Options", EditorStyles.boldLabel);
                    _exportOptions.IncludeRightHand = EditorGUILayout.Toggle(
                        new GUIContent("Include Right Hand", "Include right hand skeleton in export"),
                        _exportOptions.IncludeRightHand);
                    _exportOptions.IncludeLeftHand = EditorGUILayout.Toggle(
                        new GUIContent("Include Left Hand", "Include left hand skeleton (mirrored)"),
                        _exportOptions.IncludeLeftHand);

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Cleanup Options", EditorStyles.boldLabel);
                    _exportOptions.StripIKComponents = EditorGUILayout.Toggle(
                        new GUIContent("Strip IK Components", "Remove Unity IK constraints (not usable in external software)"),
                        _exportOptions.StripIKComponents);
                    _exportOptions.StripMonoBehaviours = EditorGUILayout.Toggle(
                        new GUIContent("Strip Scripts", "Remove MonoBehaviour scripts from export"),
                        _exportOptions.StripMonoBehaviours);

                    EditorGUILayout.Space(10);

                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button(new GUIContent("Export FBX for Animation",
                        "Export item + hand skeleton for animation in external software"),
                        GUILayout.Height(30)))
                    {
                        ExportForAnimation();
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Exports FBX with:\n" +
                        "• Item mesh hierarchy\n" +
                        "• Hand skeleton at GrabPoint\n\n" +
                        "Import into Blender/Maya to create grab animations.",
                        MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ValidateSetup()
        {
            GrabItem grabItem = (GrabItem)target;
            bool hasIssues = false;

            // Check Item ID
            if (string.IsNullOrEmpty(grabItem.ItemID))
            {
                EditorGUILayout.HelpBox("Item ID is required for animation naming", MessageType.Error);
                hasIssues = true;
            }

            // Check Grab Point
            if (grabItem.Transform == null)
            {
                EditorGUILayout.HelpBox("Grab Point Transform is not assigned", MessageType.Error);
                hasIssues = true;
            }
            else if (grabItem.Transform == grabItem.transform)
            {
                EditorGUILayout.HelpBox("Grab Point should typically be a child transform, not the root", MessageType.Warning);
                hasIssues = true;
            }

            // Check for animation
            if (!string.IsNullOrEmpty(grabItem.ItemID))
            {
                string animationName = $"Grab_{grabItem.ItemID}";
                string[] guids = AssetDatabase.FindAssets($"{animationName} t:AnimationClip");

                if (guids.Length == 0)
                {
                    EditorGUILayout.HelpBox($"Animation '{animationName}' not found in project", MessageType.Warning);
                    hasIssues = true;
                }
                else
                {
                    EditorGUILayout.HelpBox($"✓ Animation '{animationName}' found", MessageType.Info);
                }
            }

            // Check for SplinePathBuilder
            SplinePathBuilder splineBuilder = grabItem.GetComponent<SplinePathBuilder>();
            if (splineBuilder != null)
            {
                SplineContainer splineContainer = splineBuilder.GetComponent<SplineContainer>();
                if (splineContainer == null || splineContainer.Spline.Count == 0)
                {
                    EditorGUILayout.HelpBox("SplinePathBuilder has no spline data", MessageType.Warning);
                    hasIssues = true;
                }
                else
                {
                    EditorGUILayout.HelpBox($"✓ Spline path configured ({splineContainer.Spline.Count} knots)", MessageType.Info);
                }
            }

            if (!hasIssues)
            {
                EditorGUILayout.HelpBox("✓ All validations passed", MessageType.Info);
            }
        }

        private void PreviewAnimation()
        {
            GrabItem grabItem = (GrabItem)target;

            if (string.IsNullOrEmpty(grabItem.ItemID))
            {
                EditorUtility.DisplayDialog("Preview Error", "Item ID is required to preview animation", "OK");
                return;
            }

            // Try to find existing Grab Animation Editor window
            GrabAnimationEditorWindow window = EditorWindow.GetWindow<GrabAnimationEditorWindow>(false, "Grab Animation Editor", false);

            if (window == null)
            {
                // If no window exists, create a new one
                window = EditorWindow.CreateWindow<GrabAnimationEditorWindow>();
                window.titleContent = new GUIContent("Grab Animation Editor");
            }

            // Focus the window
            window.Show();
            window.Focus();

            // Select the current item in the editor window
            window.SelectItemFromInspector(grabItem);

            // Optional: Find and select the animation clip if it exists
            string animationName = $"Grab_{grabItem.ItemID}";
            string[] guids = AssetDatabase.FindAssets($"{animationName} t:AnimationClip");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    window.SelectAnimationClipFromInspector(clip);
                }
            }
        }

        private void AlignGrabPoint()
        {
            GrabItem grabItem = (GrabItem)target;

            if (grabItem.Transform == null)
            {
                EditorUtility.DisplayDialog("Align Error", "Grab Point Transform is not assigned", "OK");
                return;
            }

            Undo.RecordObject(grabItem.Transform, "Align Grab Point");

            // Align grab point to be centered on the object with proper orientation
            Bounds bounds = CalculateBounds(grabItem.gameObject);
            Vector3 centerPoint = bounds.center;

            // Position at center
            grabItem.Transform.position = centerPoint;

            // Orient to face upward (typical grab orientation)
            grabItem.Transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            EditorUtility.SetDirty(grabItem.Transform);
            SceneView.RepaintAll();
        }

        private Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 0.1f);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private void OnSceneGUI()
        {
            GrabItem grabItem = (GrabItem)target;

            if (grabItem.Transform == null)
                return;

            // Scene GUI drawing runs in Unity's immediate-mode pipeline and can "leak" outside the SceneView
            // when labels/UI aren't clipped, especially with docked windows (Profiler, Inspector, etc).
            // Keep drawing strictly to Repaint, and clip any GUI to the SceneView rect.
            EventType evtType = Event.current.type;

            // Preserve global Handles state (it is shared across editor GUI passes)
            Color prevHandlesColor = Handles.color;
            Matrix4x4 prevHandlesMatrix = Handles.matrix;
            try
            {
            // Draw grab point gizmo
                if (evtType == EventType.Repaint && _showGrabPointGizmo)
                    DrawGrabPointGizmo(grabItem);

            // Draw hand wireframe
                if (evtType == EventType.Repaint && _showHandWireframe)
                    DrawHandWireframe(grabItem);

            // Draw path preview
                if (evtType == EventType.Repaint && _showPathPreview)
                    DrawPathPreview(grabItem);

            // Interactive handle for grab point
                DrawGrabPointHandle(grabItem);
            }
            finally
            {
                Handles.color = prevHandlesColor;
                Handles.matrix = prevHandlesMatrix;
            }
        }

        private void DrawGrabPointGizmo(GrabItem grabItem)
        {
            Transform grabPoint = grabItem.Transform;

            Handles.color = _gizmoColor;

            // Draw sphere at grab point
            Handles.SphereHandleCap(0, grabPoint.position, grabPoint.rotation, 0.02f, EventType.Repaint);

            // Note: Orientation arrows removed to avoid duplication with PositionHandle in DrawGrabPointHandle()

            // Draw label (clipped to SceneView to avoid bleeding into adjacent docked windows)
            DrawWorldLabelClipped(
                grabPoint.position + Vector3.up * 0.15f,
                $"Grab Point: {grabItem.ItemID}",
                EditorStyles.whiteBoldLabel);
        }

        private static void DrawWorldLabelClipped(Vector3 worldPosition, string text, GUIStyle style)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            SceneView sv = SceneView.currentDrawingSceneView;
            if (sv == null)
                return;

            Handles.BeginGUI();
            try
            {
                // SceneView GUI coordinates (relative to the SceneView)
                Vector2 p = HandleUtility.WorldToGUIPoint(worldPosition);
                GUIContent content = new GUIContent(text);
                Vector2 size = style.CalcSize(content);

                Rect clip = new Rect(0f, 0f, sv.position.width, sv.position.height);
                GUI.BeginGroup(clip);
                try
                {
                    Rect r = new Rect(p.x - (size.x * 0.5f), p.y - size.y, size.x, size.y);
                    GUI.Label(r, content, style);
                }
                finally
                {
                    GUI.EndGroup();
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private void DrawHandWireframe(GrabItem grabItem)
        {
            Transform grabPoint = grabItem.Transform;

            Handles.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, _gizmoColor.a * 0.3f);

            // Draw simplified hand mesh
            Matrix4x4 handMatrix = Matrix4x4.TRS(grabPoint.position, grabPoint.rotation, Vector3.one);

            // Palm
            Vector3[] palmVertices = new Vector3[]
            {
                new Vector3(-HandSize.x/2, 0, -HandSize.z/2),
                new Vector3(HandSize.x/2, 0, -HandSize.z/2),
                new Vector3(HandSize.x/2, HandSize.y, -HandSize.z/2),
                new Vector3(-HandSize.x/2, HandSize.y, -HandSize.z/2),
                new Vector3(-HandSize.x/2, 0, HandSize.z/2),
                new Vector3(HandSize.x/2, 0, HandSize.z/2),
                new Vector3(HandSize.x/2, HandSize.y, HandSize.z/2),
                new Vector3(-HandSize.x/2, HandSize.y, HandSize.z/2)
            };

            // Transform vertices
            for (int i = 0; i < palmVertices.Length; i++)
            {
                palmVertices[i] = handMatrix.MultiplyPoint3x4(palmVertices[i]);
            }

            // Draw palm wireframe
            Handles.DrawLine(palmVertices[0], palmVertices[1]);
            Handles.DrawLine(palmVertices[1], palmVertices[2]);
            Handles.DrawLine(palmVertices[2], palmVertices[3]);
            Handles.DrawLine(palmVertices[3], palmVertices[0]);

            Handles.DrawLine(palmVertices[4], palmVertices[5]);
            Handles.DrawLine(palmVertices[5], palmVertices[6]);
            Handles.DrawLine(palmVertices[6], palmVertices[7]);
            Handles.DrawLine(palmVertices[7], palmVertices[4]);

            Handles.DrawLine(palmVertices[0], palmVertices[4]);
            Handles.DrawLine(palmVertices[1], palmVertices[5]);
            Handles.DrawLine(palmVertices[2], palmVertices[6]);
            Handles.DrawLine(palmVertices[3], palmVertices[7]);

            // Draw simplified fingers
            for (int i = 0; i < 5; i++)
            {
                float fingerOffset = -HandSize.x/2 + (i * HandSize.x / 4);
                Vector3 fingerBase = handMatrix.MultiplyPoint3x4(new Vector3(fingerOffset, HandSize.y, 0));
                Vector3 fingerTip = handMatrix.MultiplyPoint3x4(new Vector3(fingerOffset, HandSize.y + FingerLength, 0));

                Handles.DrawLine(fingerBase, fingerTip);
            }
        }

        private void DrawPathPreview(GrabItem grabItem)
        {
            // Get path points
            Matrix4x4[] pathPoints = null;

            // Check for spline path
            SplinePathBuilder splineBuilder = grabItem.GetComponent<SplinePathBuilder>();
            if (splineBuilder != null && splineBuilder.HasSplinePath)
            {
                // For preview, use a dummy transform as source
                Transform dummySource = Camera.current?.transform;
                if (dummySource != null)
                {
                    pathPoints = splineBuilder.BuildPathFromTo(dummySource, grabItem.transform);
                }
            }
            else
            {
                // Simple direct path
                Transform handSource = Camera.current?.transform;
                if (handSource != null)
                {
                    pathPoints = grabItem.GetPathPointsFor(handSource);
                }
            }

            if (pathPoints != null && pathPoints.Length > 1)
            {
                Handles.color = _pathColor;

                // Draw path as dotted line
                Vector3[] positions = new Vector3[pathPoints.Length];
                for (int i = 0; i < pathPoints.Length; i++)
                {
                    positions[i] = pathPoints[i].GetPosition();
                }

                Handles.DrawAAPolyLine(3f, positions);

                // Draw waypoint markers
                for (int i = 0; i < positions.Length; i++)
                {
                    float t = (float)i / (positions.Length - 1);
                    Color waypointColor = Color.Lerp(Color.green, Color.red, t);
                    Handles.color = waypointColor;
                    Handles.SphereHandleCap(0, positions[i], Quaternion.identity, 0.01f, EventType.Repaint);
                }
            }
        }

        private void DrawGrabPointHandle(GrabItem grabItem)
        {
            if (grabItem.Transform == null)
                return;

            EditorGUI.BeginChangeCheck();

            // Position handle
            Vector3 newPosition = Handles.PositionHandle(grabItem.Transform.position, grabItem.Transform.rotation);

            if (EditorGUI.EndChangeCheck())
            {
                // Apply grid snapping if enabled
                if (_enableGridSnap)
                {
                    newPosition = SnapToGrid(newPosition, _gridSnapSize);
                }

                Undo.RecordObject(grabItem.Transform, "Move Grab Point");
                grabItem.Transform.position = newPosition;
                EditorUtility.SetDirty(grabItem.Transform);

                // Update live preview if enabled
                if (_enableLivePreview && _livePreviewController != null)
                {
                    UpdateLivePreview(grabItem);
                }
            }

            // Rotation handle (when shift is held)
            if (Event.current.shift)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion newRotation = Handles.RotationHandle(grabItem.Transform.rotation, grabItem.Transform.position);

                if (EditorGUI.EndChangeCheck())
                {
                    // Apply rotation snapping if enabled
                    if (_enableRotationSnap)
                    {
                        newRotation = SnapRotation(newRotation, _rotationSnapAngle);
                    }

                    Undo.RecordObject(grabItem.Transform, "Rotate Grab Point");
                    grabItem.Transform.rotation = newRotation;
                    EditorUtility.SetDirty(grabItem.Transform);

                    // Update live preview if enabled
                    if (_enableLivePreview && _livePreviewController != null)
                    {
                        UpdateLivePreview(grabItem);
                    }
                }
            }

            // Check for continuous movement with live preview
            if (_enableLivePreview && _livePreviewController != null)
            {
                if (grabItem.Transform.position != _lastGrabPointPosition ||
                    grabItem.Transform.rotation != _lastGrabPointRotation)
                {
                    UpdateLivePreview(grabItem);
                    _lastGrabPointPosition = grabItem.Transform.position;
                    _lastGrabPointRotation = grabItem.Transform.rotation;
                }
            }
        }

        private void StartLivePreview()
        {
            GrabItem grabItem = (GrabItem)target;

            if (_livePreviewController == null)
                _livePreviewController = new GrabAnimationPreviewController();

            // Find animation clip
            string animationName = $"Grab_{grabItem.ItemID}";
            string[] guids = AssetDatabase.FindAssets($"{animationName} t:AnimationClip");

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

                if (clip != null)
                {
                    _livePreviewController.StartPreview(grabItem, clip, _livePreviewHandSide);
                    _livePreviewController.SetLooping(true);
                    _livePreviewController.SetPlaybackSpeed(1.0f);
                    _lastGrabPointPosition = grabItem.Transform.position;
                    _lastGrabPointRotation = grabItem.Transform.rotation;
                }
            }
            else
            {
                // Still create preview hand even without animation
                _livePreviewController.StartPreview(grabItem, null, _livePreviewHandSide);
            }
        }

        private void StopLivePreview()
        {
            if (_livePreviewController != null)
            {
                _livePreviewController.StopPreview();
                _livePreviewController.Dispose();
                _livePreviewController = null;
            }
        }

        private void UpdateLivePreview(GrabItem grabItem)
        {
            if (_livePreviewController != null && _livePreviewController.IsPlaying)
            {
                // Force update the preview position to match the new grab point
                _livePreviewController.ForceUpdateHandPosition();
                SceneView.RepaintAll();
            }
        }

        private void OnDisable()
        {
            // Clean up live preview when inspector is closed
            if (_livePreviewController != null)
            {
                _livePreviewController.StopPreview();
                _livePreviewController.Dispose();
                _livePreviewController = null;
            }
        }

        // Alignment helper methods
        private void AlignGrabPointToCenter()
        {
            GrabItem grabItem = (GrabItem)target;
            if (grabItem.Transform == null)
            {
                EditorUtility.DisplayDialog("Error", "Grab Point Transform is not assigned", "OK");
                return;
            }

            Undo.RecordObject(grabItem.Transform, "Center Grab Point");
            Bounds bounds = CalculateBounds(grabItem.gameObject);
            grabItem.Transform.position = bounds.center;
            EditorUtility.SetDirty(grabItem.Transform);

            if (_enableLivePreview && _livePreviewController != null)
            {
                UpdateLivePreview(grabItem);
            }
        }

        private void SnapGrabPointToSurface()
        {
            GrabItem grabItem = (GrabItem)target;
            if (grabItem.Transform == null)
            {
                EditorUtility.DisplayDialog("Error", "Grab Point Transform is not assigned", "OK");
                return;
            }

            Undo.RecordObject(grabItem.Transform, "Snap to Surface");

            // Cast ray downward to find surface
            Ray ray = new Ray(grabItem.Transform.position + Vector3.up * 2f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                grabItem.Transform.position = hit.point + hit.normal * 0.05f;
                grabItem.Transform.rotation = Quaternion.LookRotation(hit.normal);
            }
            else
            {
                EditorUtility.DisplayDialog("No Surface Found", "No surface found below the grab point", "OK");
            }

            EditorUtility.SetDirty(grabItem.Transform);

            if (_enableLivePreview && _livePreviewController != null)
            {
                UpdateLivePreview(grabItem);
            }
        }

        private void AlignGrabPointToWorld()
        {
            GrabItem grabItem = (GrabItem)target;
            if (grabItem.Transform == null)
            {
                EditorUtility.DisplayDialog("Error", "Grab Point Transform is not assigned", "OK");
                return;
            }

            Undo.RecordObject(grabItem.Transform, "Align to World");
            grabItem.Transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(grabItem.Transform);

            if (_enableLivePreview && _livePreviewController != null)
            {
                UpdateLivePreview(grabItem);
            }
        }

        private void AlignGrabPointToParent()
        {
            GrabItem grabItem = (GrabItem)target;
            if (grabItem.Transform == null)
            {
                EditorUtility.DisplayDialog("Error", "Grab Point Transform is not assigned", "OK");
                return;
            }

            if (grabItem.Transform.parent == null)
            {
                EditorUtility.DisplayDialog("Error", "Grab Point has no parent", "OK");
                return;
            }

            Undo.RecordObject(grabItem.Transform, "Align to Parent");
            grabItem.Transform.rotation = grabItem.Transform.parent.rotation;
            EditorUtility.SetDirty(grabItem.Transform);

            if (_enableLivePreview && _livePreviewController != null)
            {
                UpdateLivePreview(grabItem);
            }
        }

        // Snapping utility methods
        private Vector3 SnapToGrid(Vector3 position, float gridSize)
        {
            if (gridSize <= 0)
                return position;

            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                Mathf.Round(position.y / gridSize) * gridSize,
                Mathf.Round(position.z / gridSize) * gridSize
            );
        }

        private Quaternion SnapRotation(Quaternion rotation, float angleSnap)
        {
            if (angleSnap <= 0)
                return rotation;

            Vector3 euler = rotation.eulerAngles;
            euler.x = Mathf.Round(euler.x / angleSnap) * angleSnap;
            euler.y = Mathf.Round(euler.y / angleSnap) * angleSnap;
            euler.z = Mathf.Round(euler.z / angleSnap) * angleSnap;
            return Quaternion.Euler(euler);
        }

        private void ExportForAnimation()
        {
            GrabItem grabItem = (GrabItem)target;

            if (!_exportOptions.IncludeRightHand && !_exportOptions.IncludeLeftHand)
            {
                EditorUtility.DisplayDialog(
                    "Export Error",
                    "Please select at least one hand to include in the export.",
                    "OK");
                return;
            }

            FBXAnimationExporter.ExportForAnimation(grabItem, _exportOptions);
        }
    }
}