using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Ryx.Core.UI.Tests
{
	public class UIContextServiceTests
	{
		private UIContextService _service;
		private GameObject _gameObject;
		private RecordingView _view;
		private UIContext _phone;
		private UIContext _dialog;

		[SetUp]
		public void SetUp()
		{
			_service = new UIContextService();
			_gameObject = new GameObject("View");
			_view = _gameObject.AddComponent<RecordingView>();
			_phone = ScriptableObject.CreateInstance<UIContext>();
			_dialog = ScriptableObject.CreateInstance<UIContext>();
		}

		[TearDown]
		public void TearDown()
		{
			_service.Dispose();
			UnityEngine.Object.DestroyImmediate(_gameObject);
			UnityEngine.Object.DestroyImmediate(_phone);
			UnityEngine.Object.DestroyImmediate(_dialog);
		}

		[Test]
		public void DefaultOnlyViewIsHiddenByOverlayAndRestoredAfterDispose()
		{
			using var registration = _service.Register(_view);

			using (var activation = _service.PushContext(_phone))
				Assert.That(_view.IsVisible, Is.False);

			Assert.That(_view.IsVisible, Is.True);
		}

		[Test]
		public void HiddenRequestedStateIsNotOverriddenWhenContextIsRemoved()
		{
			using var registration = _service.Register(_view);
			_view.Hide(false).Forget();

			using (var activation = _service.PushContext(_phone))
				Assert.That(_view.IsVisible, Is.False);

			Assert.That(_view.IsRequestedVisible, Is.False);
			Assert.That(_view.IsVisible, Is.False);
		}

		[Test]
		public void RegisterDoesNotActivateInitiallyInactiveView()
		{
			_gameObject.SetActive(false);

			using var registration = _service.Register(_view);

			Assert.That(_view.IsRequestedVisible, Is.False);
			Assert.That(_view.IsVisible, Is.False);
		}

		[Test]
		public void LatestRemainingHandleDeterminesActiveContext()
		{
			using var phone = _service.PushContext(_phone);
			var dialog = _service.PushContext(_dialog);
			Assert.That(_service.ActiveContext, Is.SameAs(_dialog));

			dialog.Dispose();
			Assert.That(_service.ActiveContext, Is.SameAs(_phone));
		}

		[Test]
		public void GlobalViewRemainsVisibleForOverlayContext()
		{
			using var registration = _service.Register(_view, isGlobal: true);
			using var activation = _service.PushContext(_phone);

			Assert.That(_view.IsVisible, Is.True);
		}

		[Test]
		public void RegistrationPassesConfiguredAnimationFlags()
		{
			using var registration = _service.Register(_view, animateShow: true, animateHide: true);
			using (var activation = _service.PushContext(_phone))
				Assert.That(_view.LastAnimate, Is.True);

			Assert.That(_view.LastAnimate, Is.True);
		}

		private sealed class RecordingView : UIView
		{
			public bool LastAnimate { get; private set; }

			protected override UniTask ApplyVisibility(bool animate)
			{
				LastAnimate = animate;
				return base.ApplyVisibility(animate);
			}
		}
	}
}
