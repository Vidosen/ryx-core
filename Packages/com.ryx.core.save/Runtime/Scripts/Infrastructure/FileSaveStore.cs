using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ryx.Core.Save.Infrastructure
{
    /// <summary>
    /// File-backed store rooted at <see cref="Application.persistentDataPath"/> by default.
    /// Writes are atomic (temp file + replace) so a crash mid-write cannot truncate an
    /// existing save. Falls back to synchronous I/O on WebGL, which has no thread pool.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private const string Extension = ".sav";
        private const string TempExtension = ".tmp";

        private readonly string _rootDirectory;

        public FileSaveStore(string rootDirectory = null, string subdirectory = "Saves")
        {
            var root = string.IsNullOrEmpty(rootDirectory) ? Application.persistentDataPath : rootDirectory;
            _rootDirectory = string.IsNullOrEmpty(subdirectory) ? root : Path.Combine(root, subdirectory);
        }

        public string RootDirectory => _rootDirectory;

        public UniTask<bool> ExistsAsync(SaveKey key, CancellationToken cancellationToken = default)
        {
            var path = PathFor(key);
            return RunAsync(() => File.Exists(path), cancellationToken);
        }

        public async UniTask<string> ReadAsync(SaveKey key, CancellationToken cancellationToken = default)
        {
            var path = PathFor(key);
            if (!File.Exists(path))
            {
                return null;
            }

            return await RunAsync(() => File.ReadAllText(path), cancellationToken);
        }

        public async UniTask WriteAsync(SaveKey key, string contents, CancellationToken cancellationToken = default)
        {
            var path = PathFor(key);
            var tempPath = path + TempExtension;

            await RunAsync(() =>
            {
                Directory.CreateDirectory(_rootDirectory);
                File.WriteAllText(tempPath, contents ?? string.Empty);

                // File.Replace is the atomic swap. Delete-then-Move would leave a window in
                // which the save does not exist at all — exactly the crash this guards against.
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }, cancellationToken);
        }

        public async UniTask DeleteAsync(SaveKey key, CancellationToken cancellationToken = default)
        {
            var path = PathFor(key);
            await RunAsync(() =>
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }, cancellationToken);
        }

        public UniTask<IReadOnlyList<SaveKey>> ListAsync(CancellationToken cancellationToken = default) =>
            RunAsync<IReadOnlyList<SaveKey>>(() =>
            {
                if (!Directory.Exists(_rootDirectory))
                {
                    return Array.Empty<SaveKey>();
                }

                var files = Directory.GetFiles(_rootDirectory, "*" + Extension, SearchOption.TopDirectoryOnly);
                var keys = new List<SaveKey>(files.Length);
                foreach (var file in files)
                {
                    keys.Add(new SaveKey(Path.GetFileNameWithoutExtension(file)));
                }

                return keys;
            }, cancellationToken);

        private string PathFor(SaveKey key) => Path.Combine(_rootDirectory, key.Value + Extension);

        private static UniTask<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(work());
#else
            return UniTask.RunOnThreadPool(work, cancellationToken: cancellationToken);
#endif
        }
    }
}
