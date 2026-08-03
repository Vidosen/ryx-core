using System;
using System.Collections.Generic;
using System.Reflection;
using Ryx.Core.Validation;
using UnityEditor;
using UnityEngine;

namespace Ryx.Core.Editor.Validation
{
    internal static class RequiredReferenceObjectValidator
    {
        internal static void Validate(
            UnityEngine.Object target,
            string assetPath,
            string objectPath,
            ICollection<RequiredReferenceViolation> violations)
        {
            var serializedObject = new SerializedObject(target);
            var targetType = target.GetType();
            ReportUnreadableTopLevelFields(serializedObject, targetType, assetPath, objectPath, violations);

            var property = serializedObject.GetIterator();
            while (property.Next(enterChildren: true))
            {
                if (!RequiredReferenceFieldResolver.TryResolve(targetType, property.propertyPath, out var resolved) ||
                    resolved.IsCollectionElement ||
                    !resolved.Field.IsDefined(typeof(RequiredReferenceAttribute), inherit: true))
                {
                    continue;
                }

                ValidateProperty(property, resolved, targetType, assetPath, objectPath, violations);
            }
        }

        private static void ValidateProperty(
            SerializedProperty property,
            ResolvedRequiredReferenceField resolved,
            Type targetType,
            string assetPath,
            string objectPath,
            ICollection<RequiredReferenceViolation> violations)
        {
            var field = resolved.Field;
            if (field.IsDefined(typeof(SerializeReference), inherit: true))
            {
                AddViolation(violations, targetType, assetPath, objectPath, resolved.FieldPath, null,
                    RequiredReferenceViolationKind.UnsupportedFieldType,
                    "uses [SerializeReference], which RequiredReference does not support.");
                return;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    AddUnreadable(violations, targetType, assetPath, objectPath, resolved.FieldPath);
                    return;
                }

                if (property.objectReferenceValue == null)
                {
                    AddMissing(violations, targetType, assetPath, objectPath, resolved.FieldPath, null, property,
                        RequiredReferenceViolationKind.MissingValue);
                }

                return;
            }

            var elementType = RequiredReferenceFieldResolver.CollectionElementType(field.FieldType);
            if (elementType != null && typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                if (!property.isArray)
                {
                    AddUnreadable(violations, targetType, assetPath, objectPath, resolved.FieldPath);
                    return;
                }

                if (property.arraySize == 0)
                {
                    AddViolation(violations, targetType, assetPath, objectPath, resolved.FieldPath, null,
                        RequiredReferenceViolationKind.EmptyCollection, "is required but empty.");
                    return;
                }

                for (var index = 0; index < property.arraySize; index++)
                {
                    var element = property.GetArrayElementAtIndex(index);
                    if (element.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        AddUnreadable(violations, targetType, assetPath, objectPath, resolved.FieldPath);
                        return;
                    }

                    if (element.objectReferenceValue == null)
                    {
                        AddMissing(violations, targetType, assetPath, objectPath, resolved.FieldPath, index, element,
                            RequiredReferenceViolationKind.MissingCollectionElement);
                    }
                }

                return;
            }

            AddViolation(violations, targetType, assetPath, objectPath, resolved.FieldPath, null,
                RequiredReferenceViolationKind.UnsupportedFieldType,
                $"has unsupported type {field.FieldType.FullName}.");
        }

        private static void ReportUnreadableTopLevelFields(
            SerializedObject serializedObject,
            Type targetType,
            string assetPath,
            string objectPath,
            ICollection<RequiredReferenceViolation> violations)
        {
            foreach (var field in RequiredReferenceFieldResolver.DeclaredFields(targetType))
            {
                if (!field.IsDefined(typeof(RequiredReferenceAttribute), inherit: true)) continue;
                if (serializedObject.FindProperty(field.Name) != null) continue;
                AddUnreadable(violations, targetType, assetPath, objectPath, field.Name);
            }
        }

        private static void AddMissing(
            ICollection<RequiredReferenceViolation> violations,
            Type targetType,
            string assetPath,
            string objectPath,
            string fieldPath,
            int? elementIndex,
            SerializedProperty property,
            RequiredReferenceViolationKind kind)
        {
            var detail = property.objectReferenceInstanceIDValue != 0 ? "has a missing reference." : "is null.";
            AddViolation(violations, targetType, assetPath, objectPath, fieldPath, elementIndex, kind,
                $"is required but {detail}");
        }

        private static void AddUnreadable(
            ICollection<RequiredReferenceViolation> violations,
            Type targetType,
            string assetPath,
            string objectPath,
            string fieldPath) =>
            AddViolation(violations, targetType, assetPath, objectPath, fieldPath, null,
                RequiredReferenceViolationKind.UnreadableSerializedProperty,
                "is marked required but is not a readable Unity serialized property.");

        private static void AddViolation(
            ICollection<RequiredReferenceViolation> violations,
            Type targetType,
            string assetPath,
            string objectPath,
            string fieldPath,
            int? elementIndex,
            RequiredReferenceViolationKind kind,
            string detail)
        {
            var subject = elementIndex.HasValue ? $"{fieldPath}[{elementIndex.Value}]" : fieldPath;
            var message = $"{assetPath}\n{objectPath}\n{targetType.FullName}.{subject} {detail}";
            violations.Add(new RequiredReferenceViolation(
                assetPath, objectPath, targetType.FullName, fieldPath, elementIndex, kind, message));
        }
    }
}
