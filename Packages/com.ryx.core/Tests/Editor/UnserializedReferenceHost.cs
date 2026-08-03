using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class UnserializedReferenceHost : ScriptableObject
    {
        [RequiredReference] private RequiredReferenceTarget _target;
    }
}
