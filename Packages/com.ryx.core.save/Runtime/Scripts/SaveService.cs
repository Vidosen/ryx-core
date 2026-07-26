using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ryx.Core.Save
{
    /// <summary>
    /// Application-facing persistence facade. Owns envelope handling, versioning and
    /// migration; delegates the medium to <see cref="ISaveStore"/> and the payload format to
    /// <see cref="ISaveSerializer"/>.
    /// </summary>
    public interface ISaveService
    {
        UniTask<bool> ExistsAsync(SaveKey key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads and migrates a document. Returns <paramref name="fallback"/> when the document
        /// is absent, or when it is unreadable and <see cref="SaveServiceOptions.RecoverFromCorruption"/>
        /// is enabled.
        /// </summary>
        UniTask<T> LoadAsync<T>(SaveKey key, T fallback = default, CancellationToken cancellationToken = default);

        UniTask SaveAsync<T>(SaveKey key, T value, CancellationToken cancellationToken = default);

        UniTask DeleteAsync(SaveKey key, CancellationToken cancellationToken = default);
    }

    public sealed class SaveServiceOptions
    {
        public static readonly SaveServiceOptions Default = new();

        /// <summary>
        /// When true, a corrupted or unmigratable document is treated as absent and the caller
        /// receives its fallback instead of an exception. Errors are reported through
        /// <see cref="ISaveDiagnostics"/>.
        /// </summary>
        /// <remarks>
        /// Deliberately a settable property rather than an <c>init</c> accessor: Unity's
        /// .NET Standard 2.1 profile does not ship <c>IsExternalInit</c>.
        /// </remarks>
        public bool RecoverFromCorruption { get; set; } = true;
    }

    /// <summary>Reporting port so the package never depends on a concrete logger.</summary>
    public interface ISaveDiagnostics
    {
        void OnRecovered(SaveKey key, Exception error);
    }

    public sealed class NullSaveDiagnostics : ISaveDiagnostics
    {
        public static readonly NullSaveDiagnostics Instance = new();
        private NullSaveDiagnostics() { }
        public void OnRecovered(SaveKey key, Exception error) { }
    }

    public sealed class SaveService : ISaveService
    {
        private readonly ISaveStore _store;
        private readonly ISaveSerializer _serializer;
        private readonly ISaveSchemaRegistry _schemas;
        private readonly ISaveDiagnostics _diagnostics;
        private readonly SaveServiceOptions _options;

        public SaveService(
            ISaveStore store,
            ISaveSerializer serializer,
            ISaveSchemaRegistry schemas = null,
            ISaveDiagnostics diagnostics = null,
            SaveServiceOptions options = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _schemas = schemas ?? new SaveSchemaRegistry();
            _diagnostics = diagnostics ?? NullSaveDiagnostics.Instance;
            _options = options ?? SaveServiceOptions.Default;
        }

        public UniTask<bool> ExistsAsync(SaveKey key, CancellationToken cancellationToken = default) =>
            _store.ExistsAsync(key, cancellationToken);

        public async UniTask<T> LoadAsync<T>(SaveKey key, T fallback = default, CancellationToken cancellationToken = default)
        {
            var raw = await _store.ReadAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            try
            {
                var envelope = ReadEnvelope(key, raw);
                var pipeline = _schemas.Resolve(key);
                var payload = pipeline.Upgrade(envelope.payload, envelope.version);

                // An empty payload means the document was written from a null value; the
                // caller's fallback is a better answer than default(T).
                return string.IsNullOrEmpty(payload) ? fallback : _serializer.Deserialize<T>(payload);
            }
            catch (Exception error) when (_options.RecoverFromCorruption && IsRecoverable(error))
            {
                _diagnostics.OnRecovered(key, error);
                return fallback;
            }
        }

        public UniTask SaveAsync<T>(SaveKey key, T value, CancellationToken cancellationToken = default)
        {
            var pipeline = _schemas.Resolve(key);
            var envelope = new SaveEnvelope(pipeline.TargetVersion, _serializer.Serialize(value));
            var raw = _serializer.Serialize(envelope);
            return _store.WriteAsync(key, raw, cancellationToken);
        }

        public UniTask DeleteAsync(SaveKey key, CancellationToken cancellationToken = default) =>
            _store.DeleteAsync(key, cancellationToken);

        private SaveEnvelope ReadEnvelope(SaveKey key, string raw)
        {
            SaveEnvelope envelope;
            try
            {
                envelope = _serializer.Deserialize<SaveEnvelope>(raw);
            }
            catch (Exception error)
            {
                throw new SaveCorruptedException(key, "envelope could not be parsed.", error);
            }

            if (envelope == null)
            {
                throw new SaveCorruptedException(key, "envelope deserialized to null.");
            }

            if (envelope.version < 1)
            {
                throw new SaveCorruptedException(key, $"envelope declares invalid version {envelope.version}.");
            }

            return envelope;
        }

        private static bool IsRecoverable(Exception error) =>
            error is SaveCorruptedException or SaveMigrationException or FormatException or ArgumentException;
    }
}
