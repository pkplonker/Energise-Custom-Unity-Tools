using UnityEditor;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal class RenderInfo
	{
		internal static void Render()
		{
			var infoStyle = new GUIStyle(EditorStyles.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				wordWrap = true
			};
			EditorGUILayout.Separator();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Scriptable Objects Editor", infoStyle);
			infoStyle.fontStyle = FontStyle.Normal;
			EditorGUILayout.LabelField(
				"Thanks for using our Scriptable Objects Editor.",
				infoStyle);
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField(
				"Any comments, requests, feedback, please email: EnergiseTools@gmail.com\n\n Thanks, Stuart Heath (Energise Software)",
				infoStyle);
		}
	}
}