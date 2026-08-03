using System;
using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class NestedReferenceHost : ScriptableObject
    {
        [Serializable]
        private struct Payload
        {
            [SerializeField, RequiredReference] private RequiredReferenceTarget _reference;
        }

        [SerializeField] private Payload[] _nested = new Payload[0];
    }
}
