using System;
using UnityEngine;

namespace Ryx.Core.App
{
    /// <summary>
    /// Identity of a scene by build-settings name. A dedicated type so scene names stop
    /// travelling through the codebase as bare strings.
    /// </summary>
    [Serializable]
    public struct SceneId : IEquatable<SceneId>
    {
        [SerializeField] private string name;

        public SceneId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Scene name cannot be null or whitespace.", nameof(name));
            }

            this.name = name;
        }

        public string Name => name;

        public bool IsValid => !string.IsNullOrWhiteSpace(name);

        public bool Equals(SceneId other) => string.Equals(name, other.name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SceneId other && Equals(other);
        public override int GetHashCode() => name == null ? 0 : StringComparer.Ordinal.GetHashCode(name);
        public override string ToString() => name ?? "<none>";

        public static bool operator ==(SceneId left, SceneId right) => left.Equals(right);
        public static bool operator !=(SceneId left, SceneId right) => !left.Equals(right);
    }
}
