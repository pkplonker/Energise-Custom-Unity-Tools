using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjectEditor
{
	public class Column
	{
		public enum Kind
		{
			BuiltIn,
			Property
		}

		public Kind kind;
		public string label;
		public float width;
		public string propertyPath;
		public Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]> drawAction;

		public Column(Kind k, string label, float width, string path = null,
			Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]> drawAction =
				null)
		{
			kind = k;
			this.label = label;
			this.width = width;
			propertyPath = path;
			this.drawAction = drawAction;
		}
	}
}