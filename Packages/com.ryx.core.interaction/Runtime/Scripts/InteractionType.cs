using System;
using UnityEngine;

namespace Ryx.Core.Interaction
{
	[Serializable]
	public struct InteractionType : IEquatable<InteractionType>
	{
		[SerializeField] private string id;

		public string Id => id ?? string.Empty;

		public InteractionType(string id)
		{
			this.id = id;
		}

		public static readonly InteractionType None = new InteractionType("");
		public override string ToString()
		{
			return Id;
		}

		public bool Equals(InteractionType other)
		{
			return string.Equals(Id, other.Id, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is InteractionType other && Equals(other);
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public static bool operator ==(InteractionType left, InteractionType right) => left.Equals(right);
		public static bool operator !=(InteractionType left, InteractionType right) => !left.Equals(right);

		public static implicit operator string(InteractionType t) => t.Id;
		public static implicit operator InteractionType(string id) => new InteractionType(id);
	}
}



