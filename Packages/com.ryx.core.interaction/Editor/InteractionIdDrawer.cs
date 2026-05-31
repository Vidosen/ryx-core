using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ryx.Core.Interaction.Editor
{
	[CustomPropertyDrawer(typeof(InteractionId))]
	public class InteractionIdDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var idProp = property.FindPropertyRelative("id");
			var current = idProp.stringValue ?? string.Empty;

			var db = FindDatabase();
			if (db == null)
			{
				EditorGUI.PropertyField(position, idProp, label);
				return;
			}

			var ids = db.Ids.ToArray();
			if (ids.Length == 0)
			{
				EditorGUI.PropertyField(position, idProp, label);
				return;
			}

			int index = Mathf.Max(0, System.Array.IndexOf(ids, current));
			index = EditorGUI.Popup(position, label.text, index, ids);
			idProp.stringValue = ids[Mathf.Clamp(index, 0, ids.Length - 1)];
		}

		private InteractionConfigDatabase FindDatabase()
		{
			var guids = AssetDatabase.FindAssets("t:InteractionConfigDatabase");
			if (guids == null || guids.Length == 0) return null;
			var path = AssetDatabase.GUIDToAssetPath(guids[0]);
			return AssetDatabase.LoadAssetAtPath<InteractionConfigDatabase>(path);
		}
	}
}





