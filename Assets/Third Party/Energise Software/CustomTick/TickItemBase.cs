namespace CustomTick
{
	internal abstract class TickItemBase : ITickItem
	{
		private int Id { get; set; }
		private float Interval { get; set; }
		private float Timer { get; set; }
		private float DelayRemaining { get; set; }
		private bool OneShot { get; set; }
		private bool paused;

		protected TickItemBase(int id, float interval, float delay, bool oneShot, bool paused)
		{
			Id = id;
			Interval = interval;
			Timer = interval;
			DelayRemaining = delay;
			OneShot = oneShot;
			this.paused = paused;
		}

		public bool IsPaused() => paused;
		public void SetPaused(bool p) => paused = p;
		public bool IsOneShot() => OneShot;
		public int GetId() => Id;

		public bool ShouldTick(float deltaTime)
		{
			if (paused || !ValidateTarget()) return false;

			if (DelayRemaining > 0f)
			{
				DelayRemaining -= deltaTime;
				if (DelayRemaining <= 0f)
					return true;
				return false;
			}

			Timer -= deltaTime;
			if (Timer <= 0f)
			{
				Timer = Interval;
				return true;
			}

			return false;
		}

		public bool IsValid() => ValidateTarget();

		protected abstract bool ValidateTarget();
		public abstract void Execute();
	}
}