using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ryx.Core.Save
{
    /// <summary>
    /// Raw persistence port. Implementations deal with bytes-on-a-medium only: no
    /// versioning, no serialization, no domain knowledge.
    /// </summary>
    public interface ISaveStore
    {
        UniTask<bool> ExistsAsync(SaveKey key, CancellationToken cancellationToken = default);

        /// <summary>Returns the stored contents, or <c>null</c> when the document does not exist.</summary>
        UniTask<string> ReadAsync(SaveKey key, CancellationToken cancellationToken = default);

        UniTask WriteAsync(SaveKey key, string contents, CancellationToken cancellationToken = default);

        UniTask DeleteAsync(SaveKey key, CancellationToken cancellationToken = default);

        UniTask<IReadOnlyList<SaveKey>> ListAsync(CancellationToken cancellationToken = default);
    }
}
