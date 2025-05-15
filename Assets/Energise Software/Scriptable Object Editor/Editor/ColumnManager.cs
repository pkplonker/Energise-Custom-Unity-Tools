using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ScriptableObjectEditor
{
	/// <summary>
	/// Manages the collection of columns: initialization, refresh (with saved prefs), filters, sorting, resizing, and reordering.
	/// </summary>
	internal class ColumnManager
	{
		public Rect[] LastHeaderCellRects { get; private set; }
		public List<Column> Columns { get; private set; } = new();
		public string[] columnFilterStrings;

		private SearchField[] columnSearchFields;
		private int reorderSourceColumn = -1;
		private int draggingColumn = -1;
		private bool isReordering;
		private float dragStartMouseX;
		private float dragStartWidth;
		private int currentDropIndex = -1;
		private int sortColumnIndex = -1;
		private bool sortAscending = true;

		public int CurrentDropIndex => currentDropIndex;
		public int ColumnCount => Columns.Count;

		/// <summary>
		/// Sets up the default built-in columns and prepares filter fields and header rects.
		/// </summary>
		public void InitializeColumns()
		{
			Columns.Clear();
			Columns.AddRange(BuiltInColumnFactory.CreateDefaults());
			EnsureColumnFiltersAndRects();
		}

		/// <summary>
		/// Ensures that the filter search fields and header rectangle arrays match the current column count.
		/// </summary>
		public void EnsureColumnFiltersAndRects()
		{
			int count = Columns.Count;
			if (columnSearchFields == null || columnSearchFields.Length != count)
			{
				columnSearchFields = new SearchField[count];
				columnFilterStrings = new string[count];
				for (int i = 0; i < count; i++)
				{
					columnSearchFields[i] = new SearchField();
					columnFilterStrings[i] = string.Empty;
				}
			}

			if (LastHeaderCellRects == null || LastHeaderCellRects.Length != count)
				LastHeaderCellRects = new Rect[count];
		}

		/// <summary>
		/// Builds the column list based on saved order and widths, then incorporates the provided property paths.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the current ScriptableObject type.</param>
		/// <param name="propPaths">List of serialized property paths to include as columns.</param>
		public void Refresh(int selectedTypeIndex, List<string> propPaths)
		{
			var builtInTemplate = BuiltInColumnFactory.CreateDefaults().ToList();
			Columns.Clear();

			var orderList = SOEPrefs.LoadInterleavedColumnOrderForCurrentType(
				selectedTypeIndex, TypeHandler.ScriptableObjectTypes);

			if (orderList != null)
			{
				foreach (var entry in orderList)
				{
					if (entry.StartsWith("[H]"))
					{
						string label = entry.Substring(3);
						var def = builtInTemplate.FirstOrDefault(h => h.Label == label);
						if (def != null) Columns.Add(def);
					}
					else
					{
						string path = entry.Substring(3);
						Columns.Add(CreatePropertyColumn(path));
					}
				}
			}
			else
			{
				Columns.AddRange(builtInTemplate);
				foreach (var path in propPaths)
					Columns.Add(CreatePropertyColumn(path));
			}

			SyncPropertyColumns(propPaths);
			ApplySavedWidths(selectedTypeIndex);
			EnsureColumnFiltersAndRects();
		}

		/// <summary>
		/// Clears all per-column filter strings back to empty.
		/// </summary>
		public void ClearFilterStrings()
		{
			if (columnFilterStrings == null) return;
			for (int i = 0; i < columnFilterStrings.Length; i++)
				columnFilterStrings[i] = string.Empty;
		}
		
		/// <summary>
		/// Filters the current object list by applying each column’s filter string.
		/// </summary>
		public void ApplyAllFilters()
		{
			var filtered = new List<ScriptableObject>();
			foreach (var obj in TypeHandler.CurrentTypeObjectsOriginal)
			{
				bool keep = true;
				for (int i = 0; i < Columns.Count; i++)
				{
					string filter = columnFilterStrings[i];
					if (string.IsNullOrEmpty(filter)) continue;

					var col = Columns[i];
					string cellText = col.ColType == Column.ColumnType.BuiltIn && col.Label == "Instance Name"
						? obj.name
						: TypeHandler.GetPropertyValue(obj, col.PropertyPath)?.ToString() ?? string.Empty;

					if (!cellText.Contains(filter, StringComparison.OrdinalIgnoreCase))
					{
						keep = false;
						break;
					}
				}

				if (keep) filtered.Add(obj);
			}

			TypeHandler.CurrentTypeObjects = filtered;
		}

		/// <summary>
		/// Sorts the current object list according to the active sort column and direction.
		/// </summary>
		public void ApplySorting()
		{
			if (sortColumnIndex < 0 || sortColumnIndex >= ColumnCount) return;
			var col = Columns[sortColumnIndex];
			if (col.ColType == Column.ColumnType.BuiltIn && col.Label == "Instance Name")
			{
				TypeHandler.CurrentTypeObjects = sortAscending
					? TypeHandler.CurrentTypeObjects.OrderBy(o => o.name).ToList()
					: TypeHandler.CurrentTypeObjects.OrderByDescending(o => o.name).ToList();
			}
			else if (col.ColType == Column.ColumnType.Property)
			{
				string path = col.PropertyPath;
				var first = TypeHandler.CurrentTypeObjects.FirstOrDefault();
				if (first != null)
				{
					var so = new SerializedObject(first);
					var prop = so.FindProperty(path);
					bool isColor = prop != null && prop.propertyType == SerializedPropertyType.Color;

					if (isColor)
					{
						Func<ScriptableObject, float> key = o =>
						{
							var c = new SerializedObject(o).FindProperty(path).colorValue;
							return c.r + c.g + c.b + c.a;
						};
						TypeHandler.CurrentTypeObjects = sortAscending
							? TypeHandler.CurrentTypeObjects.OrderBy(key).ToList()
							: TypeHandler.CurrentTypeObjects.OrderByDescending(key).ToList();
					}
					else
					{
						Func<ScriptableObject, object> key = o => TypeHandler.GetPropertyValue(o, path);
						TypeHandler.CurrentTypeObjects = sortAscending
							? TypeHandler.CurrentTypeObjects.OrderBy(key).ToList()
							: TypeHandler.CurrentTypeObjects.OrderByDescending(key).ToList();
					}
				}
			}
		}

		/// <summary>
		/// Returns the index of the column currently under a drag-reorder operation (or –1 if none).
		/// </summary>
		/// <returns>Current drop target column index, or –1.</returns>
		public int GetDropIndex()
		{
			var e = Event.current;
			return (isReordering && e.rawType == EventType.Repaint)
				? GetColumnIndexAt(e.mousePosition.x)
				: -1;
		}

		/// <summary>
		/// Handles mouse input for resizing or reordering a header cell, and triggers persistence on change.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the current ScriptableObject type.</param>
		/// <param name="columnIndex">Index of the column being interacted with.</param>
		/// <param name="cellRect">Screen rectangle of the header cell.</param>
		/// <param name="repaintCallback">Callback to request window repaint.</param>
		public void HandleColumnInput(
			int selectedTypeIndex,
			int columnIndex,
			Rect cellRect,
			Action repaintCallback)
		{
			const float hotWidth = 12f;
			Rect resizeHandle = new(
				cellRect.xMax - hotWidth,
				cellRect.y,
				hotWidth,
				cellRect.height);
			EditorGUIUtility.AddCursorRect(resizeHandle, MouseCursor.ResizeHorizontal);

			float lineX = cellRect.xMax - 0.5f;
			Handles.BeginGUI();
			Handles.color = new Color(0, 0, 0, 0.2f);
			Handles.DrawLine(new Vector3(lineX, cellRect.y + 4f), new Vector3(lineX, cellRect.yMax - 4f));
			Handles.EndGUI();

			int id = GUIUtility.GetControlID(FocusType.Passive, resizeHandle);
			var e = Event.current;

			switch (e.GetTypeForControl(id))
			{
				case EventType.MouseDown:
					if (resizeHandle.Contains(e.mousePosition))
					{
						GUIUtility.hotControl = id;
						draggingColumn = columnIndex;
						dragStartMouseX = e.mousePosition.x;
						dragStartWidth = Columns[columnIndex].Width;
						e.Use();
						return;
					}

					if (cellRect.Contains(e.mousePosition))
					{
						reorderSourceColumn = columnIndex;
						dragStartMouseX = e.mousePosition.x;
						isReordering = false;
						e.Use();
					}

					break;

				case EventType.MouseDrag:
					if (GUIUtility.hotControl == id && draggingColumn == columnIndex)
					{
						float delta = e.mousePosition.x - dragStartMouseX;
						Columns[columnIndex].Width = Mathf.Max(20f, dragStartWidth + delta);
						repaintCallback();
						e.Use();
						return;
					}

					if (reorderSourceColumn == columnIndex)
					{
						if (!isReordering && Mathf.Abs(e.mousePosition.x - dragStartMouseX) > 5f)
							isReordering = true;
						if (isReordering)
						{
							currentDropIndex = GetColumnIndexAt(e.mousePosition.x);
							repaintCallback();
						}

						e.Use();
					}

					break;

				case EventType.MouseUp:
					if (GUIUtility.hotControl == id && draggingColumn == columnIndex)
					{
						GUIUtility.hotControl = 0;
						draggingColumn = -1;
						SaveColumns(selectedTypeIndex);
						repaintCallback();
						e.Use();
						return;
					}

					if (isReordering && reorderSourceColumn == columnIndex)
					{
						if (currentDropIndex >= 0 && currentDropIndex != reorderSourceColumn)
							ReorderColumn(reorderSourceColumn, currentDropIndex, selectedTypeIndex);
						isReordering = false;
						reorderSourceColumn = -1;
						currentDropIndex = -1;
						repaintCallback();
						e.Use();
					}

					break;
			}

			LastHeaderCellRects[columnIndex] = cellRect;
		}

		/// <summary>
		/// Returns the column label augmented with a sort indicator arrow if it is the active sort column.
		/// </summary>
		/// <param name="index">Index of the column.</param>
		/// <returns>Label text plus icon if sorted, otherwise just the label.</returns>
		public string GetSortedLabel(int index)
			=> Columns[index].Label + (sortColumnIndex == index ? (sortAscending ? " ▲" : " ▼") : "");

		/// <summary>
		/// Creates a new property-type column for the given serialized property path.
		/// </summary>
		/// <param name="path">Serialized property path to display.</param>
		/// <returns>A configured property column instance.</returns>
		private Column CreatePropertyColumn(string path)
			=> new(
				Column.ColumnType.Property,
				path,
				Mathf.Max(100, path.Length * 10),
				path,
				(obj, toAdd, toRemove, opts) =>
				{
					EditorGUILayout.LabelField(
						TypeHandler.GetPropertyValue(obj, path)?.ToString() ?? string.Empty,
						opts);
				});

		/// <summary>
		/// Synchronizes the set of property columns to exactly match the supplied property paths.
		/// </summary>
		/// <param name="props">Enumerable of property paths that should remain as columns.</param>
		private void SyncPropertyColumns(IEnumerable<string> props)
		{
			var propSet = new HashSet<string>(props);
			Columns.RemoveAll(c => c.ColType == Column.ColumnType.Property && !propSet.Contains(c.PropertyPath));
			var exist = new HashSet<string>(Columns
				.Where(c => c.ColType == Column.ColumnType.Property)
				.Select(c => c.PropertyPath));
			foreach (var p in props)
				if (!exist.Contains(p))
					Columns.Add(CreatePropertyColumn(p));
		}

		/// <summary>
		/// Loads and applies saved column width values for the current ScriptableObject type.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the current ScriptableObject type.</param>
		private void ApplySavedWidths(int selectedTypeIndex)
		{
			var w = SOEPrefs.LoadColumnWidthsForCurrentType(
				selectedTypeIndex, TypeHandler.ScriptableObjectTypes);
			if (w == null) return;
			for (int i = 0; i < w.Count && i < Columns.Count; i++)
				Columns[i].Width = w[i];
		}

		/// <summary>
		/// Moves a column from the source index to the destination index and saves the new order.
		/// </summary>
		/// <param name="src">Original column index.</param>
		/// <param name="dst">New target column index.</param>
		/// <param name="selectedTypeIndex">Index of the current ScriptableObject type.</param>
		private void ReorderColumn(int src, int dst, int selectedTypeIndex)
		{
			var col = Columns[src];
			Columns.RemoveAt(src);
			Columns.Insert(dst, col);
			SaveColumns(selectedTypeIndex);
		}

		/// <summary>
		/// Persists the current column widths and order for the current ScriptableObject type.
		/// </summary>
		/// <param name="selectedTypeIndex">Index of the current ScriptableObject type.</param>
		private void SaveColumns(int selectedTypeIndex)
		{
			SOEPrefs.SaveColumnWidthsForCurrentType(
				selectedTypeIndex,
				TypeHandler.ScriptableObjectTypes,
				Columns.Select(c => c.Width).ToList());
			SOEPrefs.SaveColumnOrderForCurrentType(
				selectedTypeIndex,
				TypeHandler.ScriptableObjectTypes,
				Columns);
			LastHeaderCellRects = new Rect[Columns.Count];
		}

		/// <summary>
		/// Determines which column index lies at the given horizontal mouse position.
		/// </summary>
		/// <param name="mouseX">X-coordinate of the mouse in header space.</param>
		/// <returns>Column index under the position, or –1 if none.</returns>
		private int GetColumnIndexAt(float mouseX)
		{
			float x = 0;
			for (int i = 0; i < Columns.Count; i++)
			{
				if (mouseX >= x && mouseX <= x + Columns[i].Width) return i;
				x += Columns[i].Width;
			}

			return -1;
		}
	}
}