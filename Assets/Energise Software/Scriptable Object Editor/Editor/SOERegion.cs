using System;
using UnityEditor;

namespace ScriptableObjectEditor
{
	internal class SOERegion : IDisposable
	{
		private readonly bool isVertical;

		/// <summary>
		/// Begins a scoped EditorGUILayout region: vertical if <paramref name="vertical"/> is true, horizontal otherwise.
		/// </summary>
		/// <param name="vertical">Determines whether to begin a vertical (true) or horizontal (false) layout group.</param>
		internal SOERegion(bool b = false)
		{
			isVertical = b;
			if (isVertical) EditorGUILayout.BeginVertical();
			else EditorGUILayout.BeginHorizontal();
		}

		/// <summary>
		/// Ends the EditorGUILayout region started by the constructor (vertical or horizontal).
		/// </summary>
		public void Dispose()
		{
			if (isVertical) EditorGUILayout.EndVertical();
			else EditorGUILayout.EndHorizontal();
		}
	}
}