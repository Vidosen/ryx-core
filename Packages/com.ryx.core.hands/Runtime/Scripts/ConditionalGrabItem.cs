using System.Collections.Generic;
using UnityEngine;

namespace Ryx.Core.Hands
{
	public class ConditionalGrabItem : GrabItem, IConditionalGrabItem
	{
		[SerializeField] private bool _isHot;
		[SerializeField] private List<ItemTagSO> _requiredToolTags = new();
		
		public bool IsHot
		{
			get => _isHot;
			set => _isHot = value;
		}
		
		public override bool CanBeGrabbedDirectly => !_isHot;
		
		public bool CanBeGrabbedByTool(ItemTagSO toolTag)
		{
			return _isHot && _requiredToolTags.Contains(toolTag);
		}
	}
}

