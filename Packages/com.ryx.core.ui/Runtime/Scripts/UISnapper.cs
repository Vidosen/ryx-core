using System;
using UnityEngine;

namespace Ryx.Core.UI
{
	public class UISnapper : MonoBehaviour
	{
		private enum TransformMode
		{
			Anchor,
			Local,
			World
		}

		public Transform Target;
		[SerializeField] private TransformMode _mode;
		[SerializeField] private Vector3 _offset;
		private RectTransform _transform;
		private Canvas _canvas;
		[SerializeField] private Camera worldCamera;

		public Vector3 Offset
		{
			get => _offset;
			set => _offset = value;
		}

		private void Awake()
		{
			_transform = GetComponent<RectTransform>();
			var canvas = _transform.GetComponentInParent<Canvas>();
			_canvas = canvas.isRootCanvas ? canvas : canvas.rootCanvas;
			if (worldCamera == null) worldCamera = Camera.main;
		}

		private void Update()
		{
			if (Target == null) return;
			var pos = GetRectPositionOnCanvas(Target.position + _offset, _canvas, worldCamera);
			switch (_mode)
			{
				case TransformMode.Anchor:
					_transform.anchoredPosition = pos;
					break;
				case TransformMode.Local:
					_transform.localPosition = pos;
					break;
				case TransformMode.World:
					_transform.position = pos;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static Vector2 GetRectPositionOnCanvas(Vector3 worldPos, Canvas targetCanvas, Camera cam)
		{
			var canvasRect = targetCanvas.GetComponent<RectTransform>();
			var viewportPosition = cam.WorldToViewportPoint(worldPos);
			var canvasSizeDelta = canvasRect.sizeDelta;
			var screenPosition = new Vector2(
				viewportPosition.x * canvasSizeDelta.x - canvasSizeDelta.x * 0.5f,
				viewportPosition.y * canvasSizeDelta.y - canvasSizeDelta.y * 0.5f);
			return screenPosition;
		}
	}
}


