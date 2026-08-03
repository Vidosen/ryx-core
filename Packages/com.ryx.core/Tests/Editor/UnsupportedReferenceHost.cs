using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class UnsupportedReferenceHost : ScriptableObject
    {
        [SerializeField, RequiredReference] private string _value;
    }
}
