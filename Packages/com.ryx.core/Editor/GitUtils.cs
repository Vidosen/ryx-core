using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Ryx.Core.Editor
{
    public static class GitUtils
    {
        public readonly struct RemoteTag
        {
            public RemoteTag(string name, string gitRef)
            {
                Name = name;
                GitRef = gitRef;
            }

            public string Name { get; }
            public string GitRef { get; }
        }

        public const string RepositoryUrl = "git@github.com:Vidosen/ryx-core.git";
        private const string FetchHeadRef = "FETCH_HEAD^{commit}";

        [Serializable]
        private class PackageManifestLookupData
        {
            public string name;
        }

        public static string RunCommand(string args, string workingDirectory)
        {
            return RunCommand(args, workingDirectory, true);
        }

        public static string RunCommand(string args, string workingDirectory, bool logOnFailure)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null) return null;
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    if (logOnFailure)
                    {
                        UnityEngine.Debug.LogError($"Git Error: {error}");
                    }
                    return null;
                }

                return output;
            }
        }

        public static List<RemoteTag> GetRemoteTags()
        {
            string output = RunCommand($"ls-remote --tags {RepositoryUrl}", Application.dataPath);
            if (string.IsNullOrEmpty(output)) return new List<RemoteTag>();

            var directRefs = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string line in output.Split('\n'))
            {
                ParseRemoteTagLine(line, directRefs);
            }

            return directRefs
                .Select(pair => new RemoteTag(pair.Key, pair.Value))
                .Where(tag => !string.IsNullOrEmpty(tag.Name) && !string.IsNullOrEmpty(tag.GitRef))
                .ToList();
        }

        public static List<string> GetAllTags()
        {
            return GetRemoteTags().Select(tag => tag.Name).ToList();
        }

        public static List<string> GetTagsForPackage(string packageName)
        {
            return GetAllTags().Where(t => t.StartsWith(packageName + "/")).ToList();
        }

        public static string GetRemotePackageManifest(string packageName, string gitRef)
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(gitRef)) return null;

            string cacheRepositoryPath = GetCacheRepositoryPath();
            if (string.IsNullOrEmpty(cacheRepositoryPath)) return null;

            if (!EnsureCacheRepository(cacheRepositoryPath)) return null;
            if (!FetchRef(cacheRepositoryPath, gitRef)) return null;

            string manifestPath = ResolveManifestPath(cacheRepositoryPath, packageName);
            if (string.IsNullOrEmpty(manifestPath))
            {
                UnityEngine.Debug.LogWarning($"[Ryx] Could not resolve package.json for {packageName} at {gitRef}.");
                return null;
            }

            string manifestJson = ReadGitFile(cacheRepositoryPath, FetchHeadRef, manifestPath);
            if (string.IsNullOrEmpty(manifestJson))
            {
                UnityEngine.Debug.LogWarning($"[Ryx] Could not read '{manifestPath}' for {packageName} at {gitRef}.");
            }

            return manifestJson;
        }

        private static void ParseRemoteTagLine(
            string line,
            Dictionary<string, string> directRefs)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            string[] parts = line.Split('\t');
            if (parts.Length < 2) return;

            string reference = parts[1];
            if (string.IsNullOrEmpty(reference) || !reference.StartsWith("refs/tags/")) return;

            string tagName = reference.Substring("refs/tags/".Length);
            if (tagName.EndsWith("^{}", StringComparison.Ordinal)) return;

            directRefs[tagName] = reference;
        }

        private static string GetCacheRepositoryPath()
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot)) return null;

            return Path.Combine(projectRoot, "Library", "RyxPackageManagerGitCache");
        }

        private static string GetProjectRoot()
        {
            if (string.IsNullOrEmpty(Application.dataPath)) return null;

            try
            {
                return Directory.GetParent(Application.dataPath)?.FullName;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[Ryx] Failed to determine project root: {exception.Message}");
                return null;
            }
        }

        private static bool EnsureCacheRepository(string repositoryPath)
        {
            try
            {
                Directory.CreateDirectory(repositoryPath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[Ryx] Failed to create git cache directory '{repositoryPath}': {exception.Message}");
                return false;
            }

            bool hasGitDirectory = Directory.Exists(Path.Combine(repositoryPath, ".git"));
            if (!hasGitDirectory && !RunCommandSucceeded("init", repositoryPath))
            {
                return false;
            }

            string originUrl = RunCommand("remote get-url origin", repositoryPath, false);
            if (string.IsNullOrWhiteSpace(originUrl))
            {
                return RunCommandSucceeded($"remote add origin {QuoteArgument(RepositoryUrl)}", repositoryPath);
            }

            if (!string.Equals(originUrl.Trim(), RepositoryUrl, StringComparison.Ordinal))
            {
                return RunCommandSucceeded($"remote set-url origin {QuoteArgument(RepositoryUrl)}", repositoryPath);
            }

            return true;
        }

        private static bool FetchRef(string repositoryPath, string gitRef)
        {
            return RunCommandSucceeded(
                $"fetch --depth=1 --force origin {QuoteArgument(gitRef)}",
                repositoryPath);
        }

        private static string ResolveManifestPath(string repositoryPath, string packageName)
        {
            string canonicalPath = $"Packages/{packageName}/package.json";
            if (!string.IsNullOrEmpty(ReadGitFile(repositoryPath, FetchHeadRef, canonicalPath, false)))
            {
                return canonicalPath;
            }

            string treeOutput = RunCommand($"ls-tree -r --name-only {FetchHeadRef}", repositoryPath, false);
            if (string.IsNullOrEmpty(treeOutput)) return null;

            string[] packageJsonPaths = treeOutput
                .Split('\n')
                .Select(path => path.Trim())
                .Where(path => path.EndsWith("/package.json", StringComparison.Ordinal))
                .ToArray();

            foreach (string path in packageJsonPaths)
            {
                string manifestJson = ReadGitFile(repositoryPath, FetchHeadRef, path, false);
                if (string.IsNullOrEmpty(manifestJson)) continue;

                string manifestPackageName = ParseManifestPackageName(manifestJson);
                if (string.Equals(manifestPackageName, packageName, StringComparison.Ordinal))
                {
                    return path;
                }
            }

            return null;
        }

        private static string ReadGitFile(string repositoryPath, string gitRef, string filePath)
        {
            return ReadGitFile(repositoryPath, gitRef, filePath, true);
        }

        private static string ReadGitFile(string repositoryPath, string gitRef, string filePath, bool logOnFailure)
        {
            return RunCommand($"show {gitRef}:{filePath}", repositoryPath, logOnFailure);
        }

        private static string ParseManifestPackageName(string manifestJson)
        {
            if (string.IsNullOrEmpty(manifestJson)) return null;

            try
            {
                return JsonUtility.FromJson<PackageManifestLookupData>(manifestJson)?.name;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        private static bool RunCommandSucceeded(string args, string workingDirectory)
        {
            return RunCommandSucceeded(args, workingDirectory, true);
        }

        private static bool RunCommandSucceeded(string args, string workingDirectory, bool logOnFailure)
        {
            return RunCommand(args, workingDirectory, logOnFailure) != null;
        }
    }
}
