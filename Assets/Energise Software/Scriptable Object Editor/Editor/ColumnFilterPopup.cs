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

		/// <summary>
		/// Initializes a filter popup for the specified column on the given ColumnManager.
		/// </summary>
		/// <param name="columnIndex">Index of the column to filter.</param>
		/// <param name="owner">The ColumnManager instance that owns this popup.</param>
		internal ColumnFilterPopup(int columnIndex, ColumnManager owner)
		{
			this.columnIndex = columnIndex;
			this.owner = owner;
			searchField = new SearchField();
			filterText = owner.columnFilterStrings[columnIndex];
		}

		/// <summary>
		/// Returns the fixed size of this filter popup window.
		/// </summary>
		/// <returns>The width and height of the popup.</returns>
		public override Vector2 GetWindowSize() => new Vector2(200, 60);

		/// <summary>
		/// Draws the popup’s GUI: the label, search field, and Apply/Clear buttons.
		/// </summary>
		/// <param name="rect">The rectangle area of the popup window.</param>
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

		/// <summary>
		/// Commits the current filter text back to the ColumnManager and applies filtering.
		/// </summary>
		private void Commit()
		{
			owner.columnFilterStrings[columnIndex] = filterText;
			owner.ApplyAllFilters();
		}
	}
}