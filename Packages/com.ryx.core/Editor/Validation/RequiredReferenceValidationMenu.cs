using System;
using UnityEditor;
using UnityEngine;

using Stopwatch = System.Diagnostics.Stopwatch;

namespace Ryx.Core.Editor.Validation
{
    internal static class RequiredReferenceValidationMenu
    {
        [MenuItem("Ryx/Validation/Required References")]
        private static void ValidateBuildGraph()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var violations = RequiredReferenceValidator.ValidateBuildSceneGraph(out var assetCount);
                stopwatch.Stop();

                if (violations.Count == 0)
                {
                    Debug.Log(
                        $"[Ryx] Required reference validation passed for {assetCount} assets " +
                        $"in {stopwatch.Elapsed.TotalSeconds:F2}s.");
                    return;
                }

                foreach (var violation in violations) Debug.LogError(violation.ToString());
                Debug.LogError(
                    $"[Ryx] Required reference validation found {violations.Count} violation(s) " +
                    $"across {assetCount} assets in {stopwatch.Elapsed.TotalSeconds:F2}s.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
