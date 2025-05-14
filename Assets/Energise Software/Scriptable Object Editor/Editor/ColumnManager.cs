using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal class ColumnManager
	{
		public Rect[] LastHeaderCellRects { get; private set; }
		public List<Column> Columns { get; private set; } = new();
		private SearchField[] columnSearchFields;
		public string[] columnFilterStrings;
		private int reorderSourceColumn = -1;
		private int draggingColumn = -1;
		private int sortColumnIndex = -1;
		private bool sortAscending = true;
		private bool isReordering;
		private float dragStartMouseX;
		private float dragStartWidth;
		public int CurrentDropIndex { get; private set; } = -1;

		private List<Header> BuiltInHeaders = new()
		{
			new Header("Copy", 35),
			new Header("Delete", 50),
			new Header("Instance Name", 150),
		};

		internal int ColumnCount => Columns?.Count ?? 0;

		internal ColumnManager() { }

		internal void EnsureColumnFiltersAndRects()
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
			{
				LastHeaderCellRects = new Rect[count];
			}
		}

		internal void InitializeColumns()
		{
			Columns.Clear();
			Columns.Add(new Column(Column.ColumnType.BuiltIn, "Copy", 35, null,
				(obj, toAdd, toRemove, opts) =>
				{
					if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus", "|Copy this scriptable object"),
						    opts)) toAdd.Add(obj);
				}));
			Columns.Add(new Column(Column.ColumnType.BuiltIn, "Delete", 50, null,
				(obj, toAdd, toRemove, opts) =>
				{
					if (GUILayout.Button(
						    EditorGUIUtility.IconContent("d_TreeEditor.Trash", "|Delete this scriptable object"), opts))
						toRemove.Add(obj);
				}));
			Columns.Add(new Column(Column.ColumnType.BuiltIn, "Instance Name", 150, null,
				(obj, toAdd, toRemove, opts) =>
				{
					var path = AssetDatabase.GetAssetPath(obj);
					var oldName = obj.name;
					var newName = EditorGUILayout.TextField(oldName, opts);
					if (newName != oldName)
					{
						AssetDatabase.RenameAsset(path, newName);
						AssetDatabase.SaveAssets();
						obj.name = newName;
					}
				}
			));
			EnsureColumnFiltersAndRects();
		}

		internal void Refresh(int selectedTypeIndex, List<string> propPaths)
		{
			var builtInTemplate = Columns.Where(c => c.ColType == Column.ColumnType.BuiltIn).ToList();
			Columns.Clear();

			var orderList =
				SOEPrefs.LoadInterleavedColumnOrderForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes);
			if (orderList != null)
			{
				foreach (var entry in orderList)
				{
					if (entry.StartsWith("[H]"))
					{
						var label = entry.Substring(3);
						var def = BuiltInHeaders.FirstOrDefault(h => h.Label == label);
						if (def == null) continue;
						var template = builtInTemplate.FirstOrDefault(t => t.Label == label);
						var action = template != null
							? template.DrawAction
							: GetBuiltInDrawAction(label);
						Columns.Add(new Column(Column.ColumnType.BuiltIn, label, def.Width, null, action));
					}
					else
					{
						var path = entry.Substring(3);
						var wi = Mathf.Max(100, path.Length * 10);
						Columns.Add(new Column(Column.ColumnType.Property, path, wi, path));
					}
				}
			}
			else
			{
				foreach (var h in BuiltInHeaders)
				{
					var template = builtInTemplate.FirstOrDefault(t => t.Label == h.Label);
					var action = template != null
						? template.DrawAction
						: GetBuiltInDrawAction(h.Label);
					Columns.Add(new Column(Column.ColumnType.BuiltIn, h.Label, h.Width, null, action));
				}

				foreach (var path in propPaths)
				{
					Columns.Add(new Column(Column.ColumnType.Property, path, Mathf.Max(100, path.Length * 10), path));
				}
			}

			var allProps = propPaths.ToHashSet();
			Columns.RemoveAll(c => c.ColType == Column.ColumnType.Property && !allProps.Contains(c.PropertyPath));

			var existingProps = Columns.Where(c => c.ColType == Column.ColumnType.Property).Select(c => c.PropertyPath)
				.ToHashSet();
			foreach (var p in propPaths)
			{
				if (!existingProps.Contains(p))
					Columns.Add(new Column(Column.ColumnType.Property, p, Mathf.Max(100, p.Length * 10), p));
			}

			var w = SOEPrefs.LoadColumnWidthsForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes);
			if (w != null)
				for (int i = 0; i < w.Count && i < Columns.Count; i++)
					Columns[i].Width = w[i];
		}

		private Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]>
			GetBuiltInDrawAction(string label)
		{
			switch (label)
			{
				case "Copy":
					return (obj, toAdd, toRemove, opts) =>
					{
						if (GUILayout.Button(
							    EditorGUIUtility.IconContent("d_Toolbar Plus", "|Copy this scriptable object"), opts))
							toAdd.Add(obj);
					};
				case "Delete":
					return (obj, toAdd, toRemove, opts) =>
					{
						if (GUILayout.Button(
							    EditorGUIUtility.IconContent("d_TreeEditor.Trash", "|Delete this scriptable object"),
							    opts))
							toRemove.Add(obj);
					};
				default:
					return (obj, toAdd, toRemove, opts) =>
					{
						EditorGUILayout.LabelField(obj.name, EditorStyles.textField, opts);
					};
			}
		}

		internal void ClearFilterStrings()
		{
			for (int i = 0; i < columnFilterStrings.Length; i++)
			{
				columnFilterStrings[i] = string.Empty;
			}
		}

		private void ReorderColumn(int src, int dst, int selectedTypeIndex)
		{
			var col = Columns[src];
			Columns.RemoveAt(src);
			Columns.Insert(dst, col);
			SaveColumns(selectedTypeIndex);
		}

		private void SaveColumns(int selectedTypeIndex)
		{
			SOEPrefs.SaveColumnWidthsForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes,
				Columns.Select(c => c.Width).ToList());
			SOEPrefs.SaveColumnOrderForCurrentType(
				selectedTypeIndex,
				TypeHandler.ScriptableObjectTypes,
				Columns
			);
			LastHeaderCellRects = new Rect[Columns.Count];
		}

		private int GetColumnIndexAtPosition(float mouseX)
		{
			float x = 0;
			for (int i = 0; i < Columns.Count; i++)
			{
				if (mouseX >= x && mouseX <= x + Columns[i].Width)
					return i;
				x += Columns[i].Width;
			}

			return -1;
		}

		internal void ApplyAllFilters()
		{
			var filtered = new List<ScriptableObject>();
			foreach (var obj in TypeHandler.CurrentTypeObjectsOriginal)
			{
				bool keep = true;
				for (int i = 0; i < Columns.Count; i++)
				{
					var filter = columnFilterStrings[i];
					if (string.IsNullOrEmpty(filter)) continue;

					Column col = Columns[i];
					string cellText = col.ColType == Column.ColumnType.BuiltIn && col.Label == "Instance Name"
						? obj.name
						: $"{TypeHandler.GetPropertyValue(obj, col.PropertyPath)}";

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

		internal void ApplySorting()
		{
			if (sortColumnIndex < 0 || sortColumnIndex >= ColumnCount) return;
			var col = Columns[sortColumnIndex];
			if (col.ColType == Column.ColumnType.BuiltIn)
			{
				if (col.Label == "Instance Name")
				{
					TypeHandler.CurrentTypeObjects = sortAscending
						? TypeHandler.CurrentTypeObjects.OrderBy(o => o.name).ToList()
						: TypeHandler.CurrentTypeObjects.OrderByDescending(o => o.name).ToList();
				}
			}
			else if (col.ColType == Column.ColumnType.Property)
			{
				string path = col.PropertyPath;
				var first = new SerializedObject(TypeHandler.CurrentTypeObjects[0]);
				var prop = first.FindProperty(path);
				bool isColor = prop != null && prop.propertyType == SerializedPropertyType.Color;
				if (isColor)
				{
					TypeHandler.CurrentTypeObjects = sortAscending
						? TypeHandler.CurrentTypeObjects.OrderBy(o =>
						{
							var c = new SerializedObject(o).FindProperty(path).colorValue;
							return c.r + c.g + c.b + c.a;
						}).ToList()
						: TypeHandler.CurrentTypeObjects.OrderByDescending(o =>
						{
							var c = new SerializedObject(o).FindProperty(path).colorValue;
							return c.r + c.g + c.b + c.a;
						}).ToList();
				}
				else
				{
					TypeHandler.CurrentTypeObjects = sortAscending
						? TypeHandler.CurrentTypeObjects.OrderBy(o => TypeHandler.GetPropertyValue(o, path)).ToList()
						: TypeHandler.CurrentTypeObjects.OrderByDescending(o => TypeHandler.GetPropertyValue(o, path))
							.ToList();
				}
			}
		}

		internal void HandleColumnInput(
			int selectedTypeIndex,
			int columnIndex,
			Rect cellRect,
			Action repaintCallback
		)
		{
			const float hotWidth = 12f;
			Rect resizeHandle = new Rect(
				cellRect.xMax - hotWidth,
				cellRect.y,
				hotWidth,
				cellRect.height
			);
			EditorGUIUtility.AddCursorRect(resizeHandle, MouseCursor.ResizeHorizontal);

			float lineX = cellRect.xMax - 0.5f;
			Handles.BeginGUI();
			Handles.color = new Color(0, 0, 0, 0.2f);
			Handles.DrawLine(
				new Vector3(lineX, cellRect.y + 4f),
				new Vector3(lineX, cellRect.yMax - 4f)
			);
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

					break;
			}

			if (e.type == EventType.MouseDown && cellRect.Contains(e.mousePosition) &&
			    !resizeHandle.Contains(e.mousePosition))
			{
				reorderSourceColumn = columnIndex;
				dragStartMouseX = e.mousePosition.x;
				isReordering = false;
				e.Use();
			}

			if (e.type == EventType.MouseDrag && reorderSourceColumn == columnIndex)
			{
				if (!isReordering && Mathf.Abs(e.mousePosition.x - dragStartMouseX) > 5f)
					isReordering = true;

				if (isReordering)
				{
					CurrentDropIndex = GetColumnIndexAtPosition(e.mousePosition.x);
					repaintCallback();
				}

				e.Use();
			}

			if (e.type == EventType.MouseUp && isReordering && reorderSourceColumn == columnIndex)
			{
				int target = CurrentDropIndex;
				if (target >= 0 && target != reorderSourceColumn)
				{
					ReorderColumn(reorderSourceColumn, target, selectedTypeIndex);
				}

				isReordering = false;
				reorderSourceColumn = -1;
				CurrentDropIndex = -1;
				repaintCallback();
				e.Use();
			}

			LastHeaderCellRects[columnIndex] = cellRect;
		}

		internal int GetDropIndex()
		{
			var e = Event.current;
			if (isReordering && e.rawType == EventType.Repaint)
			{
				return GetColumnIndexAtPosition(e.mousePosition.x);
			}

			return -1;
		}

		internal string GetSortedLabel(int index) =>
			Columns[index].Label + (sortColumnIndex == index ? (sortAscending ? " ▲" : " ▼") : "");
	}
}