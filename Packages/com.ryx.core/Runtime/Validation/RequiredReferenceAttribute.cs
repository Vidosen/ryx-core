using System;
using UnityEngine;

namespace Ryx.Core.Validation
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredReferenceAttribute : PropertyAttribute
    {
    }
}
