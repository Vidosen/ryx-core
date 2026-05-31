using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ryx.Core.UI
{
	public class UISnappingButton : UIButton
	{
		public UISnapper snapper;
		[SerializeField] private Image _icon;
		[SerializeField] private TMP_Text _label;

		public void SetVisuals(Sprite icon, string labelText, Color? color = null)
		{
			if (_icon)
			{
				_icon.sprite = icon;
				_icon.enabled = icon;
				if (color.HasValue) 
					_icon.color = color.Value;
			}

			if (_label)
			{
				_label.text = labelText ?? string.Empty;
				_label.enabled = !string.IsNullOrEmpty(_label.text);
			}
		}
	}
}



