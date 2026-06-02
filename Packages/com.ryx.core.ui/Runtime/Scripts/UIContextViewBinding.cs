using System;
using UnityEngine;

namespace Ryx.Core.UI
{
	public class UIContextViewBinding : MonoBehaviour
	{
		[SerializeField] private UIView _view;
		[SerializeField] private UIContext[] _allowedContexts;
		[SerializeField] private bool _isGlobal;
		[SerializeField] private bool _animateShow;
		[SerializeField] private bool _animateHide;

		private IDisposable _registration;

		public UIView View => _view ? _view : _view = GetComponent<UIView>();
		public UIContext[] AllowedContexts => _allowedContexts ?? Array.Empty<UIContext>();
		public bool IsGlobal => _isGlobal;
		public bool AnimateShow => _animateShow;
		public bool AnimateHide => _animateHide;

		public void Initialize(UIContextService service)
		{
			_registration?.Dispose();
			_registration = service?.Register(this);
		}

		protected virtual void OnDestroy()
		{
			_registration?.Dispose();
			_registration = null;
		}
	}
}
