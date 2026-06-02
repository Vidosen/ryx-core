using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ryx.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Ryx.Core.Interaction.Tests
{
	public class InteractionUIManagerContextTests
	{
		private GameObject _gameObject;
		private InteractionUIManager _manager;
		private UIContextService _contextService;
		private UIContext _phone;
		private InteractionConfig _config;
		private GameObject _buttonPrefabObject;
		private UIButton _buttonPrefab;

		[SetUp]
		public void SetUp()
		{
			_gameObject = new GameObject("Interaction UI Manager");
			_manager = _gameObject.AddComponent<InteractionUIManager>();
			_contextService = new UIContextService();
			_manager.UseContextService(_contextService);
			_phone = ScriptableObject.CreateInstance<UIContext>();
			_config = ScriptableObject.CreateInstance<InteractionConfig>();
			_buttonPrefabObject = new GameObject("Button Prefab", typeof(RectTransform), typeof(Button), typeof(UIButton));
			_buttonPrefab = _buttonPrefabObject.GetComponent<UIButton>();
			SetPrivateField(_manager, "_buttonPrefab", _buttonPrefab);
		}

		[TearDown]
		public void TearDown()
		{
			_contextService.Dispose();
			Object.DestroyImmediate(_gameObject);
			Object.DestroyImmediate(_phone);
			Object.DestroyImmediate(_config);
			Object.DestroyImmediate(_buttonPrefabObject);
		}

		[Test]
		public void DefaultOnlyInteractionIsHiddenByOverlayContext()
		{
			Assert.That(_manager.IsButtonVisibleInContext(_config), Is.True);

			using var activation = _contextService.PushContext(_phone);

			Assert.That(_manager.IsButtonVisibleInContext(_config), Is.False);
		}

		[Test]
		public void InteractionCanBeAllowedForOverlayContext()
		{
			SetPrivateField(_config, "_validContexts", new[] { _phone });

			using var activation = _contextService.PushContext(_phone);

			Assert.That(_manager.IsButtonVisibleInContext(_config), Is.True);
		}

		[Test]
		public void IdlePromptIsHiddenAndRestoredByOverlayContext()
		{
			InvokePrivateMethod(_manager, "RefreshIdleButtonState");
			var idleButton = GetPrivateField<UIButton>(_manager, "_idleButton");
			Assert.That(idleButton, Is.Not.Null);
			Assert.That(idleButton.IsVisible, Is.True);

			using (var activation = _contextService.PushContext(_phone))
				Assert.That(idleButton.IsVisible, Is.False);

			Assert.That(idleButton.IsVisible, Is.True);
		}

		[Test]
		public void StationPromptIsRestoredFromPoolAfterOverlayContext()
		{
			SetPrivateField(_manager, "_showIdleButtonWhenNoInteractions", false);
			var database = ScriptableObject.CreateInstance<InteractionConfigDatabase>();
			var owner = new GameObject("Owner");
			try
			{
				_config.name = "Open";
				SetPrivateField(database, "configs", new List<InteractionConfig> { _config });
				SetPrivateField(_manager, "_database", database);

				var original = _manager.ShowButtonFor(owner, "Open", target: null, onClick: null, animate: false);
				Assert.That(original, Is.Not.Null);

				using (var activation = _contextService.PushContext(_phone))
					Assert.That(original.IsVisible, Is.False);

				var restored = GetPrivateField<Dictionary<Object, UIButton>>(_manager, "_activeByOwner")[owner];
				Assert.That(restored, Is.SameAs(original));
				Assert.That(restored.IsVisible, Is.True);
			}
			finally
			{
				Object.DestroyImmediate(owner);
				Object.DestroyImmediate(database);
			}
		}

		private static void SetPrivateField(object target, string fieldName, object value)
		{
			var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null);
			field.SetValue(target, value);
		}

		private static T GetPrivateField<T>(object target, string fieldName)
		{
			var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null);
			return (T)field.GetValue(target);
		}

		private static void InvokePrivateMethod(object target, string methodName)
		{
			var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);
			method.Invoke(target, null);
		}
	}
}
