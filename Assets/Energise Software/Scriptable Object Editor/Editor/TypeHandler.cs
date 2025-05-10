using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace ScriptableObjectEditor
{
	public static class TypeHandler
	{
		public static MemoryStats memoryStats { get; private set; } = null;

		public static List<ScriptableObject> LoadObjectsOfType(Type type, FilterParams filterParams)
		{
			memoryStats ??= new MemoryStats();
			var currentTypeObjects = new List<ScriptableObject>();
			memoryStats.memoryUsage.Clear();
			memoryStats.totalMemoryAll = 0;
			memoryStats.totalMemoryFiltered = 0;
			if (type == null) return currentTypeObjects;

			var all = new List<ScriptableObject>();
			var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] {filterParams.assetsFolderPath});
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
				if (obj == null) continue;
				if ((filterParams.includeDerivedTypes && type.IsAssignableFrom(obj.GetType())) ||
				    (!filterParams.includeDerivedTypes && obj.GetType() == type))
				{
					all.Add(obj);
				}
			}

			foreach (var obj in all)
			{
				long mem = Profiler.GetRuntimeMemorySizeLong(obj);
				memoryStats.memoryUsage[obj] = mem;
				memoryStats.totalMemoryAll += mem;
			}

			currentTypeObjects = string.IsNullOrEmpty(filterParams.instanceSearchString)
				? all
				: all.Where(o =>
						o.name.IndexOf(filterParams.instanceSearchString, StringComparison.OrdinalIgnoreCase) >= 0)
					.ToList();

			foreach (var obj in currentTypeObjects)
				memoryStats.totalMemoryFiltered += memoryStats.memoryUsage[obj];
			return currentTypeObjects;
		}
	}

	public class MemoryStats
	{
		public Dictionary<ScriptableObject, long> memoryUsage = new();
		public long totalMemoryAll = 0;
		public long totalMemoryFiltered = 0;
	}

	public class FilterParams
	{
		public string instanceSearchString = string.Empty;
		public bool includeDerivedTypes = true;
		public string assetsFolderPath = "Assets";
	}
}