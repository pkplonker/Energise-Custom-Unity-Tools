using System;

namespace CustomTick
{
	internal class TickAction : TickItemBase
	{
		private Action Callback;

		public TickAction(int id, Action callback, float interval, float delay, bool oneShot, bool paused)
			: base(id, interval, delay, oneShot, paused)
		{
			Callback = callback;
		}

		protected override bool ValidateTarget()
		{
			return Callback != null;
		}

		public override void Execute()
		{
			Callback?.Invoke();
		}
	}
}