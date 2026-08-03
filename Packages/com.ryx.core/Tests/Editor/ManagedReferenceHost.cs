using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class ManagedReferenceHost : ScriptableObject
    {
        [SerializeReference, RequiredReference] private object _managed;
    }
}
