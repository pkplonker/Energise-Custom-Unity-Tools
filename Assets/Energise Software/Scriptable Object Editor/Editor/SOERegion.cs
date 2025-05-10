using System;
using UnityEditor;

namespace ScriptableObjectEditor
{
	public class SOERegion : IDisposable
	{
		private readonly bool isVertical;

		public SOERegion(bool b = false)
		{
			isVertical = b;
			if (isVertical) EditorGUILayout.BeginVertical();
			else EditorGUILayout.BeginHorizontal();
		}

		public void Dispose()
		{
			if (isVertical) EditorGUILayout.EndVertical();
			else EditorGUILayout.EndHorizontal();
		}
	}
}