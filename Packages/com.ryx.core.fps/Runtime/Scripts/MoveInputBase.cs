using UnityEngine;
using Ryx.Core.FPS;

public abstract class MoveInputBase : MonoBehaviour
{
    [SerializeField] private InputPlatform supportedPlatforms = InputPlatform.All;

    public abstract Vector2 MoveDelta { get; }
    public InputPlatform SupportedPlatforms => supportedPlatforms;
    public bool SupportsCurrentPlatform => InputPlatformUtility.IsCurrentPlatformSupported(supportedPlatforms);
}