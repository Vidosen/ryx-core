using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ryx.Core.Save.Infrastructure
{
    /// <summary>Volatile store. Use in edit-mode tests and for "no persistence" builds.</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private readonly Dictionary<SaveKey, string> _documents = new();

        public int Count => _documents.Count;

        public UniTask<bool> ExistsAsync(SaveKey key, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(_documents.ContainsKey(key));

        public UniTask<string> ReadAsync(SaveKey key, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(_documents.TryGetValue(key, out var contents) ? contents : null);

        public UniTask WriteAsync(SaveKey key, string contents, CancellationToken cancellationToken = default)
        {
            _documents[key] = contents;
            return UniTask.CompletedTask;
        }

        public UniTask DeleteAsync(SaveKey key, CancellationToken cancellationToken = default)
        {
            _documents.Remove(key);
            return UniTask.CompletedTask;
        }

        public UniTask<IReadOnlyList<SaveKey>> ListAsync(CancellationToken cancellationToken = default) =>
            UniTask.FromResult<IReadOnlyList<SaveKey>>(_documents.Keys.ToArray());

        /// <summary>Test helper: seed a raw document without going through a serializer.</summary>
        public void Seed(SaveKey key, string contents) => _documents[key] = contents;
    }
}
