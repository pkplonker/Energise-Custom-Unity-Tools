using System;
using UnityEngine;

namespace CustomTick
{
	public class Runtime_ManualTicker : MonoBehaviour
	{
		private TickHandle manualActionHandle;
		private TickHandle manualParamHandle;

		private void Start()
		{
			Debug.Log("ExampleTicker Start at " + Time.time);
			manualActionHandle =
				TickManager.Register(() => Debug.Log($"Manual Action Tick at {Time.time}"), 1.5f, 1.0f);
			manualParamHandle = TickManager.Register(this, nameof(ManualTickMethod), new object[] {"Hello World!", 42},
				2.0f, 3.0f);
			Invoke(nameof(UnregisterManualAction), 10f);
			Invoke(nameof(UnregisteParamHandle), 10f);
		}

		private void UnregisterManualAction()
		{
			Debug.Log("Unregistering Manual Action at " + Time.time);
			TickManager.Unregister(manualActionHandle);
		}

		private void UnregisteParamHandle()
		{
			Debug.Log("Unregistering Param Action at " + Time.time);
			TickManager.Unregister(manualParamHandle);
		}

		[Tick(1.0f)]
		private void TickEverySecond()
		{
			Debug.Log($"[Tick] Every 1 second at {Time.time}");
		}

		[Tick(0.5f)]
		private void TickTwiceASecond()
		{
			Debug.Log($"[Tick] Every 0.5 second at {Time.time}");
		}

		[Tick(2.0f, 5.0f)]
		private void TickDelayedStart()
		{
			Debug.Log($"[Tick] Delayed 5s start, every 2s at {Time.time}");
		}

		private void ManualTickMethod(string message, int number)
		{
			Debug.Log($"[Manual Method] {message}, Number {number} at {Time.time}");
		}

		private void OnDestroy()
		{
			TickManager.Unregister(manualActionHandle);
			TickManager.Unregister(manualParamHandle);

			Debug.Log("ExampleTicker destroyed and manual ticks unregistered at " + Time.time);
		}
	}
}