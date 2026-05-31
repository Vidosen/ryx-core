using System.Collections.Generic;
using UnityEngine;

namespace Ryx.Core.Interaction
{
	[CreateAssetMenu(fileName = "InteractionTypeDatabase", menuName = "Ryx/Interaction/TypeDatabase")]
	public class InteractionTypeDatabase : ScriptableObject
	{
		[SerializeField] private List<string> ids = new();

		private HashSet<string> _set;
		private bool _initialized;

		private void OnEnable()
		{
			_initialized = false;
		}

		private void Ensure()
		{
			if (_initialized) return;
			_set = new HashSet<string>(ids);
			_initialized = true;
		}

		public IReadOnlyList<string> Ids => ids;

		public bool IsValid(string value)
		{
			Ensure();
			return _set.Contains(value);
		}
	}
}



