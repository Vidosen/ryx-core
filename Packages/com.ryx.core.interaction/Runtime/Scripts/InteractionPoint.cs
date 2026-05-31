using UnityEngine;
#if RYX_CORE_UI
using Ryx.Core.UI;
#endif

namespace Ryx.Core.Interaction
{
	#if RYX_CORE_UI
    [RequireComponent(typeof(BoxCollider))]
    public class InteractionPoint : MonoBehaviour
    {
        [SerializeField] private float thresholdDistance = 5f;
        [SerializeField] private InteractionId interactionId;
        [SerializeField] private InteractionUIManager uiManager;

		private BoxCollider _boxCollider;
		private Camera _playerCamera;
		private bool _isButtonVisible;
		Transform _targetTransform;
		

		private void Awake()
		{
			_boxCollider = GetComponent<BoxCollider>();
			_playerCamera = Camera.main;
			if (!_playerCamera)
				Debug.LogError("[InteractionPoint] Player Camera not found. Please tag your camera as 'MainCamera'.");
            if (!_targetTransform)
            {
            	var targetGo = new GameObject($"{name}_InteractionTarget");
            	_targetTransform = targetGo.transform;
            	_targetTransform.SetParent(transform, false);
            	_targetTransform.position = transform.position;
            }
            if (!uiManager)
            {
            	#if UNITY_2023_1_OR_NEWER
            	uiManager = FindFirstObjectByType<InteractionUIManager>();
            	#else
            	uiManager = FindObjectOfType<InteractionUIManager>();
            	#endif
            }
		}

		private void OnDestroy()
		{
            if (uiManager)
                uiManager.HideButtonFor(this, true);
		}

		private void Update()
		{
            if (!_playerCamera || !uiManager) return;
			var closestPoint = _boxCollider.ClosestPoint(_playerCamera.transform.position);
			var distance = Vector3.Distance(_playerCamera.transform.position, closestPoint);
			if (distance <= thresholdDistance && IsInsideFOV(closestPoint))
			{
				UpdateUIButtonPosition(closestPoint);
				if (!_isButtonVisible)
				{
                    _isButtonVisible = ShowUIButton();
				}
			}
			else if (_isButtonVisible)
			{
                HideUIButton();
				_isButtonVisible = false;
			}
		}

		private bool IsInsideFOV(Vector3 closestPoint)
		{
			var viewport = _playerCamera.WorldToViewportPoint(closestPoint);
			return viewport.x is >= 0 and < 1 && viewport.y is >= 0 and < 1 && viewport.z >= 0;
		}

        private bool ShowUIButton()
        {
            return uiManager.ShowButtonFor(this, interactionId, _targetTransform, true) != null;
        }

		private void UpdateUIButtonPosition(Vector3 position)
		{
			_targetTransform.position = position;
		}

        private void HideUIButton()
        {
            if (uiManager)
                uiManager.HideButtonFor(this, true);
        }

		private void OnDisable()
		{
			HideUIButton();
		}
	}
	#endif
}


