using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public abstract class BaseReferenceHost : ScriptableObject
    {
        [SerializeField, RequiredReference] private RequiredReferenceTarget _baseReference;
    }
}
