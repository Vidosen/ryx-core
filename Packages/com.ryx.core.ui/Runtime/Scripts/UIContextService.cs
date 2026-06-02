using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Ryx.Core.UI
{
	public sealed class UIContextService : IDisposable
	{
		private readonly List<ContextActivation> _activations = new();
		private readonly Dictionary<UIView, ViewRegistration> _registrations = new();
		private bool _disposed;

		public UIContext ActiveContext => _activations.Count > 0 ? _activations[^1].Context : null;
		public event Action<UIContext> ContextChanged;

		public IDisposable PushContext(UIContext context)
		{
			if (context == null) throw new ArgumentNullException(nameof(context));
			ThrowIfDisposed();

			var activation = new ContextActivation(this, context);
			_activations.Add(activation);
			Refresh();
			return activation;
		}

		public IDisposable Register(
			UIView view,
			IReadOnlyList<UIContext> allowedContexts = null,
			bool isGlobal = false,
			bool animateShow = false,
			bool animateHide = false)
		{
			if (view == null) throw new ArgumentNullException(nameof(view));
			ThrowIfDisposed();

			Unregister(view);
			var registration = new ViewRegistration(
				this,
				view,
				allowedContexts ?? Array.Empty<UIContext>(),
				isGlobal,
				animateShow,
				animateHide);
			_registrations.Add(view, registration);
			Apply(registration);
			return registration;
		}

		public IDisposable Register(UIContextViewBinding binding)
		{
			if (binding == null) throw new ArgumentNullException(nameof(binding));
			return Register(binding.View, binding.AllowedContexts, binding.IsGlobal, binding.AnimateShow, binding.AnimateHide);
		}

		public bool IsVisible(IReadOnlyList<UIContext> allowedContexts, bool isGlobal)
		{
			if (isGlobal) return true;

			var activeContext = ActiveContext;
			if (activeContext == null)
				return allowedContexts == null || allowedContexts.Count == 0;
			if (allowedContexts == null || allowedContexts.Count == 0)
				return false;

			for (var i = 0; i < allowedContexts.Count; i++)
				if (allowedContexts[i] == activeContext)
					return true;

			return false;
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_activations.Clear();
			foreach (var registration in _registrations.Values)
				registration.Detach();
			_registrations.Clear();
			ContextChanged = null;
		}

		private void Release(ContextActivation activation)
		{
			if (_disposed || !_activations.Remove(activation)) return;
			Refresh();
		}

		private void Unregister(UIView view)
		{
			if (!_registrations.Remove(view, out var registration)) return;
			registration.Detach();
		}

		private void Refresh()
		{
			foreach (var registration in _registrations.Values)
				Apply(registration);
			ContextChanged?.Invoke(ActiveContext);
		}

		private void Apply(ViewRegistration registration)
		{
			var allowed = IsVisible(registration.AllowedContexts, registration.IsGlobal);
			registration.View
				.SetContextAllowed(allowed, allowed ? registration.AnimateShow : registration.AnimateHide)
				.Forget();
		}

		private void ThrowIfDisposed()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(UIContextService));
		}

		private sealed class ContextActivation : IDisposable
		{
			private UIContextService _owner;
			public UIContext Context { get; }

			public ContextActivation(UIContextService owner, UIContext context)
			{
				_owner = owner;
				Context = context;
			}

			public void Dispose()
			{
				var owner = _owner;
				if (owner == null) return;
				_owner = null;
				owner.Release(this);
			}
		}

		private sealed class ViewRegistration : IDisposable
		{
			private UIContextService _owner;
			public UIView View { get; }
			public IReadOnlyList<UIContext> AllowedContexts { get; }
			public bool IsGlobal { get; }
			public bool AnimateShow { get; }
			public bool AnimateHide { get; }

			public ViewRegistration(
				UIContextService owner,
				UIView view,
				IReadOnlyList<UIContext> allowedContexts,
				bool isGlobal,
				bool animateShow,
				bool animateHide)
			{
				_owner = owner;
				View = view;
				AllowedContexts = allowedContexts;
				IsGlobal = isGlobal;
				AnimateShow = animateShow;
				AnimateHide = animateHide;
			}

			public void Dispose()
			{
				var owner = _owner;
				if (owner == null) return;
				_owner = null;
				owner.Unregister(View);
			}

			public void Detach()
			{
				_owner = null;
			}
		}
	}
}
