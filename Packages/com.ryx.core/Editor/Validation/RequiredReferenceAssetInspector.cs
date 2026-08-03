using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ryx.Core.Editor.Validation
{
    internal static class RequiredReferenceAssetInspector
    {
        internal static void Inspect(string assetPath, ICollection<RequiredReferenceViolation> violations)
        {
            switch (Path.GetExtension(assetPath).ToLowerInvariant())
            {
                case ".unity":
                    InspectScene(assetPath, scene => ValidateScene(scene, assetPath, violations));
                    return;
                case ".prefab":
                    InspectPrefab(assetPath, root => ValidateHierarchy(root, assetPath, violations));
                    return;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<ScriptableObject>())
            {
                RequiredReferenceObjectValidator.Validate(asset, assetPath, asset.name, violations);
            }
        }

        internal static void InspectScene(string assetPath, Action<Scene> inspect)
        {
            var scene = EditorSceneManager.OpenPreviewScene(assetPath);
            try
            {
                inspect(scene);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        internal static void InspectPrefab(string assetPath, Action<GameObject> inspect)
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                inspect(root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateScene(
            Scene scene,
            string assetPath,
            ICollection<RequiredReferenceViolation> violations)
        {
            foreach (var root in scene.GetRootGameObjects()) ValidateHierarchy(root, assetPath, violations);
        }

        private static void ValidateHierarchy(
            GameObject root,
            string assetPath,
            ICollection<RequiredReferenceViolation> violations)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (behaviour == null) continue;
                RequiredReferenceObjectValidator.Validate(
                    behaviour,
                    assetPath,
                    HierarchyPath(behaviour.transform),
                    violations);
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            var segments = new Stack<string>();
            for (var current = transform; current != null; current = current.parent) segments.Push(current.name);
            return string.Join("/", segments);
        }
    }
}
