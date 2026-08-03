using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class CollectionReferenceHost : ScriptableObject
    {
        [SerializeField, RequiredReference] private RequiredReferenceTarget[] _targets = new RequiredReferenceTarget[0];
    }
}
