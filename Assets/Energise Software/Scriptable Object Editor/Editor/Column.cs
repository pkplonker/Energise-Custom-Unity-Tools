using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjectEditor
{
	/// <summary>
	/// Encapsulates a column: its type, label, width, property path (if any), and draw action.
	/// </summary>
	internal class Column
	{
		public enum ColumnType { BuiltIn, Property }

		public ColumnType ColType { get; }
		public string Label { get; }
		public float Width { get; set; }
		public string PropertyPath { get; }
		public Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]> DrawAction { get; }

		public Column(ColumnType type, string label, float width, string propertyPath,
			Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]> draw)
		{
			ColType = type;
			Label = label;
			Width = width;
			PropertyPath = propertyPath;
			DrawAction = draw;
		}
	}
}