using UnityEngine;

namespace Ryx.Core.UI
{
	[CreateAssetMenu(fileName = "UIContext", menuName = "Ryx/UI/Context")]
	public class UIContext : ScriptableObject
	{
		[SerializeField] private string _contextId;

		public string ContextId => string.IsNullOrEmpty(_contextId) ? name : _contextId;

		public override string ToString() => ContextId;
	}
}
