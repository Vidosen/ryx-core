using NUnit.Framework;
using UnityEngine;

namespace Ryx.Core.Hands.Tests
{
	public class ItemTagTests
	{
		[Test]
		public void ToolGrabItem_AcceptsConfiguredTag()
		{
			var toolObject = new GameObject("Tool");
			var tag = ScriptableObject.CreateInstance<ItemTagSO>();
			var tool = toolObject.AddComponent<ToolGrabItem>();

			var field = typeof(ToolGrabItem).GetField(
				"_compatibleItemTags",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			var tags = (System.Collections.Generic.List<ItemTagSO>)field.GetValue(tool);
			tags.Add(tag);

			Assert.That(tool.CanGrabItemTag(tag), Is.True);
			Object.DestroyImmediate(toolObject);
			Object.DestroyImmediate(tag);
		}
	}
}
