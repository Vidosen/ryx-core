using System.Collections.Generic;
using UnityEngine;

namespace Ryx.Core.Interaction
{
    [CreateAssetMenu(fileName = "InteractionConfigDatabase", menuName = "Ryx/Interaction/ConfigDatabase")]
    public class InteractionConfigDatabase : ScriptableObject
    {
        [SerializeField] private List<InteractionConfig> configs = new();

        private Dictionary<string, InteractionConfig> _byId;
        private bool _initialized;

        private void OnEnable()
        {
            _initialized = false;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _byId = new Dictionary<string, InteractionConfig>();
            foreach (var cfg in configs)
            {
                if (!cfg || string.IsNullOrEmpty(cfg.Id)) continue;
                _byId[cfg.Id] = cfg;
            }
            _initialized = true;
        }

        public bool TryGet(string id, out InteractionConfig config)
        {
            EnsureInitialized();
            return _byId.TryGetValue(id, out config);
        }

		public System.Collections.Generic.IEnumerable<string> Ids
		{
			get
			{
				EnsureInitialized();
				return _byId.Keys;
			}
		}

		public bool TryGet(InteractionId id, out InteractionConfig config)
		{
			return TryGet(id.Id, out config);
		}
    }
}






