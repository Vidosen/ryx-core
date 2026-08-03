using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class RequiredReferenceComponent : MonoBehaviour
    {
        [SerializeField, RequiredReference] private UnityEngine.Object _target;
    }
}
