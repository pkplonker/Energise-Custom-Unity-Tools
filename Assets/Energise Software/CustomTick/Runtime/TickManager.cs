using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

namespace CustomTick
{
	[DefaultExecutionOrder(-10000)]
	internal static class TickManager
	{
		private static Dictionary<float, TickGroup> tickGroups = new();
#if UNITY_EDITOR
		private static readonly Dictionary<int, TickType> tickIdToType = new();
		private static readonly Dictionary<int, string> tickDescriptions = new();
#endif
		private static bool initialized = false;
		private static int nextId = 1;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Initialize()
		{
			if (initialized) return;
			initialized = true;

			ScanScene();
			HookPlayerLoop();
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RescanAll();

		private static void RescanAll() => ScanScene();

		private static void ScanScene()
		{
			MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

			foreach (var behaviour in behaviours)
			{
				if (behaviour == null) continue;

				var methods = behaviour.GetType()
					.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				foreach (var method in methods)
				{
					var tickAttr = method.GetCustomAttribute<TickAttribute>();
					if (tickAttr != null)
					{
						if (method.GetParameters().Length == 0)
						{
							int id = nextId++;
							var tickItem = new TickMethod(
								id,
								behaviour,
								method,
								tickAttr.Interval,
								tickAttr.Delay,
								oneShot: false,
								paused: false
							);

							if (!tickGroups.TryGetValue(tickAttr.Interval, out var group))
							{
								group = new TickGroup();
								tickGroups.Add(tickAttr.Interval, group);
							}
#if UNITY_EDITOR
							tickIdToType[id] = TickType.Method;
							tickDescriptions[id] = $"{behaviour.GetType().Name}.{method.Name}";
#endif
							group.Items.Add(tickItem);
						}
						else
						{
							Debug.LogWarning(
								$"[Tick] method '{method.Name}' on '{behaviour.name}' must have no parameters.");
						}
					}
				}
			}
		}

		private static void HookPlayerLoop()
		{
			var loop = PlayerLoop.GetCurrentPlayerLoop();
			InsertCustomUpdate(ref loop);
			PlayerLoop.SetPlayerLoop(loop);
		}

		private static void InsertCustomUpdate(ref PlayerLoopSystem loop)
		{
			var updateSystem = new PlayerLoopSystem
			{
				type = typeof(TickManager),
				updateDelegate = Update
			};

			var subsystems = new List<PlayerLoopSystem>(loop.subSystemList);
			subsystems.Insert(0, updateSystem);
			loop.subSystemList = subsystems.ToArray();
		}

		private static void Update()
		{
			float deltaTime = Time.deltaTime;

			groupsToRemove.Clear();

			foreach (var pair in tickGroups)
			{
				var group = pair.Value;
				var items = group.Items;

				for (int i = items.Count - 1; i >= 0; i--)
				{
					var item = items[i];

					if (item.ShouldTick(deltaTime))
					{
						item.Execute();

						if (item.IsOneShot())
						{
							items.RemoveAt(i);
						}
					}
					else if (!item.IsValid())
					{
						items.RemoveAt(i);
					}
				}

				if (items.Count == 0)
				{
					groupsToRemove.Add(pair.Key);
				}
			}

			for (int i = 0; i < groupsToRemove.Count; i++)
			{
				tickGroups.Remove(groupsToRemove[i]);
			}
		}

		private static readonly List<float> groupsToRemove = new();

		public static TickHandle Register(Action callback, float interval, float delay = 0f, bool oneShot = false,
			bool paused = false
#if UNITY_EDITOR
			, string description = null
#endif
		)
		{
			if (callback == null || interval <= 0f) return default;

			int id = nextId++;
			var tickItem = new TickAction(id, callback, interval, delay, oneShot, paused);

			if (!tickGroups.TryGetValue(interval, out var group))
				tickGroups[interval] = group = new TickGroup();

			group.Items.Add(tickItem);

#if UNITY_EDITOR
			tickIdToType[id] = TickType.Action;
			if (!string.IsNullOrEmpty(description))
				tickDescriptions[id] = description;
#endif

			return new TickHandle {Id = id};
		}

		public static TickHandle Register(MonoBehaviour target, string methodName, object[] parameters, float interval,
			float delay = 0f, bool oneShot = false, bool paused = false
#if UNITY_EDITOR
			, string description = null
#endif
		)
		{
			if (target == null || string.IsNullOrEmpty(methodName) || interval <= 0f) return default;

			var method = target.GetType().GetMethod(methodName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				Debug.LogWarning($"TickManager: Method '{methodName}' not found on '{target.name}'.");
				return default;
			}

			int id = nextId++;
			var tickItem = new TickMethodWithParams(id, target, method, interval, parameters, delay, oneShot, paused);

			if (!tickGroups.TryGetValue(interval, out var group))
				tickGroups[interval] = group = new TickGroup();

			group.Items.Add(tickItem);

#if UNITY_EDITOR
			tickIdToType[id] = TickType.MethodWithParams;
			if (!string.IsNullOrEmpty(description))
				tickDescriptions[id] = description;
#endif

			return new TickHandle {Id = id};
		}

		public static void Unregister(TickHandle handle)
		{
			if (!handle.IsValid)
				return;

			foreach (var group in tickGroups.Values)
			{
				var items = group.Items;
				for (int i = items.Count - 1; i >= 0; i--)
				{
					if (items[i].GetId() == handle.Id)
					{
						items.RemoveAt(i);
						return;
					}
				}
			}
		}

		public static void Pause(TickHandle handle)
		{
			if (!handle.IsValid)
				return;

			foreach (var group in tickGroups.Values)
			{
				var items = group.Items;
				for (int i = 0; i < items.Count; i++)
				{
					if (items[i].GetId() == handle.Id)
					{
						items[i].SetPaused(true);
						return;
					}
				}
			}
		}

		public static void Resume(TickHandle handle)
		{
			if (!handle.IsValid)
				return;

			foreach (var group in tickGroups.Values)
			{
				var items = group.Items;
				for (int i = 0; i < items.Count; i++)
				{
					if (items[i].GetId() == handle.Id)
					{
						items[i].SetPaused(false);
						return;
					}
				}
			}
		}

#if UNITY_EDITOR
		public static bool EditorTryGetType(int id, out TickType type) => tickIdToType.TryGetValue(id, out type);

		public static IReadOnlyDictionary<float, TickGroup> EditorGetGroups() => tickGroups;

		public static bool EditorTryGetDescription(int id, out string desc) =>
			tickDescriptions.TryGetValue(id, out desc);

		public static void EditorReset()
		{
			tickGroups.Clear();
			tickIdToType.Clear();
			tickDescriptions.Clear();
			nextId = 1;
		}

#endif
	}
}