namespace CustomTick
{
	internal interface ITickItem
	{
		bool IsValid();
		int GetId();
		bool IsPaused();
		void SetPaused(bool paused);
		bool IsOneShot();
		bool ShouldTick(float deltaTime);
		void Execute();                  
	}
}