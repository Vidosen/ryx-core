using UnityEngine;

namespace Ryx.Core.FPS
{
    public abstract class LookInputBase : MonoBehaviour
    {
        [SerializeField] private InputPlatform supportedPlatforms = InputPlatform.All;

        public abstract Vector2 LookDelta { get; }
        public InputPlatform SupportedPlatforms => supportedPlatforms;
        public bool SupportsCurrentPlatform => InputPlatformUtility.IsCurrentPlatformSupported(supportedPlatforms);
    }
}
