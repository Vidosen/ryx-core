using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Ryx.Core.UI
{
	[RequireComponent(typeof(Button))]
	public class UIButton : MonoBehaviour
	{
		[SerializeField] private UIEffectHandler _handler;
		public Button Button => _button? _button : _button = GetComponent<Button>();
		private Button _button;
		public Observable<Unit> Click => Observable.FromEvent(a => new UnityAction(a),
			h => Button.onClick.AddListener(h),
			h => Button.onClick.RemoveListener(h)
		);

		public async UniTask Show(bool animate)
		{
			gameObject.SetActive(true);
			if (_handler.HasAny(UIEffectEvent.Show) && animate)
				await _handler.Play(UIEffectEvent.Show);
			else
				_handler.SetInstant(UIEffectEvent.Show);

			Debug.Log($"{name} has shown", gameObject);
		}

		public async UniTask Hide(bool animate)
		{
			if (_handler.HasAny(UIEffectEvent.Hide) && animate)
				await _handler.Play(UIEffectEvent.Hide);
			else
				_handler.SetInstant(UIEffectEvent.Hide);

			gameObject.SetActive(false);
			Debug.Log($"{name} is hidden", gameObject);
		}

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

