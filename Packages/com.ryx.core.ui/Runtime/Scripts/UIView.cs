using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ryx.Core.UI
{
	public class UIView : MonoBehaviour
	{
		[SerializeField] protected UIEffectHandler _handler = new();

		private bool _requestedVisible = true;
		private bool _contextAllowed = true;
		private bool _visibilityInitialized;
		private int _visibilityRevision;

		public bool IsRequestedVisible => _requestedVisible;
		public bool IsVisible => gameObject.activeSelf;

		public UniTask Show(bool animate)
		{
			_visibilityInitialized = true;
			_requestedVisible = true;
			return ApplyVisibility(animate);
		}

		public UniTask Hide(bool animate)
		{
			_visibilityInitialized = true;
			_requestedVisible = false;
			return ApplyVisibility(animate);
		}

		public UniTask SetContextAllowed(bool allowed, bool animate)
		{
			EnsureVisibilityInitialized();
			_contextAllowed = allowed;
			return ApplyVisibility(animate);
		}

		private void EnsureVisibilityInitialized()
		{
			if (_visibilityInitialized)
				return;

			_requestedVisible = gameObject.activeSelf;
			_visibilityInitialized = true;
		}

		protected virtual async UniTask ApplyVisibility(bool animate)
		{
			var revision = ++_visibilityRevision;
			var shouldShow = _requestedVisible && _contextAllowed;

			if (shouldShow)
			{
				gameObject.SetActive(true);
				if (_handler.HasAny(UIEffectEvent.Show) && animate)
					await _handler.Play(UIEffectEvent.Show);
				else
					_handler.SetInstant(UIEffectEvent.Show);
				return;
			}

			if (_handler.HasAny(UIEffectEvent.Hide) && animate && gameObject.activeInHierarchy)
				await _handler.Play(UIEffectEvent.Hide);
			else
				_handler.SetInstant(UIEffectEvent.Hide);

			if (revision == _visibilityRevision)
				gameObject.SetActive(false);
		}
	}
}
