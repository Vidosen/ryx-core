using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class PublicReferenceHost : ScriptableObject
    {
        [RequiredReference] public RequiredReferenceTarget PublicReference;
    }
}
