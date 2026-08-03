using System;
using NUnit.Framework;
using Ryx.Core.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class RequiredReferenceLoaderTests
    {
        private const string Root = "Assets/__RyxRequiredReferenceLoaderTests";
        private const string ScenePath = Root + "/Failure.unity";
        private const string PrefabPath = Root + "/Failure.prefab";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "__RyxRequiredReferenceLoaderTests");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void PreviewSceneClosesWhenInspectionThrows()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var before = EditorSceneManager.previewSceneCount;

            Assert.Throws<InvalidOperationException>(() =>
                RequiredReferenceAssetInspector.InspectScene(
                    ScenePath,
                    _ => throw new InvalidOperationException("inspection failed")));

            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(before));
        }

        [Test]
        public void PrefabContentsUnloadWhenInspectionThrows()
        {
            var source = new GameObject("Failure");
            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            GameObject loaded = null;

            Assert.Throws<InvalidOperationException>(() =>
                RequiredReferenceAssetInspector.InspectPrefab(
                    PrefabPath,
                    root =>
                    {
                        loaded = root;
                        throw new InvalidOperationException("inspection failed");
                    }));

            Assert.That(loaded == null, Is.True);
        }
    }
}
