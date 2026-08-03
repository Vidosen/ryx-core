using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class PrivateReferenceHost : ScriptableObject
    {
        [SerializeField, RequiredReference] private RequiredReferenceTarget _privateReference;
    }
}
