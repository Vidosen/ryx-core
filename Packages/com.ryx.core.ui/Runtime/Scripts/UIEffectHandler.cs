using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ryx.Core.UI
{
	[Serializable]
	public class UIEffectHandler
	{
		[SerializeField]
		private GameObject target;

		public GameObject Target => target;

		public UniTask Play(UIEffectEvent evt, Component context)
		{
			return Play(evt, context ? context.gameObject : null);
		}

		public async UniTask<bool> Play(UIEffectEvent evt, GameObject context = null)
		{
			var go = ResolveTarget(context);
			if (!go) return false;

			var effects = go.GetComponents<UIPlayEffectBase>();
			if (effects == null || effects.Length == 0) return false;
			
			var effectTasks = new List<UniTask<bool>>(effects.Length);
			for (int i = 0; i < effects.Length; i++)
			{
				var effect = effects[i];
				if (effect)
				{
					effectTasks.Add(effect.TryPlay(evt));
				}
			}
			var results = await UniTask.WhenAll(effectTasks);
			return Array.TrueForAll(results, succeed => succeed);
		}

		public void SetInstant(UIEffectEvent evt, Component context)
		{
			SetInstant(evt, context ? context.gameObject : null);
		}

		public void SetInstant(UIEffectEvent evt, GameObject context = null)
		{
			var go = ResolveTarget(context);
			if (!go) return;

			var effects = go.GetComponents<UIPlayEffectBase>();
			if (effects == null || effects.Length == 0) return;

			for (var i = 0; i < effects.Length; i++)
			{
				var effect = effects[i];
				if (effect)
				{
					effect.SetInstantIfTriggered(evt);
				}
			}
		}

		public bool HasAny(UIEffectEvent evt, Component context)
		{
			return HasAny(evt, context ? context.gameObject : null);
		}

		public bool HasAny(UIEffectEvent evt, GameObject context = null)
		{
			var go = ResolveTarget(context);
			if (!go) return false;

			var effects = go.GetComponents<UIPlayEffectBase>();
			if (effects == null || effects.Length == 0) return false;

			for (int i = 0; i < effects.Length; i++)
			{
				var effect = effects[i];
				if (effect && effect.ShouldTrigger(evt))
				{
					return true;
				}
			}

			return false;
		}

		private GameObject ResolveTarget(GameObject context)
		{
			return target ? target : context;
		}
	}
}






