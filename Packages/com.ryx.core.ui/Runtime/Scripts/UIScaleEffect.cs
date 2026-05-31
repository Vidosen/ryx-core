using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ryx.Core.UI
{
	public class UIScaleEffect : UIPlayEffectBase
	{
		[SerializeField]
		private Transform target;

		[SerializeField]
		private Vector3 from = Vector3.zero;

		[SerializeField]
		private Vector3 to = Vector3.one;

		[SerializeField]
		[Min(0f)]
		private float duration = 0.25f;

		[SerializeField]
		[Min(0f)]
		private float delay = 0f;

		[SerializeField]
		private bool useUnscaledTime = true;

		[SerializeField]
		private bool setFromBeforePlay = true;

		private bool isPlaying;

		private Transform Target => target ? target : transform;

		public override bool IsPlaying => isPlaying;

		public override bool CanPlay()
		{
			return isActiveAndEnabled && !IsPlaying && Target != null;
		}

		public override async UniTask<bool> PlayEffect()
		{
			if (!CanPlay()) return false;

			var t = Target;
			if (setFromBeforePlay)
			{
				t.localScale = from;
			}

			isPlaying = true;
			if (delay > 0f)
				await UniTask.Delay(System.TimeSpan.FromSeconds(delay), ignoreTimeScale: useUnscaledTime);

			var elapsed = 0f;
			var start = t.localScale;
			while (elapsed < duration && isPlaying)
			{
				await UniTask.Yield();
				elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
				t.localScale = Vector3.LerpUnclamped(start, to, duration <= 0f ? 1f : elapsed / duration);
			}
			if (isPlaying)
				t.localScale = to;
			isPlaying = false;
			return true;
		}

		public override void SetEffectInstant()
		{
			isPlaying = false;
			var t = Target;
			if (t)
			{
				t.localScale = to;
			}
		}

		private void OnDisable()
		{
			isPlaying = false;
		}
	}
}
