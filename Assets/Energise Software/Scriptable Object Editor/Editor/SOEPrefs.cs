using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ScriptableObjectEditor
{
	internal static class SOEPrefs
	{
		private static string GetPrefsKeyForType(Type t) => $"SOEditor_ColumnWidths_{t.AssemblyQualifiedName}";

		internal static void SaveColumnWidthsForCurrentType(int selectedTypeIndex, List<Type> scriptableObjectTypes,
			List<float> columnWidths)
		{
			if (selectedTypeIndex < 0 || selectedTypeIndex >= scriptableObjectTypes.Count) return;
			string key = GetPrefsKeyForType(scriptableObjectTypes[selectedTypeIndex]);
			string data = string.Join(",", columnWidths.Select(w => w.ToString()));
			EditorPrefs.SetString(key, data);
		}
	}
}