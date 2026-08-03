using System.Linq;
using NUnit.Framework;
using Ryx.Core.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class RequiredReferenceAssetGraphTests
    {
        private const string Root = "Assets/__RyxRequiredReferenceGraphTests";
        private const string ScenePath = Root + "/Validation.unity";
        private const string InnerPrefabPath = Root + "/Inner.prefab";
        private const string OuterPrefabPath = Root + "/Outer.prefab";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "__RyxRequiredReferenceGraphTests");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void InactiveComponentInPrefabIsValidated()
        {
            CreatePrefab(InnerPrefabPath, active: false);

            var violation = RequiredReferenceValidator.ValidateAssetGraph(new[] { InnerPrefabPath }).Single();

            Assert.That(violation.AssetPath, Is.EqualTo(InnerPrefabPath));
            Assert.That(violation.ObjectPath, Is.EqualTo("Inner"));
        }

        [Test]
        public void PreviewSceneValidationPreservesSceneManagerSetup()
        {
            CreateScene(ScenePath);
            var before = EditorSceneManager.GetSceneManagerSetup();

            var violations = RequiredReferenceValidator.ValidateAssetGraph(new[] { ScenePath });

            Assert.That(violations.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                before.Select(DescribeSetup).ToArray(),
                EditorSceneManager.GetSceneManagerSetup().Select(DescribeSetup).ToArray());
        }

        [Test]
        public void NestedPrefabAndItsSourceAreBothReachable()
        {
            var inner = CreatePrefab(InnerPrefabPath, active: true);
            var outer = new GameObject("Outer");
            try
            {
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(inner);
                nested.transform.SetParent(outer.transform);
                PrefabUtility.SaveAsPrefabAsset(outer, OuterPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(outer);
            }

            var violations = RequiredReferenceValidator.ValidateAssetGraph(new[] { OuterPrefabPath });

            CollectionAssert.AreEquivalent(
                new[] { InnerPrefabPath, OuterPrefabPath },
                violations.Select(violation => violation.AssetPath).Distinct().ToArray());
        }

        [Test]
        public void CyclicScriptableObjectDependenciesAreVisitedOnce()
        {
            var firstPath = Root + "/A.asset";
            var secondPath = Root + "/B.asset";
            var first = ScriptableObject.CreateInstance<RequiredReferenceGraphNode>();
            var second = ScriptableObject.CreateInstance<RequiredReferenceGraphNode>();
            AssetDatabase.CreateAsset(first, firstPath);
            AssetDatabase.CreateAsset(second, secondPath);
            first.PointTo(second);
            second.PointTo(first);
            EditorUtility.SetDirty(first);
            EditorUtility.SetDirty(second);
            AssetDatabase.SaveAssets();

            var violations = RequiredReferenceValidator.ValidateAssetGraph(new[] { firstPath });

            CollectionAssert.AreEqual(
                new[] { firstPath, secondPath },
                violations.Select(violation => violation.AssetPath).ToArray());
        }

        [Test]
        public void DisabledBuildSceneIsNotARoot()
        {
            var enabledPath = Root + "/Enabled.unity";
            var disabledPath = Root + "/Disabled.unity";
            CreateScene(enabledPath, includeRequiredComponent: false);
            CreateScene(disabledPath, includeRequiredComponent: true);
            var original = EditorBuildSettings.scenes;

            try
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(enabledPath, enabled: true),
                    new EditorBuildSettingsScene(disabledPath, enabled: false),
                };

                Assert.That(RequiredReferenceValidator.ValidateBuildSceneGraph(), Is.Empty);
            }
            finally
            {
                EditorBuildSettings.scenes = original;
            }
        }

        [Test]
        public void RepeatedValidationUsesDeterministicOrdering()
        {
            CreatePrefab(InnerPrefabPath, active: true);
            CreatePrefab(OuterPrefabPath, active: true);

            var first = RequiredReferenceValidator.ValidateAssetGraph(new[] { OuterPrefabPath, InnerPrefabPath });
            var second = RequiredReferenceValidator.ValidateAssetGraph(new[] { InnerPrefabPath, OuterPrefabPath });

            CollectionAssert.AreEqual(first.Select(item => item.ToString()), second.Select(item => item.ToString()));
        }

        [Test]
        public void InvalidRootPathIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                RequiredReferenceValidator.ValidateAssetGraph(new[] { Root + "/Missing.asset" }));
        }

        private static GameObject CreatePrefab(string path, bool active)
        {
            var root = new GameObject("Root");
            root.SetActive(active);
            root.AddComponent<RequiredReferenceComponent>();
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateScene(string path, bool includeRequiredComponent = true)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            if (includeRequiredComponent) root.AddComponent<RequiredReferenceComponent>();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static string DescribeSetup(SceneSetup setup) =>
            $"{setup.path}|{setup.isLoaded}|{setup.isActive}";
    }
}
