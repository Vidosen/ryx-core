using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ryx.Core.Editor.Validation
{
    internal static class RequiredReferenceAssetGraph
    {
        internal static IReadOnlyList<string> Traverse(IEnumerable<string> roots)
        {
            var queue = new Queue<string>(roots.Select(Normalize));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var paths = new List<string>();

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var guid = AssetDatabase.AssetPathToGUID(path);
                var key = string.IsNullOrEmpty(guid) ? path : guid;
                if (!visited.Add(key)) continue;

                paths.Add(path);
                foreach (var dependency in AssetDatabase.GetDependencies(path, recursive: false)
                             .Select(Normalize)
                             .OrderBy(candidate => candidate, StringComparer.Ordinal))
                {
                    var dependencyGuid = AssetDatabase.AssetPathToGUID(dependency);
                    var dependencyKey = string.IsNullOrEmpty(dependencyGuid) ? dependency : dependencyGuid;
                    if (!visited.Contains(dependencyKey)) queue.Enqueue(dependency);
                }
            }

            return paths;
        }

        internal static string Normalize(string path) => path.Replace('\\', '/');
    }
}
