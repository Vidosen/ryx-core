using System;

namespace Ryx.Core.UI
{
	[Flags]
	public enum UIEffectEvent
	{
		None = 0,
		Show = 1 << 0,
		Hide = 1 << 1,
		Alert = 1 << 2,
		Enable = 1 << 3,
		Disable = 1 << 4,
		Click = 1 << 5,
	}
}






