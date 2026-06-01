using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ryx.Core.Editor
{
    public static class GitUtils
    {
        public const string RepositoryUrl = "git@github.com:Vidosen/ryx-core.git";

        public static string RunCommand(string args, string workingDirectory)
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

                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"Git Error: {error}");
                    return null;
                }

                return output;
            }
        }

        public static List<string> GetAllTags()
        {
            string output = RunCommand($"ls-remote --tags --refs {RepositoryUrl}", Application.dataPath);
            if (string.IsNullOrEmpty(output)) return new List<string>();

            return output.Split('\n')
                .Select(line => line.Split('\t').LastOrDefault())
                .Where(reference => !string.IsNullOrEmpty(reference) && reference.StartsWith("refs/tags/"))
                .Select(reference => reference.Substring("refs/tags/".Length))
                .ToList();
        }

        public static List<string> GetTagsForPackage(string packageName)
        {
            return GetAllTags().Where(t => t.StartsWith(packageName + "/")).ToList();
        }
    }
}
