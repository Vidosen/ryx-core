using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ryx.Core.UI.Editor
{
	[CustomPropertyDrawer(typeof(UIEffectHandler))]
	public class UIEffectHandlerDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float line = EditorGUIUtility.singleLineHeight + 2f;
			int lines = 1 + GetEventLinesCount(property);
			return lines * line;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			var lineHeight = EditorGUIUtility.singleLineHeight;
			var vPad = 2f;
			var rect = new Rect(position.x, position.y, position.width, lineHeight);

			var targetProp = property.FindPropertyRelative("target");
			EditorGUI.PropertyField(rect, targetProp, label);
			rect.y += lineHeight + vPad;

			var contextGO = ResolveTargetGO(property, targetProp);
			if (contextGO == null)
			{
				EditorGUI.HelpBox(rect, "No target: assign a GameObject or place on a Component.", MessageType.Info);
				EditorGUI.EndProperty();
				return;
			}

			var effects = contextGO.GetComponents<UIPlayEffectBase>();
			if (effects == null || effects.Length == 0)
			{
				EditorGUI.HelpBox(rect, "No UI effects found on target.", MessageType.None);
				EditorGUI.EndProperty();
				return;
			}

			foreach (UIEffectEvent evt in Enum.GetValues(typeof(UIEffectEvent)))
			{
				if (evt == UIEffectEvent.None) continue;
				if (!IsSingleFlag(evt)) continue;

				var names = effects.Where(e => e != null && e.ShouldTrigger(evt))
					.Select(e => e.GetType().Name)
					.Distinct()
					.ToArray();

				if (names.Length == 0) continue;

				EditorGUI.LabelField(rect, $"{evt}: {string.Join(", ", names)}");
				rect.y += lineHeight + vPad;
			}

			EditorGUI.EndProperty();
		}

		private static bool IsSingleFlag(UIEffectEvent evt)
		{
			int val = (int)evt;
			return val != 0 && (val & (val - 1)) == 0;
		}

		private static GameObject ResolveTargetGO(SerializedProperty property, SerializedProperty targetProp)
		{
			if (targetProp != null && targetProp.objectReferenceValue is GameObject goFromProp)
			{
				return goFromProp;
			}

			var owner = property.serializedObject.targetObject as Component;
			if (owner != null) return owner.gameObject;

			if (property.serializedObject.targetObject is GameObject go)
			{
				return go;
			}

			return null;
		}

		private static int GetEventLinesCount(SerializedProperty property)
		{
			var targetProp = property.FindPropertyRelative("target");
			var contextGO = ResolveTargetGO(property, targetProp);
			if (contextGO == null) return 1;

			var effects = contextGO.GetComponents<UIPlayEffectBase>();
			if (effects == null || effects.Length == 0) return 1;

			int count = 0;
			foreach (UIEffectEvent evt in Enum.GetValues(typeof(UIEffectEvent)))
			{
				if (evt == UIEffectEvent.None) continue;
				if (!IsSingleFlag(evt)) continue;

				bool any = false;
				for (int i = 0; i < effects.Length; i++)
				{
					var e = effects[i];
					if (e != null && e.ShouldTrigger(evt))
					{
						any = true;
						break;
					}
				}

				if (any) count++;
			}

			return count;
		}
	}
}





