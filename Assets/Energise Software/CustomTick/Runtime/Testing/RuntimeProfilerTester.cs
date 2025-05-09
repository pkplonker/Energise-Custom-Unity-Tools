using UnityEngine;

namespace CustomTick
{
	public class RuntimeProfilerTester : MonoBehaviour
	{
		[SerializeField] private int CountPerType = 500;
		private TickHandle[] actionHandles;
		private TickHandle[] methodHandles;

		private int actionTickCount;
		private int methodTickCount;
		private int attributeTickCount;

		private float logInterval = 5f;
		private float logTimer = 0f;

		private void Start()
		{
			actionHandles = new TickHandle[CountPerType];
			methodHandles = new TickHandle[CountPerType];

			Debug.Log($"[StressTester] Spawning {CountPerType * 3} total tick events...");

			for (int i = 0; i < CountPerType; i++)
			{
				int index = i;
				actionHandles[i] = TickBuilder.Create(() => actionTickCount++)
					.SetInterval(1.0f)
					.SetDelay(Random.Range(0f, 2f))
					.SetDescription($"ActionTick_{index}")
					.Register();
			}

			for (int i = 0; i < CountPerType; i++)
			{
				int index = i;
				methodHandles[i] = TickBuilder.Create(this, nameof(OnMethodTick), new object[] {index})
					.SetInterval(1.0f)
					.SetDelay(Random.Range(0f, 2f))
					.SetDescription($"MethodTick_{index}")
					.Register();
			}
		}

		private void OnMethodTick(int index)
		{
			methodTickCount++;
		}

		[Tick(1.0f, 1.5f)]
		private void AttributeTick()
		{
			attributeTickCount++;
		}

		private void Update()
		{
			logTimer += Time.deltaTime;
			if (logTimer >= logInterval)
			{
				Debug.Log(
					$"[StressTester] Action: {actionTickCount}, Method: {methodTickCount}, Attribute: {attributeTickCount} (after {Time.time:F1}s)");
				logTimer = 0f;
			}
		}

		private void OnDestroy()
		{
			foreach (var handle in actionHandles)
				TickManager.Unregister(handle);

			foreach (var handle in methodHandles)
				TickManager.Unregister(handle);

			Debug.Log("[StressTester] Unregistered all manual ticks.");
		}
	}
}