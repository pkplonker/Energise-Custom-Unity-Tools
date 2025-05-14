using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal class Column
	{
		internal enum ColumnType
		{
			BuiltIn,
			Property
		}

		internal ColumnType ColType;
		internal string Label;
		internal float Width;
		internal string PropertyPath;
		internal Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]> DrawAction;

		internal Column(ColumnType k, string label, float width, string path = null,
			Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]> drawAction =
				null)
		{
			ColType = k;
			Label = label;
			Width = width;
			PropertyPath = path;
			DrawAction = drawAction;
		}
	}
}