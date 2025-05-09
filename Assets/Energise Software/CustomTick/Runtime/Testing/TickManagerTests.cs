using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace CustomTick.Tests
{
	public class TickManagerTests
	{
		private int actionTickCounter;
		private int methodTickCounter;
		private GameObject testObject;
		private TickTester tester;

		[SetUp]
		public void SetUp()
		{
			actionTickCounter = 0;
			methodTickCounter = 0;
			testObject = new GameObject("TickTester");
			tester = testObject.AddComponent<TickTester>();
		}

		[TearDown]
		public void TearDown()
		{
			if (testObject != null)
			{
				Object.Destroy(testObject);
			}

			TickManager.EditorReset();
		}

		private IEnumerator AdvanceTime(float seconds)
		{
			float elapsed = 0f;
			while (elapsed < seconds)
			{
				elapsed += Time.deltaTime;
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator RegisterActionTick_FiresCorrectly()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0.1f);
			yield return AdvanceTime(0.2f);
			Assert.GreaterOrEqual(actionTickCounter, 1);
		}

		[UnityTest]
		public IEnumerator RegisterMethodTick_FiresCorrectly()
		{
			var handle = TickBuilder.Create(tester, nameof(TickTester.OnTick))
				.SetInterval(0.1f)
				.Register();

			yield return AdvanceTime(0.2f);

			Assert.GreaterOrEqual(tester.TickCount, 1);
		}

		[UnityTest]
		public IEnumerator UnregisterActionTick_DoesNotFire()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0.1f);
			TickManager.Unregister(handle);

			yield return AdvanceTime(0.2f);

			Assert.AreEqual(0, actionTickCounter);
		}

		[UnityTest]
		public IEnumerator PauseAndResume_ActionTick_Works()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0.1f);
			TickManager.Pause(handle);

			yield return AdvanceTime(0.2f);
			Assert.AreEqual(0, actionTickCounter);

			TickManager.Resume(handle);
			yield return AdvanceTime(0.2f);
			Assert.GreaterOrEqual(actionTickCounter, 1);
		}

		[UnityTest]
		public IEnumerator OneShotTick_UnregistersItself()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0.1f, oneShot: true);

			yield return AdvanceTime(0.5f);

			Assert.AreEqual(1, actionTickCounter);
		}

		[UnityTest]
		public IEnumerator TickWithDelay_WaitsBeforeFiring()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0.1f, delay: 0.3f);

			yield return AdvanceTime(0.2f);
			Assert.AreEqual(0, actionTickCounter);

			yield return AdvanceTime(0.2f);
			Assert.GreaterOrEqual(actionTickCounter, 1);
		}

		[UnityTest]
		public IEnumerator RegisterNullAction_ReturnsDefaultHandle()
		{
			var handle = TickManager.Register(null, 0.1f);
			Assert.IsFalse(handle.IsValid);
			yield return null;
		}

		[UnityTest]
		public IEnumerator RegisterWithZeroInterval_ReturnsDefaultHandle()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0f);
			Assert.IsFalse(handle.IsValid);
			yield return null;
		}

		[UnityTest]
		public IEnumerator RegisterMethod_InvalidName_ReturnsDefaultHandle()
		{
			var handle = TickBuilder.Create(tester, "NonExistentMethod")
				.SetInterval(0.1f)
				.Register();

			Assert.IsFalse(handle.IsValid);
			yield return null;
		}

		[UnityTest]
		public IEnumerator PauseInvalidHandle_DoesNothing()
		{
			var invalidHandle = new TickHandle {Id = 0};
			Assert.DoesNotThrow(() => TickManager.Pause(invalidHandle));
			yield return null;
		}

		[UnityTest]
		public IEnumerator ResumeInvalidHandle_DoesNothing()
		{
			var invalidHandle = new TickHandle {Id = 0};
			Assert.DoesNotThrow(() => TickManager.Resume(invalidHandle));
			yield return null;
		}

		[UnityTest]
		public IEnumerator UnregisterInvalidHandle_DoesNothing()
		{
			var invalidHandle = new TickHandle {Id = 0};
			Assert.DoesNotThrow(() => TickManager.Unregister(invalidHandle));
			yield return null;
		}

		[UnityTest]
		public IEnumerator UnregisterWhilePaused_TickNeverFires()
		{
			var handle = TickManager.Register(() => actionTickCounter++, 0.1f);
			TickManager.Pause(handle);
			TickManager.Unregister(handle);

			yield return AdvanceTime(0.5f);

			Assert.AreEqual(0, actionTickCounter);
		}

		[UnityTest]
		public IEnumerator TickHandle_Pause_StopsTicking()
		{
			var handle = TickBuilder.Create(() => actionTickCounter++)
				.SetInterval(0.1f)
				.Register();

			handle.Pause();

			yield return AdvanceTime(0.3f);

			Assert.AreEqual(0, actionTickCounter);
		}

		[UnityTest]
		public IEnumerator TickHandle_Resume_AllowsTicking()
		{
			var handle = TickBuilder.Create(() => actionTickCounter++)
				.SetInterval(0.1f)
				.SetPaused(true)
				.Register();

			yield return AdvanceTime(0.3f);
			Assert.AreEqual(0, actionTickCounter);

			handle.Resume();

			yield return AdvanceTime(0.3f);
			Assert.GreaterOrEqual(actionTickCounter, 1);
		}

		[UnityTest]
		public IEnumerator TickHandle_Unregister_StopsTickImmediately()
		{
			var handle = TickBuilder.Create(() => actionTickCounter++)
				.SetInterval(0.1f)
				.Register();

			yield return AdvanceTime(0.2f);

			Assert.GreaterOrEqual(actionTickCounter, 1);

			handle.Unregister();
			int countAfterUnregister = actionTickCounter;

			yield return AdvanceTime(0.3f);

			Assert.AreEqual(countAfterUnregister, actionTickCounter);
		}

		[UnityTest]
		public IEnumerator TickHandle_InvalidHandle_CallsAreSafe()
		{
			var invalidHandle = new TickHandle();

			Assert.DoesNotThrow(() => invalidHandle.Pause());
			Assert.DoesNotThrow(() => invalidHandle.Resume());
			Assert.DoesNotThrow(() => invalidHandle.Unregister());

			yield return null;
		}
	}

	public class TickTester : MonoBehaviour
	{
		public int TickCount { get; private set; }

		private void Awake()
		{
			TickCount = 0;
		}

		internal void OnTick()
		{
			TickCount++;
		}
	}
}