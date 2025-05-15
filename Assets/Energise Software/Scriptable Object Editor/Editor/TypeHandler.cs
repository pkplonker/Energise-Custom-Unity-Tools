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
	internal static class TypeHandler
	{
		internal static MemoryStats MemoryStats { get; private set; } = null;
		internal static string[] TypeNames { get; private set; } = null;
		private static List<Assembly> AvailableAssemblies = new();
		internal static List<Type> ScriptableObjectTypes;
		internal static string[] AssemblyNames;
		internal static List<ScriptableObject> CurrentTypeObjectsOriginal = new();
		internal static List<ScriptableObject> CurrentTypeObjects = new();

		/// <summary>
		/// Loads all ScriptableObject assets of the specified type from the selected folder, tracks memory usage, and applies any name-based filtering.
		/// </summary>
		/// <param name="type">The ScriptableObject type to load.</param>
		/// <param name="selectionParams">Selection parameters (folder path, include derived, name filter).</param>
		internal static void LoadObjectsOfType(Type type, SelectionParams selectionParams)
		{
			MemoryStats ??= new MemoryStats();
			MemoryStats.memoryUsage.Clear();
			MemoryStats.totalMemoryAll = 0;
			CurrentTypeObjects.Clear();
			CurrentTypeObjectsOriginal.Clear();

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

			CurrentTypeObjectsOriginal = all;

			CurrentTypeObjects = string.IsNullOrEmpty(selectionParams.instanceSearchString)
				? new List<ScriptableObject>(all)
				: all.Where(o =>
					o.name.IndexOf(selectionParams.instanceSearchString, StringComparison.OrdinalIgnoreCase) >= 0
				).ToList();
		}

		/// <summary>
		/// Populates the list of available assemblies that contain ScriptableObject types in the selected folder.
		/// </summary>
		/// <param name="selectionParams">Selection parameters including the assets folder path.</param>
		internal static void LoadAvailableAssemblies(SelectionParams selectionParams)
		{
			AvailableAssemblies = GetAssembliesWithScriptableObjects(selectionParams);
			AssemblyNames = AvailableAssemblies
				.Select(assembly => assembly.GetName().Name)
				.Prepend("All Assemblies")
				.ToArray();
		}

		/// <summary>
		/// Builds the list of ScriptableObject types from the available assemblies, applying name filtering and include-derived settings.
		/// </summary>
		/// <param name="selectionParams">Selection parameters including assembly index, type name filter, and include-derived flag.</param>
		internal static void LoadScriptableObjectTypes(SelectionParams selectionParams)
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

		/// <summary>
		/// Retrieves a comparable value from a serialized property on a ScriptableObject for sorting purposes.
		/// </summary>
		/// <param name="o">The ScriptableObject instance.</param>
		/// <param name="path">The serialized property path.</param>
		/// <returns>An IComparable representing the property’s value.</returns>
		internal static IComparable GetPropertyValue(ScriptableObject o, string path)
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

		/// <summary>
		/// Prompts the user for a save location and creates one or more new instances of the specified ScriptableObject type as assets.
		/// </summary>
		/// <param name="count">Number of instances to create.</param>
		/// <param name="type">The ScriptableObject type.</param>
		/// <param name="defaultFolder">Default folder path for asset creation.</param>
		/// <param name="selectedTypeIndex">Index of the type in the type list (for validation).</param>
		internal static void CreateNewInstance(int count, Type type, string defaultFolder, int selectedTypeIndex)
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

		/// <summary>
		/// Loads assembly and type lists based on the provided selection parameters.
		/// </summary>
		/// <param name="selectionParams">Selection parameters including folder and assembly settings.</param>
		internal static void Load(SelectionParams selectionParams)
		{
			LoadAvailableAssemblies(selectionParams);
			LoadScriptableObjectTypes(selectionParams);
		}

		/// <summary>
		/// Determines whether the given ScriptableObject type has any assets in the specified folder.
		/// </summary>
		/// <param name="type">The ScriptableObject type to check.</param>
		/// <param name="selectionParams">Selection parameters including the assets folder path.</param>
		/// <returns>True if at least one asset of the type exists in the folder; otherwise false.</returns>
		private static bool IsInAssetsFolder(Type type, SelectionParams selectionParams) =>
			AssetDatabase.FindAssets($"t:{type.Name}", new[] {selectionParams.assetsFolderPath}).Any();

		/// <summary>
		/// Scans all loaded assemblies (excluding Unity’s) and returns those containing non-abstract ScriptableObject types with assets in the specified folder.
		/// </summary>
		/// <param name="selectionParams">Selection parameters including the assets folder path.</param>
		/// <returns>List of assemblies meeting the criteria.</returns>
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