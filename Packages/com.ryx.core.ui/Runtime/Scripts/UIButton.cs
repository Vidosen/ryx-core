using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Ryx.Core.UI
{
	[RequireComponent(typeof(Button))]
	public class UIButton : UIView
	{
		[SerializeField] private Image _icon;
		[SerializeField] private TMP_Text _label;

		public Button Button => _button? _button : _button = GetComponent<Button>();
		private Button _button;

		public void SetVisuals(Sprite icon, string labelText, Color? color = null)
		{
			if (_icon)
			{
				_icon.sprite = icon;
				_icon.enabled = icon;
				if (color.HasValue) 
					_icon.color = color.Value;
			}

			if (_label != null)
			{
				_label.text = labelText ?? string.Empty;
				_label.enabled = !string.IsNullOrEmpty(_label.text);
			}
		}

		public Observable<Unit> Click => Observable.FromEvent(a => new UnityAction(a),
			h => Button.onClick.AddListener(h),
			h => Button.onClick.RemoveListener(h)
		);

		public async UniTask Enable(bool animate)
		{
			if (_handler.HasAny(UIEffectEvent.Enable) && animate)
				await _handler.Play(UIEffectEvent.Enable);
			else
				_handler.SetInstant(UIEffectEvent.Enable);
		}

		public async UniTask Disable(bool animate)
		{
			if (_handler.HasAny(UIEffectEvent.Disable) && animate)
				await _handler.Play(UIEffectEvent.Disable);
			else
				_handler.SetInstant(UIEffectEvent.Disable);
		}
	}
}
