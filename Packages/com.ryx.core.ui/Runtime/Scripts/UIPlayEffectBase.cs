using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ryx.Core.UI
{
	public abstract class UIPlayEffectBase : MonoBehaviour
	{
		[SerializeField]
		private UIEffectEvent triggers = UIEffectEvent.None;

		public UIEffectEvent Triggers => triggers;

		public abstract bool IsPlaying { get; }
		public abstract bool CanPlay();
		public abstract UniTask<bool> PlayEffect();
		public abstract void SetEffectInstant();

		public bool ShouldTrigger(UIEffectEvent evt)
		{
			return (triggers & evt) != 0;
		}

		public virtual UniTask<bool> TryPlay(UIEffectEvent evt)
		{
			if (!ShouldTrigger(evt) || !CanPlay())
				return UniTask.FromResult(false);
			return PlayEffect();
		}

		public virtual void SetInstantIfTriggered(UIEffectEvent evt)
		{
			if (ShouldTrigger(evt))
			{
				SetEffectInstant();
			}
		}
	}
}



