using System;
using UnityEngine;

namespace Ryx.Core.FPS
{
    [Flags]
    public enum InputPlatform
    {
        None = 0,
        Editor = 1 << 0,
        Desktop = 1 << 1,
        Mobile = 1 << 2,
        WebGL = 1 << 3,
        Console = 1 << 4,
        All = ~0
    }

    public static class InputPlatformUtility
    {
        public static InputPlatform CurrentPlatformMask
        {
            get
            {
                var mask = InputPlatform.None;

                if (Application.isEditor)
                {
                    mask |= InputPlatform.Editor;
                    return mask;
                }

                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    mask |= InputPlatform.WebGL;

                switch (SystemInfo.deviceType)
                {
                    case DeviceType.Handheld:
                        mask |= InputPlatform.Mobile;
                        break;
                    case DeviceType.Console:
                        mask |= InputPlatform.Console;
                        break;
                    default:
                        mask |= InputPlatform.Desktop;
                        break;
                }

                if (mask == InputPlatform.None)
                    mask = InputPlatform.Desktop;

                return mask;
            }
        }

        public static bool IsCurrentPlatformSupported(InputPlatform supportedPlatforms)
        {
            if (supportedPlatforms == InputPlatform.None)
                return false;

            return (supportedPlatforms & CurrentPlatformMask) != 0;
        }
    }
}


