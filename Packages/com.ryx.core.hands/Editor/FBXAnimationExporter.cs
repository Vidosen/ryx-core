using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if UNITY_FORMATS_FBX
using UnityEditor.Formats.Fbx.Exporter;
#endif

namespace Ryx.Core.Hands.Editor
{
    /// <summary>
    /// Handles FBX export for grab animations, creating a combined hierarchy
    /// of the grabbable item and hand skeleton for external animation tools.
    /// </summary>
    public static class FBXAnimationExporter
    {
        /// <summary>
        /// Export options for the FBX animation export
        /// </summary>
        public class ExportOptions
        {
            public bool IncludeRightHand = true;
            public bool IncludeLeftHand = false;
            public bool StripIKComponents = true;
            public bool StripMonoBehaviours = true;
        }

        /// <summary>
        /// Checks if FBX Exporter package is available
        /// </summary>
        public static bool IsFBXExporterAvailable()
        {
#if UNITY_FORMATS_FBX
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Main export method
        /// </summary>
        public static bool ExportForAnimation(GrabItem grabItem, ExportOptions options = null)
        {
            options ??= new ExportOptions();

            if (!IsFBXExporterAvailable())
            {
                EditorUtility.DisplayDialog(
                    "FBX Exporter Required",
                    "The FBX Exporter package (com.unity.formats.fbx) is required.\n\n" +
                    "Please install it via Package Manager.",
                    "OK");
                return false;
            }

            if (!ValidateGrabItem(grabItem, out string validationError))
            {
                EditorUtility.DisplayDialog("Export Error", validationError, "OK");
                return false;
            }

            string defaultName = $"{grabItem.ItemID}_AnimationRig.fbx";
            string savePath = EditorUtility.SaveFilePanel(
                "Export FBX for Animation",
                Application.dataPath,
                defaultName,
                "fbx");

            if (string.IsNullOrEmpty(savePath))
                return false;

            try
            {
                return PerformExport(grabItem, savePath, options);
            }
            catch (Exception ex)
            {
                Debug.LogError($"FBX Export failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Export Failed", $"Export failed: {ex.Message}", "OK");
                return false;
            }
        }

        private static bool ValidateGrabItem(GrabItem grabItem, out string error)
        {
            error = null;

            if (grabItem == null)
            {
                error = "GrabItem is null";
                return false;
            }

            if (string.IsNullOrEmpty(grabItem.ItemID))
            {
                error = "GrabItem must have an ItemID set";
                return false;
            }

            if (grabItem.Transform == null)
            {
                error = "GrabItem must have a GrabPoint Transform assigned";
                return false;
            }

            return true;
        }

        private static bool PerformExport(GrabItem grabItem, string savePath, ExportOptions options)
        {
#if UNITY_FORMATS_FBX
            GameObject exportRoot = null;

            try
            {
                exportRoot = CreateExportHierarchy(grabItem, options);

                if (exportRoot == null)
                {
                    Debug.LogError("Failed to create export hierarchy");
                    return false;
                }

                // Validate skinned meshes before export
                ValidateSkinnedMeshes(exportRoot);

                var exportSettings = new ExportModelOptions
                {
                    ExportFormat = ExportFormat.Binary,
                    KeepInstances = false,
                    EmbedTextures = false,
                    PreserveImportSettings = false
                };

                string result = ModelExporter.ExportObject(savePath, exportRoot, exportSettings);

                if (!string.IsNullOrEmpty(result))
                {
                    Debug.Log($"FBX exported successfully to: {result}");

                    if (EditorUtility.DisplayDialog(
                        "Export Complete",
                        $"FBX exported to:\n{result}\n\nOpen containing folder?",
                        "Open Folder",
                        "Close"))
                    {
                        EditorUtility.RevealInFinder(result);
                    }

                    return true;
                }
                else
                {
                    Debug.LogError("ModelExporter.ExportObject returned null/empty");
                    return false;
                }
            }
            finally
            {
                if (exportRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(exportRoot);
                }
            }
#else
            Debug.LogError("FBX Exporter package not available");
            return false;
#endif
        }

        private static GameObject CreateExportHierarchy(GrabItem grabItem, ExportOptions options)
        {
            GameObject exportRoot = new GameObject($"{grabItem.ItemID}_AnimationRig");
            exportRoot.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                GameObject itemClone = CloneItemForExport(grabItem);
                itemClone.transform.SetParent(exportRoot.transform, false);
                itemClone.name = $"Item_{grabItem.ItemID}";

                Transform grabPointClone = FindCorrespondingGrabPoint(grabItem, itemClone);

                if (grabPointClone == null)
                {
                    Debug.LogWarning("Could not find grab point in cloned item, using item root");
                    grabPointClone = itemClone.transform;
                }

                if (options.IncludeRightHand)
                {
                    AddHandToExport(exportRoot, grabPointClone, HandSide.Right, options);
                }

                if (options.IncludeLeftHand)
                {
                    AddHandToExport(exportRoot, grabPointClone, HandSide.Left, options);
                }

                return exportRoot;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(exportRoot);
                throw;
            }
        }

        private static GameObject CloneItemForExport(GrabItem grabItem)
        {
            GameObject clone = UnityEngine.Object.Instantiate(grabItem.gameObject);
            clone.hideFlags = HideFlags.HideAndDontSave;

            RemoveComponentsOfType<GrabItem>(clone);
            RemoveComponentsOfType<SplinePathBuilder>(clone);
            RemoveComponentsOfType<Collider>(clone);

            return clone;
        }

        private static Transform FindCorrespondingGrabPoint(GrabItem original, GameObject clone)
        {
            if (original.Transform == null)
                return null;

            string path = GetTransformPath(original.Transform, original.transform);

            if (string.IsNullOrEmpty(path))
            {
                return clone.transform;
            }

            return clone.transform.Find(path);
        }

        private static string GetTransformPath(Transform target, Transform root)
        {
            if (target == root)
                return "";

            var path = new List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }

        private static void AddHandToExport(GameObject exportRoot, Transform grabPoint,
            HandSide side, ExportOptions options)
        {
            GameObject handPrefab = FindHandPrefab(side);

            if (handPrefab == null)
            {
                Debug.LogWarning($"Could not find {side} hand prefab for export");
                return;
            }

            GameObject handInstance = UnityEngine.Object.Instantiate(handPrefab);
            handInstance.hideFlags = HideFlags.HideAndDontSave;
            handInstance.name = $"Hand_{side}";

            // Parent hand to grab point so animation is relative to grab point
            handInstance.transform.SetParent(grabPoint, false);
            handInstance.transform.localPosition = Vector3.zero;
            handInstance.transform.localRotation = Quaternion.identity;

            if (side == HandSide.Left)
            {
                Vector3 scale = handInstance.transform.localScale;
                scale.y = -Mathf.Abs(scale.y);
                handInstance.transform.localScale = scale;
            }

            if (options.StripIKComponents)
            {
                StripIKComponents(handInstance);
            }

            if (options.StripMonoBehaviours)
            {
                StripMonoBehaviours(handInstance);
            }
        }

        private static GameObject FindHandPrefab(HandSide side)
        {
            string searchPattern = side == HandSide.Right
                ? "RightHandInterctive t:Prefab"
                : "LeftHandInterctive t:Prefab";

            string[] guids = AssetDatabase.FindAssets(searchPattern);

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (side == HandSide.Left)
            {
                guids = AssetDatabase.FindAssets("RightHandInterctive t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            return null;
        }

        private static void StripIKComponents(GameObject root)
        {
            var ikTypeNames = new[]
            {
                "UnityEngine.Animations.Rigging.TwoBoneIKConstraint",
                "UnityEngine.Animations.Rigging.ChainIKConstraint",
                "UnityEngine.Animations.Rigging.RigBuilder",
                "UnityEngine.Animations.Rigging.Rig",
                "UnityEngine.Animations.Rigging.MultiAimConstraint",
                "UnityEngine.Animations.Rigging.MultiParentConstraint"
            };

            foreach (var typeName in ikTypeNames)
            {
                var type = Type.GetType(typeName + ", Unity.Animation.Rigging");
                if (type != null)
                {
                    var components = root.GetComponentsInChildren(type, true);
                    foreach (var comp in components)
                    {
                        UnityEngine.Object.DestroyImmediate(comp);
                    }
                }
            }

            var allComponents = root.GetComponentsInChildren<Component>(true);
            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                string compTypeName = comp.GetType().Name;
                // Be specific to avoid removing unrelated components (e.g. Rigidbody)
                if (compTypeName.Contains("IKConstraint") ||
                    compTypeName.Contains("TwoBoneIK") ||
                    compTypeName.Contains("ChainIK") ||
                    compTypeName == "RigBuilder" ||
                    compTypeName == "Rig" ||
                    compTypeName.Contains("AimConstraint") ||
                    compTypeName.Contains("ParentConstraint"))
                {
                    if (!(comp is Transform))
                    {
                        UnityEngine.Object.DestroyImmediate(comp);
                    }
                }
            }
        }

        private static void StripMonoBehaviours(GameObject root)
        {
            var keepTypes = new HashSet<Type>
            {
                typeof(Transform),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(SkinnedMeshRenderer),
                typeof(Animator)
            };

            var allComponents = root.GetComponentsInChildren<Component>(true);
            foreach (var comp in allComponents)
            {
                if (comp == null) continue;

                var compType = comp.GetType();

                if (keepTypes.Contains(compType))
                    continue;

                if (comp is MonoBehaviour)
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                }
            }
        }

        private static void RemoveComponentsOfType<T>(GameObject root) where T : Component
        {
            var components = root.GetComponentsInChildren<T>(true);
            foreach (var comp in components)
            {
                UnityEngine.Object.DestroyImmediate(comp);
            }
        }

        private static void ValidateSkinnedMeshes(GameObject root)
        {
            var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (skinnedMeshes.Length == 0)
            {
                Debug.LogWarning("FBX Export: No SkinnedMeshRenderer found in export hierarchy");
                return;
            }

            foreach (var smr in skinnedMeshes)
            {
                string path = GetHierarchyPath(smr.transform, root.transform);

                if (smr.sharedMesh == null)
                {
                    Debug.LogError($"FBX Export: SkinnedMeshRenderer at '{path}' has no mesh assigned!");
                    continue;
                }

                if (smr.bones == null || smr.bones.Length == 0)
                {
                    Debug.LogError($"FBX Export: SkinnedMeshRenderer at '{path}' has no bones assigned!");
                    continue;
                }

                // Check for null bones
                int nullBones = 0;
                foreach (var bone in smr.bones)
                {
                    if (bone == null) nullBones++;
                }

                if (nullBones > 0)
                {
                    Debug.LogWarning($"FBX Export: SkinnedMeshRenderer at '{path}' has {nullBones} null bone references");
                }

                // Verify mesh has bone weights
                var mesh = smr.sharedMesh;
                if (mesh.boneWeights.Length == 0 && mesh.GetAllBoneWeights().Length == 0)
                {
                    Debug.LogWarning($"FBX Export: Mesh '{mesh.name}' has no bone weights - skinning may not work");
                }
                else
                {
                    Debug.Log($"FBX Export: SkinnedMesh '{path}' validated - {smr.bones.Length} bones, mesh has bone weights");
                }
            }
        }

        private static string GetHierarchyPath(Transform target, Transform root)
        {
            if (target == root) return root.name;

            var path = new List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            if (current == root)
                path.Insert(0, root.name);

            return string.Join("/", path);
        }
    }
}
