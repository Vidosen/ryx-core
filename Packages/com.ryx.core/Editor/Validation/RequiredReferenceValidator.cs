using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ryx.Core.Editor.Validation
{
    public static class RequiredReferenceValidator
    {
        public static IReadOnlyList<RequiredReferenceViolation> ValidateBuildSceneGraph() =>
            ValidateBuildSceneGraph(out _);

        internal static IReadOnlyList<RequiredReferenceViolation> ValidateBuildSceneGraph(out int assetCount) =>
            ValidateAssetGraph(
                EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                out assetCount);

        public static IReadOnlyList<RequiredReferenceViolation> ValidateAssetGraph(
            IEnumerable<string> rootAssetPaths) =>
            ValidateAssetGraph(rootAssetPaths, out _);

        internal static IReadOnlyList<RequiredReferenceViolation> ValidateAssetGraph(
            IEnumerable<string> rootAssetPaths,
            out int assetCount)
        {
            if (rootAssetPaths == null) throw new ArgumentNullException(nameof(rootAssetPaths));

            var roots = rootAssetPaths.ToArray();
            if (roots.Any(path => string.IsNullOrWhiteSpace(path) ||
                                  AssetDatabase.IsValidFolder(path) ||
                                  AssetDatabase.LoadMainAssetAtPath(path) == null))
            {
                throw new ArgumentException("Every root must be an existing asset path.", nameof(rootAssetPaths));
            }

            var violations = new List<RequiredReferenceViolation>();
            var graph = RequiredReferenceAssetGraph.Traverse(roots);
            assetCount = graph.Count;
            foreach (var path in graph)
            {
                RequiredReferenceAssetInspector.Inspect(path, violations);
            }

            return violations
                .OrderBy(violation => violation.AssetPath, StringComparer.Ordinal)
                .ThenBy(violation => violation.ObjectPath, StringComparer.Ordinal)
                .ThenBy(violation => violation.ObjectType, StringComparer.Ordinal)
                .ThenBy(violation => violation.FieldPath, StringComparer.Ordinal)
                .ThenBy(violation => violation.ElementIndex)
                .ThenBy(violation => violation.Kind)
                .ToArray();
        }
    }
}
