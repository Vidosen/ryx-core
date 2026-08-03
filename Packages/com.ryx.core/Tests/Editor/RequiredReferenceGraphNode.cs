using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class RequiredReferenceGraphNode : ScriptableObject
    {
        [SerializeField] private RequiredReferenceGraphNode _next;
        [SerializeField, RequiredReference] private RequiredReferenceTarget _required;

        public void PointTo(RequiredReferenceGraphNode next) => _next = next;
    }
}
