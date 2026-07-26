using System;

namespace Ryx.Core.Save
{
    /// <summary>
    /// On-disk wrapper around a payload. Every stored document is an envelope so that the
    /// schema version is readable without knowing the payload type.
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        // Public fields: UnityEngine.JsonUtility only serializes fields.
        public int version;
        public string payload;

        public SaveEnvelope()
        {
        }

        public SaveEnvelope(int version, string payload)
        {
            this.version = version;
            this.payload = payload;
        }
    }

    public sealed class SaveCorruptedException : Exception
    {
        public SaveCorruptedException(SaveKey key, string message, Exception inner = null)
            : base($"Save document '{key}' is corrupted: {message}", inner)
        {
            Key = key;
        }

        public SaveKey Key { get; }
    }

    public sealed class SaveMigrationException : Exception
    {
        public SaveMigrationException(string message) : base(message)
        {
        }
    }
}
