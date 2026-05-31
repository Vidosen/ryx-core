using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
#if RYX_CORE_UI
using Ryx.Core.UI;
#endif

namespace Ryx.Core.Interaction
{
#if RYX_CORE_UI
	[Serializable]
	public struct NamedAnchor
	{
		public string id;
		public RectTransform anchor;
	}

	/// <summary>
	/// Stores all information needed to restore a button after context changes.
	/// </summary>
	public struct ButtonContextInfo
	{
		public UnityEngine.Object Owner;
		public InteractionId InteractionId;
		public InteractionConfig Config;
		public InteractionUILayoutPreset Layout;
		public Transform WorldTarget;
		public Action OnClick;
		public string TextOverride;
		public Sprite IconOverride;
		public bool HideOnClick;
		public bool KeepFixedWhenNoInteractions;
	}

	public class InteractionUIManager : MonoBehaviour
	{
		[SerializeField] private InteractionConfigDatabase _database;
		[SerializeField] private UISnappingButton _buttonPrefab;
		[SerializeField] private Transform _container;
		[SerializeField] private bool _hideOnClick = true;
		[SerializeField] private List<NamedAnchor> _fixedAnchors = new();

		[Header("Idle Button")]
		[SerializeField] private bool _showIdleButtonWhenNoInteractions = true;
		[SerializeField] private string _idleButtonText = "Interact";
		[SerializeField] private bool _idleUseFixedAnchor = true;
		[SerializeField] private string _idleFixedAnchorId = "UI_Anchor_BottomCenter";

		[Header("Context System")]
		[Tooltip("Default context when no other context is active. If null, all buttons with empty ValidContexts are shown.")]
		[SerializeField] private InteractionUIContext _defaultContext;
		[Tooltip("All available contexts for auto-mapping. Used to find context by linked InteractionType.")]
		[SerializeField] private List<InteractionUIContext> _contextDatabase = new();

		private FPSInteractionService _interactionService;
		private IDisposable _modeSubscription;

		public void Initialize(FPSInteractionService interactionService)
		{
			_interactionService = interactionService;
			SetupAutoMapping();
		}

		private readonly Dictionary<UnityEngine.Object, UISnappingButton> _activeByOwner = new();
		private readonly Stack<UISnappingButton> _pool = new();
		private Dictionary<string, RectTransform> _anchorLookup;

		// Context management
		private readonly Stack<InteractionUIContext> _contextStack = new();
		private readonly Dictionary<UnityEngine.Object, ButtonContextInfo> _buttonRegistry = new();
		private InteractionUIContext _currentContext;

		// Store default RectTransform values from the prefab for resetting pooled buttons
		private Vector2 _defaultAnchorMin;
		private Vector2 _defaultAnchorMax;
		private Vector2 _defaultAnchoredPosition;
		private Vector2 _defaultSizeDelta;
		private Vector3 _defaultSnapperOffset;
		private bool _initialized;
		private bool _defaultsCached;
		private UISnappingButton _idleButton;

		private void Awake()
		{
			EnsureInitialized();
			RefreshIdleButtonState();
		}

		// New API with Layout Preset
		public UISnappingButton ShowButtonFor(UnityEngine.Object owner, InteractionId interactionId, InteractionUILayoutPreset layout, Transform worldTarget, System.Action onClick, bool animate, string textOverride = null, Sprite iconOverride = null, bool hideOnClick = true, bool keepFixedWhenNoInteractions = true)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null))
			{
				Debug.LogWarning("[InteractionUIManager] owner is null.");
				return null;
			}

			if (!_buttonPrefab)
			{
				Debug.LogError("[InteractionUIManager] UISnappingButton prefab is not assigned.");
				return null;
			}

			if (!TryResolveConfig(interactionId, out var config)) return null;

			// Register button for context management (even if not shown due to context)
			RegisterButtonInContext(owner, interactionId, config, layout, worldTarget, onClick, textOverride, iconOverride, hideOnClick, keepFixedWhenNoInteractions);

			// Check if button should be visible in current context
			if (!IsButtonVisibleInContext(config))
			{
				// Button is registered but not shown - will appear when context changes
				return null;
			}

			return ShowButtonInternal(owner, interactionId, config, layout, worldTarget, onClick, animate, textOverride, iconOverride, hideOnClick);
		}

		// Legacy API for backward compatibility
		public UISnappingButton ShowButtonFor(UnityEngine.Object owner, InteractionId interactionId, Transform target, bool animate)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null))
			{
				Debug.LogWarning("[InteractionUIManager] owner is null.");
				return null;
			}

			if (!_buttonPrefab)
			{
				Debug.LogError("[InteractionUIManager] UISnappingButton prefab is not assigned.");
				return null;
			}

			if (!TryResolveConfig(interactionId, out var config)) return null;

			Action onClick = () =>
			{
				if (_interactionService == null)
				{
					Debug.LogError("[InteractionUIManager] FPSInteractionService is not initialized (call Initialize).");
					return;
				}
				_interactionService.SetInteractionMode(config.Type);
			};

			// Register button for context management
			RegisterButtonInContext(owner, interactionId, config, layout: null, worldTarget: target, onClick, textOverride: null, iconOverride: null, hideOnClick: true, keepFixedWhenNoInteractions: true);

			// Check if button should be visible in current context
			if (!IsButtonVisibleInContext(config))
			{
				return null;
			}

			return ShowButtonInternal(
				owner,
				interactionId,
				config,
				layout: null,
				worldTarget: target,
				onClick: onClick,
				animate: animate,
				textOverride: null,
				iconOverride: null,
				hideOnClick: true
			);
		}

		public UISnappingButton ShowButtonFor(UnityEngine.Object owner, InteractionId interactionId, Transform target, System.Action onClick, bool animate, string textOverride = null, Sprite iconOverride = null, bool hideOnClick = true, bool keepFixedWhenNoInteractions = true)
		{
			return ShowButtonFor(owner, interactionId, layout: null, worldTarget: target, onClick: onClick, animate: animate, textOverride: textOverride, iconOverride: iconOverride, hideOnClick: hideOnClick, keepFixedWhenNoInteractions: keepFixedWhenNoInteractions);
		}

		/// <summary>
		/// Updates the visuals of an existing button without hiding/showing it.
		/// Useful for dynamic button text that changes based on game state.
		/// </summary>
		public void UpdateButtonVisuals(UnityEngine.Object owner, InteractionId interactionId, string textOverride = null, Sprite iconOverride = null)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null)) return;
			if (!_activeByOwner.TryGetValue(owner, out var button) || !button) return;
			if (!_database || !_database.TryGet(interactionId, out var config)) return;

			var displayText = textOverride ?? (string.IsNullOrEmpty(config.DisplayName) ? config.LocalizationKey : config.DisplayName);
			var displayIcon = iconOverride ?? config.Icon;
			button.SetVisuals(displayIcon, displayText, color: config.Color);
		}

		/// <summary>
		/// Sets the interactable state of a button owned by the specified owner.
		/// Useful for enabling/disabling buttons based on game state.
		/// </summary>
		/// <param name="owner">The owner object of the button.</param>
		/// <param name="interactable">Whether the button should be interactable.</param>
		public void SetButtonInteractable(UnityEngine.Object owner, bool interactable)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null)) return;
			if (!_activeByOwner.TryGetValue(owner, out var button) || !button) return;
			button.Button.interactable = interactable;
		}

		/// <summary>
		/// Sets the interactable state of a button owned by a non-UnityEngine.Object owner.
		/// Useful for buttons with plain object owners.
		/// </summary>
		/// <param name="owner">The owner object of the button.</param>
		/// <param name="interactable">Whether the button should be interactable.</param>
		public void SetButtonInteractable(object owner, bool interactable)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null)) return;
			foreach (var kvp in _activeByOwner)
			{
				if (ReferenceEquals(kvp.Key, owner))
				{
					if (kvp.Value) kvp.Value.Button.interactable = interactable;
					return;
				}
			}
		}

		/// <summary>
		/// Enables a button and plays the Enable effect (highlight).
		/// </summary>
		public void EnableButtonFor(UnityEngine.Object owner, bool animate)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null)) return;
			if (!_activeByOwner.TryGetValue(owner, out var button) || !button) return;

			button.Button.interactable = true;
			button.Enable(animate).Forget();
		}

		/// <summary>
		/// Disables a button and plays the Disable effect (removes highlight).
		/// </summary>
		public void DisableButtonFor(UnityEngine.Object owner, bool animate)
		{
			EnsureInitialized();
			if (ReferenceEquals(owner, null)) return;
			if (!_activeByOwner.TryGetValue(owner, out var button) || !button) return;

			button.Disable(animate).Forget();
			button.Button.interactable = false;
		}

		/// <summary>
		/// Gets the display text for a given InteractionId from the config database.
		/// Useful for retrieving localized text without managing UI buttons.
		/// </summary>
		/// <param name="interactionId">The interaction ID to look up.</param>
		/// <returns>The display text from the config, or null if not found.</returns>
		public string GetDisplayText(InteractionId interactionId)
		{
			EnsureInitialized();
			if (string.IsNullOrEmpty(interactionId)) return null;
			if (!_database || !_database.TryGet(interactionId, out var config)) return null;
			return string.IsNullOrEmpty(config.DisplayName) ? config.LocalizationKey : config.DisplayName;
		}

		public void SetIdleButtonText(string text)
		{
			_idleButtonText = text ?? string.Empty;
			if (_idleButton)
			{
				_idleButton.SetVisuals(icon: null, labelText: _idleButtonText, color: null);
			}
		}

		#region Context Management

		/// <summary>
		/// The currently active UI context. Null means default context.
		/// </summary>
		public InteractionUIContext ActiveContext => _currentContext ?? _defaultContext;
		public event Action<InteractionUIContext> ContextChanged;

		/// <summary>
		/// Pushes a new context onto the stack. Buttons not valid for this context will be hidden.
		/// </summary>
		/// <param name="context">The context to push.</param>
		public void PushContext(InteractionUIContext context)
		{
			EnsureInitialized();
			if (context == null)
			{
				Debug.LogWarning("[InteractionUIManager] Cannot push null context.");
				return;
			}

			_contextStack.Push(context);
			_currentContext = context;
			EvaluateButtonVisibility();
			RaiseContextChanged();
		}

		/// <summary>
		/// Pops the current context from the stack. Restores previous context and re-evaluates button visibility.
		/// Buttons are restored instantly without animation.
		/// </summary>
		public void PopContext()
		{
			EnsureInitialized();
			if (_contextStack.Count == 0)
			{
				Debug.LogWarning("[InteractionUIManager] Context stack is empty, nothing to pop.");
				return;
			}

			_contextStack.Pop();
			_currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : null;
			EvaluateButtonVisibility();
			RaiseContextChanged();
		}

		/// <summary>
		/// Pops contexts until the specified context is removed (inclusive).
		/// Useful for cleaning up nested contexts when exiting a mode.
		/// </summary>
		/// <param name="targetContext">The context to pop to (will also be removed).</param>
		public void PopToContext(InteractionUIContext targetContext)
		{
			EnsureInitialized();
			while (_contextStack.Count > 0)
			{
				var popped = _contextStack.Pop();
				if (popped == targetContext) break;
			}
			_currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : null;
			EvaluateButtonVisibility();
			RaiseContextChanged();
		}

		/// <summary>
		/// Clears all contexts, returning to default state.
		/// All registered buttons valid for default context will be restored.
		/// </summary>
		public void ClearContexts()
		{
			EnsureInitialized();
			_contextStack.Clear();
			_currentContext = null;
			EvaluateButtonVisibility();
			RaiseContextChanged();
		}

		/// <summary>
		/// Checks if a button with the given config should be visible in the current context.
		/// </summary>
		/// <param name="config">The interaction config to check.</param>
		/// <returns>True if the button should be visible.</returns>
		public bool IsButtonVisibleInContext(InteractionConfig config)
		{
			if (config == null) return false;

			// Global buttons are always visible
			if (config.IsGlobalButton) return true;

			var activeContext = _currentContext ?? _defaultContext;

			// If no context is active (and no default), show buttons with empty ValidContexts
			if (activeContext == null)
			{
				return config.ValidContexts.Length == 0;
			}

			// Check if button is valid for the active context
			var validContexts = config.ValidContexts;

			// Empty ValidContexts means "default context only"
			if (validContexts.Length == 0)
			{
				return activeContext == _defaultContext;
			}

			// Check if active context is in the valid contexts list
			foreach (var validContext in validContexts)
			{
				if (validContext == activeContext) return true;
			}

			return false;
		}

		/// <summary>
		/// Checks if a specific context is currently active (either as current or in the stack).
		/// </summary>
		/// <param name="context">The context to check.</param>
		/// <returns>True if the context is in the stack.</returns>
		public bool IsContextActive(InteractionUIContext context)
		{
			if (context == null) return false;
			return _contextStack.Contains(context);
		}

		#endregion

		public void HideButtonFor(UnityEngine.Object owner, bool animate)
		{
			EnsureInitialized();

			var keepFixedDisabledWhenNoInteractions = ShouldKeepLastFixedButtonDisabledWhenNoInteractions(owner);

			// Unregister from context tracking - button is explicitly hidden, not just context-hidden
			UnregisterButtonFromContext(owner);

			HideButtonForAsync(owner, animate, keepFixedDisabledWhenNoInteractions).Forget();
		}

		private async UniTask HideButtonForAsync(UnityEngine.Object owner, bool animate, bool keepFixedDisabledWhenNoInteractions)
		{
			try
			{
				if (ReferenceEquals(owner, null)) return;
				if (!_activeByOwner.TryGetValue(owner, out var button) || !button) return;
				_activeByOwner.Remove(owner); // Remove from dict immediately to prevent reuse conflicts

				// If this is the last active button, keep a fixed-layout button in-place and disabled
				// instead of letting it disappear when there are no interactions available.
				if (_activeByOwner.Count == 0 && keepFixedDisabledWhenNoInteractions)
				{
					HideIdleButton();
					_idleButton = button;

					_idleButton.Button.onClick.RemoveAllListeners();
					_idleButton.Button.interactable = false;

					// Keep current visuals and fixed layout as-is; just ensure it no longer snaps.
					DisableSnapper(_idleButton);
					return;
				}

				// Generic idle button behavior (optional)
				if (_showIdleButtonWhenNoInteractions && _activeByOwner.Count == 0)
				{
					HideIdleButton();
					_idleButton = button;

					_idleButton.gameObject.name = "Button_Idle_Interact";
					_idleButton.Button.onClick.RemoveAllListeners();
					_idleButton.Button.interactable = false;
					_idleButton.SetVisuals(icon: null, labelText: _idleButtonText, color: null);

					DisableSnapper(_idleButton);

					// Reset transform to defaults first, then apply fixed anchor (if configured).
					var rect = _idleButton.GetComponent<RectTransform>();
					if (rect)
					{
						rect.anchorMin = _defaultAnchorMin;
						rect.anchorMax = _defaultAnchorMax;
						rect.anchoredPosition = _defaultAnchoredPosition;
						rect.sizeDelta = _defaultSizeDelta;
						rect.localScale = Vector3.one;
					}

					if (_idleUseFixedAnchor && rect && TryGetFixedAnchor(_idleFixedAnchorId, out var anchor))
					{
						rect.anchorMin = anchor.anchorMin;
						rect.anchorMax = anchor.anchorMax;
						rect.anchoredPosition = anchor.anchoredPosition;
						rect.sizeDelta = anchor.sizeDelta;
					}

					return;
				}

				await button.Hide(animate);   // Wait for animation to complete
				CleanupButton(button);        // Only then return to pool
				RefreshIdleButtonState();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private bool ShouldKeepLastFixedButtonDisabledWhenNoInteractions(UnityEngine.Object owner)
		{
			if (ReferenceEquals(owner, null)) return false;
			if (!_buttonRegistry.TryGetValue(owner, out var info)) return false;
			if (!info.KeepFixedWhenNoInteractions) return false;
			if (!info.Layout) return false;
			return info.Layout.LayoutType == InteractionUILayoutType.Fixed;
		}

		private UISnappingButton GetOrCreate()
		{
			if (_pool.Count > 0)
			{
				var pooled = _pool.Pop();
				pooled.gameObject.SetActive(true);
				return pooled;
			}

			var instance = Instantiate(_buttonPrefab, _container);
			return instance;
		}

		private void EnsureInitialized()
		{
			if (!_initialized)
			{
				if (!_container)
				{
					var canvas = GetComponentInParent<Canvas>();
					_container = canvas ? canvas.transform : transform;
				}

				// Build anchor lookup
				_anchorLookup = new Dictionary<string, RectTransform>();
				foreach (var namedAnchor in _fixedAnchors)
				{
					if (!string.IsNullOrEmpty(namedAnchor.id) && namedAnchor.anchor)
					{
						_anchorLookup[namedAnchor.id] = namedAnchor.anchor;
					}
				}

				_initialized = true;
			}

			// Cache default RectTransform values from the prefab (can be assigned at runtime)
			if (!_defaultsCached && _buttonPrefab)
			{
				var prefabRect = _buttonPrefab.GetComponent<RectTransform>();
				if (prefabRect)
				{
					_defaultAnchorMin = prefabRect.anchorMin;
					_defaultAnchorMax = prefabRect.anchorMax;
					_defaultAnchoredPosition = prefabRect.anchoredPosition;
					_defaultSizeDelta = prefabRect.sizeDelta;
				}

				if (_buttonPrefab.snapper)
				{
					_defaultSnapperOffset = _buttonPrefab.snapper.Offset;
				}

				_defaultsCached = true;
			}
		}

		private UISnappingButton ShowButtonInternal(
			UnityEngine.Object owner,
			InteractionId interactionId,
			InteractionConfig config,
			InteractionUILayoutPreset layout,
			Transform worldTarget,
			System.Action onClick,
			bool animate,
			string textOverride = null,
			Sprite iconOverride = null,
			bool hideOnClick = true)
		{
			var reusedIdleButton = TakeIdleButtonWithoutCleanup();
			var button = GetOrCreateForOwner(owner, reusedIdleButton);
			ConfigureButton(button, config, layout, worldTarget, textOverride, iconOverride);
			button.gameObject.name = $"Button_{interactionId}";

			// Defer click handler setup until after show animation completes to prevent click-through.
			// Guard against races: if the button is hidden/reassigned during the show animation, don't attach.
			button.Show(animate)
				.ContinueWith(() => SetupClickHandlerIfCurrent(owner, button, onClick, animate, hideOnClick))
				.Forget();

			return button;
		}

		private UISnappingButton GetOrCreateForOwner(UnityEngine.Object owner, UISnappingButton reuseIfAvailable = null)
		{
			if (!_activeByOwner.TryGetValue(owner, out var button) || !button)
			{
				button = reuseIfAvailable ? reuseIfAvailable : GetOrCreate();
				_activeByOwner[owner] = button;
			}
			return button;
		}

		private UISnappingButton TakeIdleButtonWithoutCleanup()
		{
			if (_idleButton == null) return null;
			var button = _idleButton;
			_idleButton = null;
			return button;
		}

		private bool TryResolveConfig(InteractionId interactionId, out InteractionConfig config)
		{
			config = null;

			if (string.IsNullOrEmpty(interactionId))
			{
				Debug.LogWarning("[InteractionUIManager] interactionId is null or empty.");
				return false;
			}

			if (!_database)
			{
				Debug.LogError("[InteractionUIManager] InteractionConfigDatabase is not assigned.");
				return false;
			}

			if (!_database.TryGet(interactionId, out config))
			{
				Debug.LogWarning($"[InteractionUIManager] No config found for id '{interactionId}'.");
				return false;
			}

			return true;
		}

		private void ConfigureButton(UISnappingButton button, InteractionConfig config, InteractionUILayoutPreset layout, Transform worldTarget, string textOverride = null, Sprite iconOverride = null)
		{
			button.Button.interactable = true;

			var displayText = textOverride ?? (string.IsNullOrEmpty(config.DisplayName) ? config.LocalizationKey : config.DisplayName);
			var displayIcon = iconOverride ?? config.Icon;
			button.SetVisuals(displayIcon, displayText, config.Color);

			// Apply layout logic
			if (layout)
			{
				switch (layout.LayoutType)
				{
					case InteractionUILayoutType.Snapped:
						ApplySnappedLayout(button, worldTarget, layout.WorldOffset);
						break;

					case InteractionUILayoutType.Fixed:
						// Fixed to a predefined anchor
						DisableSnapper(button);

						if (TryGetFixedAnchor(layout.FixedAnchorID, out var anchor))
						{
							var buttonRect = button.GetComponent<RectTransform>();
							if (buttonRect)
							{
								// Match the anchor's position
								buttonRect.anchorMin = anchor.anchorMin;
								buttonRect.anchorMax = anchor.anchorMax;
								buttonRect.anchoredPosition = anchor.anchoredPosition;
								buttonRect.sizeDelta = anchor.sizeDelta;
							}
						}
						else
						{
							Debug.LogWarning($"[InteractionUIManager] Fixed anchor '{layout.FixedAnchorID}' not found.");
						}
						break;
				}
			}
			else
			{
				// Fallback: use world target if no layout.
				ApplySnappedLayout(button, worldTarget, _defaultSnapperOffset);
			}

			// Clear previous click handlers - new handler will be set up after Show completes
			button.Button.onClick.RemoveAllListeners();
		}

		private void ApplySnappedLayout(UISnappingButton button, Transform worldTarget, Vector3 snapOffset)
		{
			// Reset RectTransform to defaults in case it was used as fixed before
			var buttonRect = button.GetComponent<RectTransform>();
			if (buttonRect)
			{
				buttonRect.anchorMin = _defaultAnchorMin;
				buttonRect.anchorMax = _defaultAnchorMax;
				buttonRect.anchoredPosition = _defaultAnchoredPosition;
				buttonRect.sizeDelta = _defaultSizeDelta;
			}

			if (!button.snapper)
			{
				Debug.LogError($"[InteractionUIManager] Button '{button.name}' is missing UISnapper reference.");
				return;
			}

			button.snapper.Offset = snapOffset;
			button.snapper.Target = worldTarget;
			button.snapper.enabled = true;
		}

		private void DisableSnapper(UISnappingButton button)
		{
			if (!button.snapper) return;
			button.snapper.Target = null;
			button.snapper.Offset = _defaultSnapperOffset;
			button.snapper.enabled = false;
		}

		private void SetupClickHandlerIfCurrent(UnityEngine.Object owner, UISnappingButton button, System.Action onClick, bool animateHide, bool hideOnClick)
		{
			if (ReferenceEquals(owner, null) || !button) return;
			if (!_activeByOwner.TryGetValue(owner, out var current) || current != button) return;

			SetupClickHandler(owner, button, onClick, animateHide, hideOnClick);
		}

		private void SetupClickHandler(UnityEngine.Object owner, UISnappingButton button, System.Action onClick, bool animateHide, bool hideOnClick)
		{
			button.Button.onClick.AddListener(() =>
			{
				onClick?.Invoke();
				if (_hideOnClick && hideOnClick)
				{
					HideButtonFor(owner, animateHide);
				}
			});
		}

		private bool TryGetFixedAnchor(string anchorId, out RectTransform anchor)
		{
			anchor = null;
			if (_anchorLookup == null || string.IsNullOrEmpty(anchorId))
			{
				return false;
			}

			if (_anchorLookup.TryGetValue(anchorId, out anchor) && anchor)
			{
				return true;
			}

			if (!anchorId.StartsWith("UI_Anchor_", StringComparison.Ordinal))
			{
				var legacyAlias = $"UI_Anchor_{anchorId}";
				if (_anchorLookup.TryGetValue(legacyAlias, out anchor) && anchor)
				{
					return true;
				}
			}

			return false;
		}

		private void CleanupButton(UISnappingButton button)
		{
			button.Button.onClick.RemoveAllListeners();
			DisableSnapper(button);

			// Reset RectTransform to default values to prevent offset issues when reused
			var buttonRect = button.GetComponent<RectTransform>();
			if (buttonRect)
			{
				buttonRect.anchorMin = _defaultAnchorMin;
				buttonRect.anchorMax = _defaultAnchorMax;
				buttonRect.anchoredPosition = _defaultAnchoredPosition;
				buttonRect.sizeDelta = _defaultSizeDelta;
				buttonRect.localScale = Vector3.one; // Reset scale for pooled buttons
			}

			button.gameObject.SetActive(false);
			_pool.Push(button);
		}

		private void RefreshIdleButtonState()
		{
			if (!_showIdleButtonWhenNoInteractions)
			{
				HideIdleButton();
				return;
			}

			if (!_buttonPrefab) return;

			if (_activeByOwner.Count == 0)
			{
				ShowIdleButton();
			}
			else
			{
				HideIdleButton();
			}
		}

		private void ShowIdleButton()
		{
			if (_idleButton != null) return;

			_idleButton = GetOrCreate();
			_idleButton.gameObject.name = "Button_Idle_Interact";
			_idleButton.Button.onClick.RemoveAllListeners();
			_idleButton.Button.interactable = false;
			_idleButton.SetVisuals(icon: null, labelText: _idleButtonText, color: null);

			DisableSnapper(_idleButton);
			if (_idleUseFixedAnchor && TryGetFixedAnchor(_idleFixedAnchorId, out var anchor))
			{
				var buttonRect = _idleButton.GetComponent<RectTransform>();
				if (buttonRect)
				{
					buttonRect.anchorMin = anchor.anchorMin;
					buttonRect.anchorMax = anchor.anchorMax;
					buttonRect.anchoredPosition = anchor.anchoredPosition;
					buttonRect.sizeDelta = anchor.sizeDelta;
				}
			}

			_idleButton.Show(animate: false).Forget();
		}

		private void HideIdleButton()
		{
			if (_idleButton == null) return;
			CleanupButton(_idleButton);
			_idleButton = null;
		}

		#region Context Internal Methods

		/// <summary>
		/// Re-evaluates visibility of all registered buttons based on current context.
		/// Hides buttons not valid for current context, restores buttons that should be visible.
		/// </summary>
		private void EvaluateButtonVisibility()
		{
			var toHide = new List<UnityEngine.Object>();
			var toRestore = new List<UnityEngine.Object>();

			foreach (var kvp in _buttonRegistry)
			{
				var owner = kvp.Key;
				var info = kvp.Value;

				// Skip if owner was destroyed
				if (ReferenceEquals(owner, null)) continue;

				var shouldBeVisible = IsButtonVisibleInContext(info.Config);
				var isCurrentlyVisible = _activeByOwner.TryGetValue(owner, out var button) && button;

				if (shouldBeVisible && !isCurrentlyVisible)
				{
					toRestore.Add(owner);
				}
				else if (!shouldBeVisible && isCurrentlyVisible)
				{
					toHide.Add(owner);
				}
			}

			// Hide buttons that shouldn't be visible (instant, no animation)
			foreach (var owner in toHide)
			{
				HideButtonForContextChange(owner);
			}

			// Restore buttons that should be visible (instant, no animation)
			foreach (var owner in toRestore)
			{
				RestoreButtonForContextChange(owner);
			}

			RefreshIdleButtonState();
		}

		/// <summary>
		/// Hides a button due to context change (instantly, without animation).
		/// The button info remains in the registry for potential restoration.
		/// </summary>
		private void HideButtonForContextChange(UnityEngine.Object owner)
		{
			if (!_activeByOwner.TryGetValue(owner, out var button) || !button) return;
			_activeByOwner.Remove(owner);
			CleanupButton(button);
		}

		/// <summary>
		/// Restores a button after context change (instantly, without animation).
		/// Uses stored button info to recreate the button.
		/// </summary>
		private void RestoreButtonForContextChange(UnityEngine.Object owner)
		{
			if (!_buttonRegistry.TryGetValue(owner, out var info)) return;

			// Validate owner still exists
			if (ReferenceEquals(info.Owner, null)) return;

			ShowButtonInternal(
				owner,
				info.InteractionId,
				info.Config,
				info.Layout,
				info.WorldTarget,
				info.OnClick,
				animate: false, // Instant restoration
				info.TextOverride,
				info.IconOverride,
				info.HideOnClick
			);
		}

		/// <summary>
		/// Registers a button in the context registry for potential hide/restore operations.
		/// </summary>
		private void RegisterButtonInContext(
			UnityEngine.Object owner,
			InteractionId interactionId,
			InteractionConfig config,
			InteractionUILayoutPreset layout,
			Transform worldTarget,
			Action onClick,
			string textOverride,
			Sprite iconOverride,
			bool hideOnClick,
			bool keepFixedWhenNoInteractions)
		{
			_buttonRegistry[owner] = new ButtonContextInfo
			{
				Owner = owner,
				InteractionId = interactionId,
				Config = config,
				Layout = layout,
				WorldTarget = worldTarget,
				OnClick = onClick,
				TextOverride = textOverride,
				IconOverride = iconOverride,
				HideOnClick = hideOnClick,
				KeepFixedWhenNoInteractions = keepFixedWhenNoInteractions
			};
		}

		/// <summary>
		/// Unregisters a button from context tracking.
		/// Called when button is explicitly hidden (not just context-hidden).
		/// </summary>
		private void UnregisterButtonFromContext(UnityEngine.Object owner)
		{
			_buttonRegistry.Remove(owner);
		}

		/// <summary>
		/// Sets up auto-mapping subscription to FPSInteractionService.CurrentMode.
		/// When mode changes, automatically pushes/pops matching context.
		/// </summary>
		private void SetupAutoMapping()
		{
			if (_interactionService == null) return;
			if (_contextDatabase == null || _contextDatabase.Count == 0) return;

			// Clean up previous subscription if any
			_modeSubscription?.Dispose();

			InteractionUIContext lastAutoContext = null;

			_modeSubscription = _interactionService.CurrentMode.Subscribe(mode =>
			{
				// Pop previous auto-mapped context if any
				if (lastAutoContext != null && _contextStack.Count > 0 && _contextStack.Peek() == lastAutoContext)
				{
					PopContext();
					lastAutoContext = null;
				}

				// Find context linked to this mode
				if (mode != InteractionType.None)
				{
					foreach (var ctx in _contextDatabase)
					{
						if (ctx != null && ctx.HasLinkedMode && ctx.LinkedMode == mode)
						{
							PushContext(ctx);
							lastAutoContext = ctx;
							break;
						}
					}
				}
			});
		}

		private void OnDestroy()
		{
			_modeSubscription?.Dispose();
		}

		private void RaiseContextChanged()
		{
			ContextChanged?.Invoke(ActiveContext);
		}

		#endregion
	}
#endif
}
