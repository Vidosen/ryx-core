using System;

namespace Ryx.Core.Save
{
    /// <summary>
    /// Payload serialization port. Swap the default <c>JsonUtilitySaveSerializer</c> for a
    /// Newtonsoft or MemoryPack backed implementation without touching call sites.
    /// </summary>
    public interface ISaveSerializer
    {
        string Serialize(object value, Type type);
        object Deserialize(string payload, Type type);
    }

    public static class SaveSerializerExtensions
    {
        public static string Serialize<T>(this ISaveSerializer serializer, T value)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            return serializer.Serialize(value, typeof(T));
        }

        public static T Deserialize<T>(this ISaveSerializer serializer, string payload)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            // Pattern match rather than cast: implementations return null for an empty
            // payload, and `(T)null` throws for value types.
            var result = serializer.Deserialize(payload, typeof(T));
            return result is T typed ? typed : default;
        }
    }
}
