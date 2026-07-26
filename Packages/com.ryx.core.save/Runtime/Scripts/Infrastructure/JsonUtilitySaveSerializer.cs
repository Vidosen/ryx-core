using System;
using UnityEngine;

namespace Ryx.Core.Save.Infrastructure
{
    /// <summary>
    /// Default serializer built on <see cref="JsonUtility"/>. Zero extra dependencies, but
    /// inherits Unity's limits: no dictionaries, no polymorphism, no top-level collections,
    /// and target types must be <c>[Serializable]</c> with public/serialized fields.
    /// Swap in a Newtonsoft-backed <see cref="ISaveSerializer"/> when those limits bite.
    /// </summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        private readonly bool _prettyPrint;

        public JsonUtilitySaveSerializer(bool prettyPrint = false)
        {
            _prettyPrint = prettyPrint;
        }

        public string Serialize(object value, Type type)
        {
            if (value == null) return string.Empty;

            // JsonUtility always writes the runtime shape. If the declared type is an
            // interface or base class, the round trip through Deserialize(payload, type)
            // will not reconstruct it — say so loudly rather than losing data quietly.
            if (type != null && type != value.GetType() && (type.IsInterface || type.IsAbstract))
            {
                throw new NotSupportedException(
                    $"{nameof(JsonUtilitySaveSerializer)} cannot round-trip polymorphic type '{type.Name}' " +
                    $"(value is '{value.GetType().Name}'). Save a concrete document type, or supply a " +
                    $"Newtonsoft-backed {nameof(ISaveSerializer)}.");
            }

            return JsonUtility.ToJson(value, _prettyPrint);
        }

        public object Deserialize(string payload, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrEmpty(payload)) return null;
            return JsonUtility.FromJson(payload, type);
        }
    }
}
