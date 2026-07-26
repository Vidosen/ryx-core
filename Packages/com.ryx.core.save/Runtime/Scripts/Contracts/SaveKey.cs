using System;

namespace Ryx.Core.Save
{
    /// <summary>
    /// Identity of a single save document. Value object: two keys with the same
    /// <see cref="Value"/> are the same document regardless of where they were created.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <c>[Serializable]</c>. Unity serializes fields, not auto-properties,
    /// so a <c>[SerializeField] SaveKey</c> would round-trip as an empty struct. Save keys are
    /// declared in code (see the game's <c>SaveKeys</c> class), not authored in the inspector.
    /// </remarks>
    public readonly struct SaveKey : IEquatable<SaveKey>
    {
        private static readonly char[] InvalidCharacters = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

        public SaveKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Save key cannot be null or whitespace.", nameof(value));
            }

            if (value.IndexOfAny(InvalidCharacters) >= 0)
            {
                throw new ArgumentException(
                    $"Save key '{value}' contains characters that are not safe for file-backed stores.",
                    nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(SaveKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveKey other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "<invalid>";

        public static bool operator ==(SaveKey left, SaveKey right) => left.Equals(right);
        public static bool operator !=(SaveKey left, SaveKey right) => !left.Equals(right);

        public static implicit operator string(SaveKey key) => key.Value;
    }
}
