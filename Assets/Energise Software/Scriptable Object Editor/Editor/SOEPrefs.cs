using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ScriptableObjectEditor
{
	internal static class SOEPrefs
	{
		/// <summary>
		/// Builds the EditorPrefs key for storing column widths of the given ScriptableObject type.
		/// </summary>
		/// <param name="t">The ScriptableObject type.</param>
		/// <returns>Prefs key string.</returns>
		private static string GetPrefsKeyForType(Type t) => $"SOEditor_ColumnWidths_{t.AssemblyQualifiedName}";

		/// <summary>
		/// Builds the EditorPrefs key for storing column order of the given ScriptableObject type.
		/// </summary>
		/// <param name="t">The ScriptableObject type.</param>
		/// <returns>Prefs key string.</returns>
		private static string GetOrderKeyForType(Type t) => $"SOEditor_ColumnOrder_{t.AssemblyQualifiedName}";

		/// <summary>
		/// Persists the column widths for the currently selected ScriptableObject type.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the selected type in the type list.</param>
		/// <param name="scriptableObjectTypes">List of available ScriptableObject types.</param>
		/// <param name="columnWidths">List of column width values to save.</param>
		internal static void SaveColumnWidthsForCurrentType(int selectedTypeIndex, List<Type> scriptableObjectTypes,
			List<float> columnWidths)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count) return;
			string key = GetPrefsKeyForType(scriptableObjectTypes[selectedTypeIndex]);
			string data = string.Join(",", columnWidths.Select(w => w.ToString()));
			EditorPrefs.SetString(key, data);
		}

		/// <summary>
		/// Loads the saved column widths for the currently selected ScriptableObject type, or returns null if none exist.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the selected type in the type list.</param>
		/// <param name="scriptableObjectTypes">List of available ScriptableObject types.</param>
		/// <returns>List of saved column widths, or null.</returns>
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
		/// Loads the saved interleaved column order (headers and properties) for the currently selected ScriptableObject type.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the selected type in the type list.</param>
		/// <param name="scriptableObjectTypes">List of available ScriptableObject types.</param>
		/// <returns>List of order entries (with “[H]”/“[P]” prefixes), or null if none saved.</returns>
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
		/// Persists the interleaved column order (headers and property paths) for the currently selected ScriptableObject type.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the selected type in the type list.</param>
		/// <param name="scriptableObjectTypes">List of available ScriptableObject types.</param>
		/// <param name="columns">List of Column objects defining current order.</param>
		internal static void SaveColumnOrderForCurrentType(int selectedTypeIndex, List<Type> scriptableObjectTypes,
			List<Column> columns)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count) return;
			string key = GetOrderKeyForType(scriptableObjectTypes[selectedTypeIndex]);

			var items = columns.Select(c =>
				c.ColType == Column.ColumnType.BuiltIn
					? "[H]" + c.Label
					: "[P]" + c.PropertyPath
			).ToList();

			var wrapper = new SerializationWrapper {items = items};
			string json = JsonUtility.ToJson(wrapper);
			EditorPrefs.SetString(key, json);
		}

		/// <summary>
		/// Saves a generic preference value (int, float, bool, string, enum, or serializable) under the given key.
		/// </summary>
		/// <typeparam name="T">Type of the preference value.</typeparam>
		/// <param name="tabChoiceKey">Base key name for the preference.</param>
		/// <param name="tabChoice">Value to save.</param>
		internal static void Save<T>(string tabChoiceKey, T tabChoice)
		{
			string prefsKey = $"SOEditor_{tabChoiceKey}";
			Type t = typeof(T);

			if (t == typeof(int))
			{
				EditorPrefs.SetInt(prefsKey, (int) (object) tabChoice);
			}
			else if (t == typeof(float))
			{
				EditorPrefs.SetFloat(prefsKey, (float) (object) tabChoice);
			}
			else if (t == typeof(bool))
			{
				EditorPrefs.SetBool(prefsKey, (bool) (object) tabChoice);
			}
			else if (t == typeof(string))
			{
				EditorPrefs.SetString(prefsKey, (string) (object) tabChoice);
			}
			else if (t.IsEnum)
			{
				EditorPrefs.SetString(prefsKey, tabChoice.ToString());
			}
			else
			{
				string json = JsonUtility.ToJson(tabChoice);
				EditorPrefs.SetString(prefsKey, json);
			}
		}

		/// <summary>
		/// Loads a generic preference value (int, float, bool, string, enum, or serializable) from the given key, or returns a default.
		/// </summary>
		/// <typeparam name="T">Type of the preference value.</typeparam>
		/// <param name="tabChoiceKey">Base key name for the preference.</param>
		/// <param name="defaultValue">Value to return if no preference exists.</param>
		/// <returns>Loaded preference value or default.</returns>
		internal static T Load<T>(string tabChoiceKey, T defaultValue = default)
		{
			string prefsKey = $"SOEditor_{tabChoiceKey}";
			Type t = typeof(T);

			if (!EditorPrefs.HasKey(prefsKey))
				return defaultValue;

			if (t == typeof(int))
			{
				int stored = EditorPrefs.GetInt(prefsKey, (int) (object) defaultValue);
				return (T) (object) stored;
			}

			if (t == typeof(float))
			{
				float stored = EditorPrefs.GetFloat(prefsKey, (float) (object) defaultValue);
				return (T) (object) stored;
			}

			if (t == typeof(bool))
			{
				bool stored = EditorPrefs.GetBool(prefsKey, (bool) (object) defaultValue);
				return (T) (object) stored;
			}

			if (t == typeof(string))
			{
				string stored = EditorPrefs.GetString(prefsKey, (string) (object) defaultValue);
				return (T) (object) stored;
			}

			if (t.IsEnum)
			{
				string enumString = EditorPrefs.GetString(prefsKey, defaultValue?.ToString());
				try
				{
					object parsed = Enum.Parse(t, enumString);
					return (T) parsed;
				}
				catch
				{
					return defaultValue;
				}
			}

			string json = EditorPrefs.GetString(prefsKey, null);
			if (string.IsNullOrEmpty(json))
				return defaultValue;
			try
			{
				return JsonUtility.FromJson<T>(json);
			}
			catch
			{
				return defaultValue;
			}
		}
	}

	[Serializable]
	internal class SerializationWrapper
	{
		internal List<string> items;
	}
}