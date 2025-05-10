using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace ScriptableObjectEditor
{
	public static class TypeHandler
	{
		public static MemoryStats MemoryStats { get; private set; } = null;
		public static string[] TypeNames { get; private set; } = null;
		private static List<Assembly> AvailableAssemblies = new();
		public static List<ScriptableObject> CurrentTypeObjects = new();
		public static List<Type> ScriptableObjectTypes;
		public static string[] AssemblyNames;

		public static void LoadObjectsOfType(Type type, SelectionParams selectionParams)
		{
			MemoryStats ??= new MemoryStats();
			CurrentTypeObjects.Clear();
			MemoryStats.memoryUsage.Clear();
			MemoryStats.totalMemoryAll = 0;
			MemoryStats.totalMemoryFiltered = 0;
			if (type == null) return;

			var all = new List<ScriptableObject>();
			var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] {selectionParams.assetsFolderPath});
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
				if (obj == null) continue;
				if ((selectionParams.includeDerivedTypes && type.IsAssignableFrom(obj.GetType())) ||
				    (!selectionParams.includeDerivedTypes && obj.GetType() == type))
				{
					all.Add(obj);
				}
			}

			foreach (var obj in all)
			{
				long mem = Profiler.GetRuntimeMemorySizeLong(obj);
				MemoryStats.memoryUsage[obj] = mem;
				MemoryStats.totalMemoryAll += mem;
			}

			CurrentTypeObjects = string.IsNullOrEmpty(selectionParams.instanceSearchString)
				? all
				: all.Where(o =>
						o.name.IndexOf(selectionParams.instanceSearchString, StringComparison.OrdinalIgnoreCase) >= 0)
					.ToList();

			foreach (var obj in CurrentTypeObjects)
				MemoryStats.totalMemoryFiltered += MemoryStats.memoryUsage[obj];
		}

		public static void LoadAvailableAssemblies(SelectionParams selectionParams)
		{
			AvailableAssemblies = GetAssembliesWithScriptableObjects(selectionParams);
			AssemblyNames = AvailableAssemblies
				.Select(assembly => assembly.GetName().Name)
				.Prepend("All Assemblies")
				.ToArray();
		}

		public static void LoadScriptableObjectTypes(SelectionParams selectionParams)
		{
			IEnumerable<Type> types = selectionParams.selectedAssemblyIndex == 0
				? AvailableAssemblies.SelectMany(a => a.GetTypes())
				: AvailableAssemblies[selectionParams.selectedAssemblyIndex - 1].GetTypes();

			ScriptableObjectTypes = types
				.Where(t => t.IsSubclassOf(typeof(ScriptableObject)) && !t.IsAbstract &&
				            IsInAssetsFolder(t, selectionParams))
				.OrderBy(t => t.Name)
				.ToList();

			if (!string.IsNullOrEmpty(selectionParams.typeSearchString))
			{
				ScriptableObjectTypes = ScriptableObjectTypes
					.Where(t => t.Name.IndexOf(selectionParams.typeSearchString, StringComparison.OrdinalIgnoreCase) >=
					            0)
					.ToList();
			}

			TypeNames = ScriptableObjectTypes.Select(t => t.Name).ToArray();
			selectionParams.selectedTypeIndex = Mathf.Clamp(selectionParams.selectedTypeIndex, 0, TypeNames.Length - 1);
		}

		public static IComparable GetPropertyValue(ScriptableObject o, string path)
		{
			var so = new SerializedObject(o);
			var prop = so.FindProperty(path);
			switch (prop.propertyType)
			{
				case SerializedPropertyType.Integer: return prop.intValue;
				case SerializedPropertyType.Boolean: return prop.boolValue;
				case SerializedPropertyType.Float: return prop.floatValue;
				case SerializedPropertyType.String: return prop.stringValue;
				case SerializedPropertyType.ObjectReference:
					return prop.objectReferenceValue?.name ?? string.Empty;
				default:
					return string.Empty;
			}
		}

		public static void CreateNewInstance(int count, Type type, string defaultFolder, int selectedTypeIndex)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= ScriptableObjectTypes.Count) return;

			if (CurrentTypeObjects.Count > 0)
			{
				string firstPath = AssetDatabase.GetAssetPath(CurrentTypeObjects[0]);
				string dir = Path.GetDirectoryName(firstPath);
				if (!string.IsNullOrEmpty(dir))
					defaultFolder = dir;
			}

			string basePath = EditorUtility.SaveFilePanelInProject(
				$"Create New {type.Name}",
				type.Name + ".asset",
				"asset",
				"Specify location to save new asset",
				defaultFolder);
			if (string.IsNullOrEmpty(basePath)) return;

			for (int i = 0; i < count; i++)
			{
				string uniquePath = (i == 0)
					? basePath
					: AssetDatabase.GenerateUniqueAssetPath(basePath);

				var instance = ScriptableObject.CreateInstance(type);
				AssetDatabase.CreateAsset(instance, uniquePath);
			}

			AssetDatabase.SaveAssets();
		}

		public static void Load(SelectionParams selectionParams)
		{
			LoadAvailableAssemblies(selectionParams);
			LoadScriptableObjectTypes(selectionParams);
		}

		private static bool IsInAssetsFolder(Type type, SelectionParams selectionParams) =>
			AssetDatabase.FindAssets($"t:{type.Name}", new[] {selectionParams.assetsFolderPath}).Any();

		private static List<Assembly> GetAssembliesWithScriptableObjects(SelectionParams selectionParams)
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly =>
					!assembly.FullName.StartsWith("UnityEngine") &&
					!assembly.FullName.StartsWith("UnityEditor") &&
					!assembly.FullName.StartsWith("Unity."))
				.Where(assembly =>
					assembly.GetTypes().Any(type =>
						type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract &&
						IsInAssetsFolder(type, selectionParams)))
				.OrderBy(assembly => assembly.GetName().Name)
				.ToList();
		}
	}
}