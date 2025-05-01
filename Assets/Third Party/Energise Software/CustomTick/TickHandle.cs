namespace CustomTick
{
	/// <summary>
	/// A lightweight handle representing a registered tick.
	/// </summary>
	/// <remarks>
	/// Used to control tick behavior at runtime without exposing the internal TickManager.
	/// </remarks>
	public struct TickHandle
	{
		/// <summary>
		/// The internal ID assigned to the tick. Used internally for lookups.
		/// </summary>
		internal int Id;

#if UNITY_EDITOR
		/// <summary>
		/// The type of tick (e.g., Action, Method) associated with this handle. Editor only.
		/// </summary>
		internal TickType Type;
#endif

		/// <summary>
		/// Returns true if this handle refers to a valid, currently registered tick.
		/// </summary>
		public bool IsValid => Id != 0;

		/// <summary>
		/// Pauses the tick associated with this handle.
		/// </summary>
		/// <remarks>
		/// Has no effect if the tick is already paused or the handle is invalid.
		/// </remarks>
		public void Pause()
		{
			if (IsValid) TickManager.Pause(this);
		}

		/// <summary>
		/// Resumes the tick associated with this handle.
		/// </summary>
		/// <remarks>
		/// Has no effect if the tick is already running or the handle is invalid.
		/// </remarks>
		public void Resume()
		{
			if (IsValid) TickManager.Resume(this);
		}

		/// <summary>
		/// Unregisters the tick associated with this handle.
		/// </summary>
		/// <remarks>
		/// Once unregistered, the tick will no longer be updated or executed.
		/// </remarks>
		public void Unregister()
		{
			if (IsValid) TickManager.Unregister(this);
		}
	}
}