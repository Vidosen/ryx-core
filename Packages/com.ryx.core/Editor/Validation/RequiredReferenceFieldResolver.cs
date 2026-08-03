using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ryx.Core.Editor.Validation
{
    internal readonly struct ResolvedRequiredReferenceField
    {
        internal ResolvedRequiredReferenceField(FieldInfo field, string fieldPath, bool isCollectionElement)
        {
            Field = field;
            FieldPath = fieldPath;
            IsCollectionElement = isCollectionElement;
        }

        internal FieldInfo Field { get; }
        internal string FieldPath { get; }
        internal bool IsCollectionElement { get; }
    }

    internal static class RequiredReferenceFieldResolver
    {
        private const BindingFlags DeclaredInstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> FieldsByType = new();
        private static readonly Dictionary<Type, Dictionary<string, ResolvedRequiredReferenceField?>> ResolvedByHostType = new();

        internal static bool TryResolve(Type hostType, string propertyPath, out ResolvedRequiredReferenceField result)
        {
            if (!ResolvedByHostType.TryGetValue(hostType, out var paths))
            {
                paths = new Dictionary<string, ResolvedRequiredReferenceField?>(StringComparer.Ordinal);
                ResolvedByHostType[hostType] = paths;
            }

            if (!paths.TryGetValue(propertyPath, out var cached))
            {
                cached = Resolve(hostType, propertyPath);
                paths[propertyPath] = cached;
            }

            result = cached.GetValueOrDefault();
            return cached.HasValue;
        }

        private static ResolvedRequiredReferenceField? Resolve(Type hostType, string propertyPath)
        {
            var segments = propertyPath.Split('.');
            var currentType = hostType;
            FieldInfo leaf = null;
            var display = string.Empty;
            var isCollectionElement = false;

            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (segment == "Array" && index + 1 < segments.Length && segments[index + 1].StartsWith("data[", StringComparison.Ordinal))
                {
                    var data = segments[++index];
                    var bracket = data.Substring("data".Length);
                    display += bracket;
                    currentType = CollectionElementType(currentType);
                    if (currentType == null)
                    {
                        return null;
                    }

                    isCollectionElement = index == segments.Length - 1;
                    continue;
                }

                leaf = FindField(currentType, segment);
                if (leaf == null)
                {
                    return null;
                }

                display = display.Length == 0 ? segment : display + "." + segment;
                currentType = leaf.FieldType;
                isCollectionElement = false;
            }

            return leaf == null
                ? null
                : new ResolvedRequiredReferenceField(leaf, display, isCollectionElement);
        }

        internal static Type CollectionElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }

            return null;
        }

        internal static IEnumerable<FieldInfo> DeclaredFields(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in Fields(current).Values) yield return field;
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (Fields(current).TryGetValue(name, out var field)) return field;
            }

            return null;
        }

        private static Dictionary<string, FieldInfo> Fields(Type type)
        {
            if (FieldsByType.TryGetValue(type, out var cached)) return cached;

            cached = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (var field in type.GetFields(DeclaredInstanceFields)) cached[field.Name] = field;
            FieldsByType[type] = cached;
            return cached;
        }
    }
}
