using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ScriptableObjectEditor
{
	internal static class SOEPrefs
	{
		private static string GetPrefsKeyForType(Type t) => $"SOEditor_ColumnWidths_{t.AssemblyQualifiedName}";
		private static string GetOrderKeyForType(Type t) => $"SOEditor_ColumnOrder_{t.AssemblyQualifiedName}";

		/// <summary>
		/// Persist the column widths for this SO type.
		/// </summary>
		internal static void SaveColumnWidthsForCurrentType(int selectedTypeIndex, List<Type> scriptableObjectTypes,
			List<float> columnWidths)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count) return;
			string key = GetPrefsKeyForType(scriptableObjectTypes[selectedTypeIndex]);
			string data = string.Join(",", columnWidths.Select(w => w.ToString()));
			EditorPrefs.SetString(key, data);
		}

		/// <summary>
		/// Load the column widths for this SO type, or null if none saved.
		/// </summary>
		internal static List<float> LoadColumnWidthsForCurrentType(int selectedTypeIndex,
			List<Type> scriptableObjectTypes)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count) return null;
			string key = GetPrefsKeyForType(scriptableObjectTypes[selectedTypeIndex]);
			if (!EditorPrefs.HasKey(key)) return null;
			string data = EditorPrefs.GetString(key);
			var parts = data.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
			var widths = new List<float>(parts.Length);
			foreach (var p in parts)
			{
				if (float.TryParse(p, out float f)) widths.Add(f);
			}

			return widths;
		}

		/// <summary>
		/// Returns the saved interleaved “[H]…”/“[P]…” column order for the given SO type,
		/// or null if nothing has been saved yet.
		/// </summary>
		internal static List<string> LoadInterleavedColumnOrderForCurrentType(int selectedTypeIndex,
			List<Type> scriptableObjectTypes)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count)
				return null;
			string key = $"SOEditor_ColumnOrder_{scriptableObjectTypes[selectedTypeIndex].AssemblyQualifiedName}";
			if (!EditorPrefs.HasKey(key))
				return null;
			string json = EditorPrefs.GetString(key);
			var wrapper = JsonUtility.FromJson<SerializationWrapper>(json);
			return wrapper?.items;
		}

		/// <summary>
		/// Persist the column order (default headers + property paths) for this SO type.
		/// </summary>
		internal static void SaveColumnOrderForCurrentType(int selectedTypeIndex, List<Type> scriptableObjectTypes,
			List<Column> columns)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count) return;
			string key = GetOrderKeyForType(scriptableObjectTypes[selectedTypeIndex]);

			var items = columns.Select(c =>
				c.kind == Column.Kind.BuiltIn
					? "[H]" + c.label
					: "[P]" + c.propertyPath
			).ToList();

			var wrapper = new SerializationWrapper {items = items};
			string json = JsonUtility.ToJson(wrapper);
			EditorPrefs.SetString(key, json);
		}
	}

	[Serializable]
	internal class SerializationWrapper
	{
		public List<string> items;
	}
}