using System;
using UnityEngine;

namespace Ryx.Core.Interaction
{
	[Serializable]
	public struct InteractionId : IEquatable<InteractionId>
	{
		[SerializeField] private string id;

		public string Id => id ?? string.Empty;

		public InteractionId(string id)
		{
			this.id = id;
		}

		public static readonly InteractionId None = new InteractionId("");

		public override string ToString()
		{
			return Id;
		}

		public bool Equals(InteractionId other)
		{
			return string.Equals(Id, other.Id, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is InteractionId other && Equals(other);
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public static bool operator ==(InteractionId left, InteractionId right) => left.Equals(right);
		public static bool operator !=(InteractionId left, InteractionId right) => !left.Equals(right);

		public static implicit operator string(InteractionId t) => t.Id;
		public static implicit operator InteractionId(string id) => new InteractionId(id);
	}
}





