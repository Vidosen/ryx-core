using System;
using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class NestedClassReferenceHost : ScriptableObject
    {
        [Serializable]
        private sealed class Payload
        {
            [SerializeField, RequiredReference] private RequiredReferenceTarget _reference;
        }

        [SerializeField] private Payload _nested = new();
    }
}
