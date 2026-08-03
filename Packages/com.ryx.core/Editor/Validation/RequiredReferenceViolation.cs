namespace Ryx.Core.Editor.Validation
{
    public enum RequiredReferenceViolationKind
    {
        MissingValue,
        EmptyCollection,
        MissingCollectionElement,
        UnsupportedFieldType,
        UnreadableSerializedProperty,
    }

    public readonly struct RequiredReferenceViolation
    {
        internal RequiredReferenceViolation(
            string assetPath,
            string objectPath,
            string objectType,
            string fieldPath,
            int? elementIndex,
            RequiredReferenceViolationKind kind,
            string message)
        {
            AssetPath = assetPath;
            ObjectPath = objectPath;
            ObjectType = objectType;
            FieldPath = fieldPath;
            ElementIndex = elementIndex;
            Kind = kind;
            Message = message;
        }

        public string AssetPath { get; }
        public string ObjectPath { get; }
        public string ObjectType { get; }
        public string FieldPath { get; }
        public int? ElementIndex { get; }
        public RequiredReferenceViolationKind Kind { get; }
        public string Message { get; }

        public override string ToString() => Message;
    }
}
