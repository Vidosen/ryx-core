using System;
using R3;

namespace Ryx.Core.Interaction
{
	public class FPSInteractionService : IDisposable
	{
		private readonly ReactiveProperty<InteractionType> _currentMode = new(InteractionType.None);

		public ReadOnlyReactiveProperty<InteractionType> CurrentMode => _currentMode.ToReadOnlyReactiveProperty();

		public void SetInteractionMode(InteractionType mode)
		{
			_currentMode.Value = mode;
		}

		public bool IsInMode(InteractionType mode)
		{
			return _currentMode.Value == mode;
		}

		public void ClearInteractionMode()
		{
			SetInteractionMode(InteractionType.None);
		}

		public void Dispose()
		{
			_currentMode.Dispose();
		}
	}
}



