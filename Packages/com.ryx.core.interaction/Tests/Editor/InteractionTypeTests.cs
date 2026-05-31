using NUnit.Framework;

namespace Ryx.Core.Interaction.Tests
{
	public class InteractionTypeTests
	{
		[Test]
		public void InteractionType_UsesOrdinalIdentifierEquality()
		{
			Assert.That(new InteractionType("Open"), Is.EqualTo(new InteractionType("Open")));
			Assert.That(new InteractionType("Open"), Is.Not.EqualTo(new InteractionType("open")));
		}
	}
}
