using UnityEngine;

namespace CustomTick
{
	/// <summary>
	/// Demo script that moves an object up and down using the Custom Tick System.
	/// Includes a runtime toggle to pause/resume ticking.
	/// </summary>
	public class SampleSceneDemo : MonoBehaviour
	{
		[Tooltip("How far to move on each tick.")]
		public float moveAmount = 0.5f;

		[Tooltip("Time between each movement (in seconds).")]
		public float tickInterval = 0.5f;

		[Tooltip("Toggle ticking on or off at runtime.")]
		public bool isPaused;

		private int direction = 1;
		private TickHandle tickHandle;
		private bool lastPauseState;

		private void Start()
		{
			tickHandle = TickBuilder.Create(() =>
				{
					transform.position += Vector3.up * moveAmount * direction;
					direction *= -1;
				})
				.SetInterval(tickInterval)
				.SetDescription("SampleSceneDemo.MoveUpDown")
				.Register();

			lastPauseState = isPaused;
			if (isPaused) tickHandle.Pause();
		}

		[Tick(0.2f, 0.95f)]
		private void MyCustomIntervalFunction()
		{
			Debug.Log("I'm doing something every 0.2 seconds, with a 0.95 second delay");
		}
		
		private void Update()
		{
			if (tickHandle.IsValid && isPaused != lastPauseState)
			{
				if (isPaused)
					tickHandle.Pause();
				else
					tickHandle.Resume();

				lastPauseState = isPaused;
			}
		}

		private void OnDestroy()
		{
			tickHandle.Unregister();
		}
	}
}