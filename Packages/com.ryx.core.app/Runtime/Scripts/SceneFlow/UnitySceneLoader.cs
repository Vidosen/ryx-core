using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Ryx.Core.App.Infrastructure
{
    /// <summary>
    /// <see cref="SceneManager"/>-backed <see cref="ISceneLoader"/>. The only type in the
    /// package that touches Unity scene APIs.
    /// </summary>
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public UniTask LoadSingleAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default) =>
            LoadAsync(scene, LoadSceneMode.Single, progress, cancellationToken);

        public UniTask LoadAdditiveAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default) =>
            LoadAsync(scene, LoadSceneMode.Additive, progress, cancellationToken);

        public async UniTask UnloadAsync(SceneId scene, CancellationToken cancellationToken = default)
        {
            if (!IsLoaded(scene))
            {
                return;
            }

            var operation = SceneManager.UnloadSceneAsync(scene.Name);
            if (operation == null)
            {
                return;
            }

            await operation.ToUniTask(cancellationToken: cancellationToken);
        }

        public bool IsLoaded(SceneId scene)
        {
            if (!scene.IsValid) return false;
            var loaded = SceneManager.GetSceneByName(scene.Name);
            return loaded.IsValid() && loaded.isLoaded;
        }

        private static async UniTask LoadAsync(
            SceneId scene,
            LoadSceneMode mode,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (!scene.IsValid)
            {
                throw new ArgumentException("Scene id is not valid.", nameof(scene));
            }

            var operation = SceneManager.LoadSceneAsync(scene.Name, mode);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.Name}' could not be loaded. Is it added to Build Settings?");
            }

            await operation.ToUniTask(progress, cancellationToken: cancellationToken);
        }
    }
}
