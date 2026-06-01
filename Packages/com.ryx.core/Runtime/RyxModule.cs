using UnityEngine;

namespace Ryx.Core
{
    public abstract class RyxModule : MonoBehaviour
    {
        public abstract string ModuleName { get; }
        public abstract string Version { get; }

        protected virtual void Awake()
        {
            Debug.Log($"[Ryx] Initializing {ModuleName} v{Version}");
        }
    }
}
