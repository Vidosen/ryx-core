using System.Linq;
using NUnit.Framework;
using Ryx.Core.Editor.Validation;
using Ryx.Core.Validation;
using UnityEditor;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class RequiredReferenceObjectValidatorTests
    {
        private const string TestRoot = "Assets/__RyxRequiredReferenceTests";
        private const string HostPath = TestRoot + "/Host.asset";
        private const string TargetPath = TestRoot + "/Target.asset";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "__RyxRequiredReferenceTests");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestRoot);

        [Test]
        public void NullPublicSerializedReferenceIsReported()
        {
            var host = ScriptableObject.CreateInstance<PublicReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);

            var violations = RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath });

            var violation = violations.Single();
            Assert.That(violation.AssetPath, Is.EqualTo(HostPath));
            Assert.That(violation.ObjectType, Is.EqualTo(typeof(PublicReferenceHost).FullName));
            Assert.That(violation.FieldPath, Is.EqualTo(nameof(PublicReferenceHost.PublicReference)));
            Assert.That(violation.Kind, Is.EqualTo(RequiredReferenceViolationKind.MissingValue));
        }

        [Test]
        public void NullPrivateSerializedReferenceIsReported()
        {
            var host = ScriptableObject.CreateInstance<PrivateReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);

            var violations = RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath });

            Assert.That(violations.Single().FieldPath, Is.EqualTo("_privateReference"));
        }

        [Test]
        public void AssignedReferencePasses()
        {
            var target = ScriptableObject.CreateInstance<RequiredReferenceTarget>();
            AssetDatabase.CreateAsset(target, TargetPath);
            var host = ScriptableObject.CreateInstance<PublicReferenceHost>();
            host.PublicReference = target;
            AssetDatabase.CreateAsset(host, HostPath);

            Assert.That(RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }), Is.Empty);
        }

        [Test]
        public void PrivateBaseFieldIsReported()
        {
            var host = ScriptableObject.CreateInstance<DerivedReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);

            Assert.That(RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single().FieldPath,
                Is.EqualTo("_baseReference"));
        }

        [Test]
        public void NestedStructFieldIncludesCollectionIndexInPath()
        {
            var host = ScriptableObject.CreateInstance<NestedReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);
            var serialized = new SerializedObject(host);
            serialized.FindProperty("_nested").arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single().FieldPath,
                Is.EqualTo("_nested[0]._reference"));
        }

        [Test]
        public void NestedSerializableClassReferenceHasReadablePath()
        {
            var host = ScriptableObject.CreateInstance<NestedClassReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);

            Assert.That(RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single().FieldPath,
                Is.EqualTo("_nested._reference"));
        }

        [Test]
        public void EmptyRequiredCollectionIsReported()
        {
            var host = ScriptableObject.CreateInstance<CollectionReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);

            Assert.That(RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single().Kind,
                Is.EqualTo(RequiredReferenceViolationKind.EmptyCollection));
        }

        [Test]
        public void NullCollectionElementIsReportedWithIndex()
        {
            var target = ScriptableObject.CreateInstance<RequiredReferenceTarget>();
            AssetDatabase.CreateAsset(target, TargetPath);
            var host = ScriptableObject.CreateInstance<CollectionReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);
            var serialized = new SerializedObject(host);
            var targets = serialized.FindProperty("_targets");
            targets.arraySize = 2;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var violation = RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single();

            Assert.That(violation.Kind, Is.EqualTo(RequiredReferenceViolationKind.MissingCollectionElement));
            Assert.That(violation.ElementIndex, Is.EqualTo(1));
            Assert.That(violation.FieldPath, Is.EqualTo("_targets"));
        }

        [Test]
        public void RequiredListElementIsReportedWithIndex()
        {
            var host = ScriptableObject.CreateInstance<ListReferenceHost>();
            AssetDatabase.CreateAsset(host, HostPath);
            var serialized = new SerializedObject(host);
            var targets = serialized.FindProperty("_targets");
            targets.arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var violation = RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single();

            Assert.That(violation.Kind, Is.EqualTo(RequiredReferenceViolationKind.MissingCollectionElement));
            Assert.That(violation.ElementIndex, Is.EqualTo(0));
            Assert.That(violation.FieldPath, Is.EqualTo("_targets"));
        }

        [Test]
        public void DeletedObjectReferenceIsIdentifiedAsMissing()
        {
            var target = ScriptableObject.CreateInstance<RequiredReferenceTarget>();
            AssetDatabase.CreateAsset(target, TargetPath);
            var host = ScriptableObject.CreateInstance<PublicReferenceHost>();
            host.PublicReference = target;
            AssetDatabase.CreateAsset(host, HostPath);
            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(TargetPath);

            var violation = RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single();

            StringAssert.Contains("missing reference", violation.Message);
            Assert.That(violation.ToString(), Is.EqualTo(violation.Message));
        }

        [TestCase(typeof(UnsupportedReferenceHost), RequiredReferenceViolationKind.UnsupportedFieldType)]
        [TestCase(typeof(ManagedReferenceHost), RequiredReferenceViolationKind.UnsupportedFieldType)]
        [TestCase(typeof(UnserializedReferenceHost), RequiredReferenceViolationKind.UnreadableSerializedProperty)]
        public void InvalidMarkerUsageIsReported(System.Type hostType, RequiredReferenceViolationKind expectedKind)
        {
            var host = ScriptableObject.CreateInstance(hostType);
            AssetDatabase.CreateAsset(host, HostPath);

            Assert.That(RequiredReferenceValidator.ValidateAssetGraph(new[] { HostPath }).Single().Kind,
                Is.EqualTo(expectedKind));
        }
    }
}
