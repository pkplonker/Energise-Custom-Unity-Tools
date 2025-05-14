using ScriptableObjectEditor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal class ColumnFilterPopup : PopupWindowContent
	{
		private readonly int columnIndex;
		private readonly ColumnManager owner;
		private SearchField searchField;
		private string filterText;

		internal ColumnFilterPopup(int columnIndex, ColumnManager owner)
		{
			this.columnIndex = columnIndex;
			this.owner = owner;
			searchField = new SearchField();
			filterText = owner.columnFilterStrings[columnIndex];
		}

		public override Vector2 GetWindowSize() => new Vector2(200, 60);

		public override void OnGUI(Rect rect)
		{
			GUILayout.Label($"Filter “{owner.Columns[columnIndex].Label}”", EditorStyles.boldLabel);

			Rect sfRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(18));
			filterText = searchField.OnGUI(sfRect, filterText);

			GUILayout.Space(4);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Apply"))
			{
				Commit();
				editorWindow.Close();
			}

			if (GUILayout.Button("Clear"))
			{
				filterText = string.Empty;
				Commit();
				editorWindow.Close();
			}

			EditorGUILayout.EndHorizontal();
		}

		private void Commit()
		{
			owner.columnFilterStrings[columnIndex] = filterText;
			owner.ApplyAllFilters();
		}
	}
}