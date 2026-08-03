using System.Collections.Generic;
using Ryx.Core.Validation;
using UnityEngine;

namespace Ryx.Core.Tests
{
    public sealed class ListReferenceHost : ScriptableObject
    {
        [SerializeField, RequiredReference] private List<RequiredReferenceTarget> _targets = new();
    }
}
