using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ScriptableObjectEditor
{
	public partial class ScriptableObjectEditorWindow : EditorWindow
	{
		[SerializeField]
		private int selectedTypeIndex;

		[SerializeField]
		private int selectedAssemblyIndex;

		private Vector2 scrollPosition;
		private int draggingColumn = -1;
		private float dragStartMouseX;
		private float dragStartWidth;
		private int sortColumnIndex = -1;
		private bool sortAscending = true;
		private Dictionary<string, bool> regions = new();
		private int createCount;
		private int reorderSourceColumn = -1;
		private bool isReordering = false;

		private List<Header> BuiltInHeaders = new()
		{
			new Header("Copy", 35),
			new Header("Delete", 50),
			new Header("Instance Name", 150),
		};

		private List<Column> columns = new();
		private static SelectionParams selectionParams;
		private HashSet<int> selectedRows = new();
		private int lastClickedRow = -1;
		private float cellPadding = 4;

		[MenuItem("Window/Energise Tools/Scriptable Object Editor &%S")]
		public static void ShowWindow() => GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");

		static ScriptableObjectEditorWindow() =>
			AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

		private static void OnAfterAssemblyReload()
		{
			var window = GetWindow<ScriptableObjectEditorWindow>();
			TypeHandler.Load(selectionParams);
			window.InitializeColumns();
			window.RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[window.selectedTypeIndex]);
			window.Repaint();
		}

		private void OnEnable()
		{
			InitializeColumns();
			var window = GetWindow<ScriptableObjectEditorWindow>();
			selectionParams = new SelectionParams
				{selectedTypeIndex = window.selectedTypeIndex, selectedAssemblyIndex = window.selectedAssemblyIndex};
			TypeHandler.Load(selectionParams);
			RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
		}

		private void InitializeColumns()
		{
			columns.Clear();
			columns.Add(new Column(Column.Kind.BuiltIn, "Copy", 35, null,
				(obj, toAdd, toRemove, opts) =>
				{
					if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus"), opts)) toAdd.Add(obj);
				}));
			columns.Add(new Column(Column.Kind.BuiltIn, "Delete", 50, null,
				(obj, toAdd, toRemove, opts) =>
				{
					if (GUILayout.Button(EditorGUIUtility.IconContent("d_TreeEditor.Trash"), opts)) toRemove.Add(obj);
				}));
			columns.Add(new Column(Column.Kind.BuiltIn, "Instance Name", 150, null,
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
		}

		private bool GetExpandedRegion(string key)
		{
			if (regions.ContainsKey(key)) return regions[key];
			regions.Add(key, false);
			return false;
		}

		private void RefreshObjectsOfType(Type type)
		{
			TypeHandler.LoadObjectsOfType(type, selectionParams);

			var propPaths = new List<string>();
			if (TypeHandler.CurrentTypeObjects.Any())
			{
				var firstSO = new SerializedObject(TypeHandler.CurrentTypeObjects[0]);
				propPaths = GetPropertyPaths(firstSO.GetIterator());
			}

			var builtInTemplate = columns.Where(c => c.kind == Column.Kind.BuiltIn).ToList();
			columns.Clear();

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
						var template = builtInTemplate.FirstOrDefault(t => t.label == label);
						var action = template != null
							? template.drawAction
							: GetBuiltInDrawAction(label);
						columns.Add(new Column(Column.Kind.BuiltIn, label, def.Width, null, action));
					}
					else
					{
						var path = entry.Substring(3);
						var wi = Mathf.Max(100, path.Length * 10);
						columns.Add(new Column(Column.Kind.Property, path, wi, path));
					}
				}
			}
			else
			{
				foreach (var h in BuiltInHeaders)
				{
					var template = builtInTemplate.FirstOrDefault(t => t.label == h.Label);
					var action = template != null
						? template.drawAction
						: GetBuiltInDrawAction(h.Label);
					columns.Add(new Column(Column.Kind.BuiltIn, h.Label, h.Width, null, action));
				}

				foreach (var path in propPaths)
				{
					columns.Add(new Column(Column.Kind.Property, path, Mathf.Max(100, path.Length * 10), path));
				}
			}

			var allProps = propPaths.ToHashSet();
			columns.RemoveAll(c => c.kind == Column.Kind.Property && !allProps.Contains(c.propertyPath));

			var existingProps = columns.Where(c => c.kind == Column.Kind.Property).Select(c => c.propertyPath)
				.ToHashSet();
			foreach (var p in propPaths)
			{
				if (!existingProps.Contains(p))
					columns.Add(new Column(Column.Kind.Property, p, Mathf.Max(100, p.Length * 10), p));
			}

			var w = SOEPrefs.LoadColumnWidthsForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes);
			if (w != null)
				for (int i = 0; i < w.Count && i < columns.Count; i++)
					columns[i].width = w[i];

			ApplySorting();
		}

		private Action<ScriptableObject, List<ScriptableObject>, List<ScriptableObject>, GUILayoutOption[]>
			GetBuiltInDrawAction(string label)
		{
			switch (label)
			{
				case "Copy":
					return (obj, toAdd, toRemove, opts) =>
					{
						if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus"), opts)) toAdd.Add(obj);
					};
				case "Delete":
					return (obj, toAdd, toRemove, opts) =>
					{
						if (GUILayout.Button(EditorGUIUtility.IconContent("d_TreeEditor.Trash"), opts))
							toRemove.Add(obj);
					};
				default:
					return (obj, toAdd, toRemove, opts) =>
					{
						EditorGUILayout.LabelField(obj.name, EditorStyles.textField, opts);
					};
			}
		}

		private void OnGUI()
		{
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

			using (new SOERegion(true))
			{
				EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);
				var expand = Fold("Assemblies", "Options for finding scriptable assets");
				if (expand)
				{
					using (new SOERegion())
					{
						EditorGUILayout.LabelField(new GUIContent("Path", "Where to search for scriptable assets"),
							GUILayout.Width(40));
						selectionParams.assetsFolderPath =
							EditorGUILayout.TextField(selectionParams.assetsFolderPath, GUILayout.Width(200));
						if (GUILayout.Button(new GUIContent("Browse", "Select folder to search for scriptable assets"),
							    GUILayout.Width(80)))
						{
							string sel = EditorUtility.OpenFolderPanel("Select Scriptable Object Folder",
								selectionParams.assetsFolderPath, "");
							if (!string.IsNullOrEmpty(sel))
							{
								selectionParams.assetsFolderPath = sel.Replace(Application.dataPath, "Assets");
								RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
							}
						}

						if (GUILayout.Button(EditorGUIUtility.IconContent("d_Refresh"), GUILayout.Width(35)))
						{
							TypeHandler.LoadAvailableAssemblies(selectionParams);
							RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
						}

						int newAsm = EditorGUILayout.Popup(selectedAssemblyIndex, TypeHandler.AssemblyNames,
							GUILayout.Width(200));
						if (newAsm != selectedAssemblyIndex)
						{
							selectedAssemblyIndex = newAsm;

							RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
						}

						TypeHandler.LoadScriptableObjectTypes(selectionParams);
					}
				}

				EditorGUILayout.Space();

				var expanded = Fold("Stats");
				if (expanded)
				{
					using (new SOERegion())
					{
						EditorGUILayout.LabelField($"Count: {TypeHandler.CurrentTypeObjects.Count}",
							GUILayout.Width(100));
						EditorGUILayout.LabelField(
							$"Total Memory: {TypeHandler.MemoryStats.totalMemoryAll / 1024f:F1} KB",
							GUILayout.Width(140));
						EditorGUILayout.LabelField(
							$"Filtered Memory: {TypeHandler.MemoryStats.totalMemoryFiltered / 1024f:F1} KB",
							GUILayout.Width(160));
						GUILayout.FlexibleSpace();
					}
				}

				using (new SOERegion())
				{
					EditorGUILayout.LabelField(new GUIContent("Type", "The currently selected scriptableobject type"),
						GUILayout.Width(40));
					int newTypeIdx =
						EditorGUILayout.Popup(selectedTypeIndex, TypeHandler.TypeNames, GUILayout.Width(200));
					if (newTypeIdx != selectedTypeIndex)
					{
						selectedTypeIndex = newTypeIdx;
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					}

					EditorGUI.BeginChangeCheck();
					bool inc = EditorGUILayout.ToggleLeft(
						new GUIContent("Include Derived", "Tick to include inherited/derived types in view"),
						selectionParams.includeDerivedTypes,
						GUILayout.Width(120));
					if (EditorGUI.EndChangeCheck() && inc != selectionParams.includeDerivedTypes)
					{
						selectionParams.includeDerivedTypes = inc;
						TypeHandler.LoadScriptableObjectTypes(selectionParams);
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					}

					EditorGUILayout.LabelField(new GUIContent("Filter Types", "Text filtering of types to display"),
						GUILayout.Width(80));

					var newTypeSearch =
						EditorGUILayout.TextField(selectionParams.typeSearchString, GUILayout.Width(200));
					if (newTypeSearch != selectionParams.typeSearchString)
					{
						selectionParams.typeSearchString = newTypeSearch;
						TypeHandler.LoadScriptableObjectTypes(selectionParams);
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
					}
				}

				using (new SOERegion())
				{
					EditorGUILayout.LabelField(
						new GUIContent("Filter Instances", "Only show instances matching the filter text"),
						GUILayout.Width(100));
					var newInstSearch =
						EditorGUILayout.TextField(selectionParams.instanceSearchString, GUILayout.Width(200));
					if (newInstSearch != selectionParams.instanceSearchString)
					{
						selectionParams.instanceSearchString = newInstSearch;
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					}
				}

				using (new SOERegion())
				{
					if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus"), GUILayout.Width(30),
						    GUILayout.Height(18)))
					{
						Type type = TypeHandler.ScriptableObjectTypes[selectedTypeIndex];
						TypeHandler.CreateNewInstance(createCount, type, selectionParams.assetsFolderPath,
							selectedTypeIndex);
						RefreshObjectsOfType(type);
					}

					createCount = EditorGUILayout.IntField(createCount, GUILayout.Width(40));
					createCount = Mathf.Max(1, createCount);
				}

				EditorGUILayout.Space();
				if (TypeHandler.CurrentTypeObjects.Any() && TypeHandler.TypeNames.Any())
				{
					DrawPropertiesGrid();
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private void ReorderColumn(int src, int dst)
		{
			var col = columns[src];
			columns.RemoveAt(src);
			columns.Insert(dst, col);
			SaveColumns();
			Repaint();
		}

		private void SaveColumns()
		{
			SOEPrefs.SaveColumnWidthsForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes,
				columns.Select(c => c.width).ToList());
			SOEPrefs.SaveColumnOrderForCurrentType(
				selectedTypeIndex,
				TypeHandler.ScriptableObjectTypes,
				columns
			);
		}

		private bool Fold(string key, string optionalTooltip = "")
		{
			var val = EditorGUILayout.Foldout(GetExpandedRegion(key), new GUIContent(key, optionalTooltip));
			regions[key] = val;
			return val;
		}

		private void ApplySorting()
		{
			if (sortColumnIndex < 0 || sortColumnIndex >= columns.Count) return;
			var col = columns[sortColumnIndex];
			if (col.kind == Column.Kind.BuiltIn)
			{
				if (col.label == "Instance Name")
				{
					TypeHandler.CurrentTypeObjects = sortAscending
						? TypeHandler.CurrentTypeObjects.OrderBy(o => o.name).ToList()
						: TypeHandler.CurrentTypeObjects.OrderByDescending(o => o.name).ToList();
				}
			}
			else if (col.kind == Column.Kind.Property)
			{
				string path = col.propertyPath;
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

		private int GetColumnIndexAtPosition(float mouseX)
		{
			float x = 0;
			for (int i = 0; i < columns.Count; i++)
			{
				if (mouseX >= x && mouseX <= x + columns[i].width)
					return i;
				x += columns[i].width;
			}

			return -1;
		}

		private void DrawHeaderCell(string label, float width, int columnIndex)
		{
			var content = new GUIContent(label + (sortColumnIndex == columnIndex ? (sortAscending ? " ▲" : " ▼") : ""));
			Rect cellRect = GUILayoutUtility.GetRect(content, EditorStyles.boldLabel, GUILayout.Width(width));
			GUI.Label(cellRect, content, EditorStyles.boldLabel);
			Rect handleRect = new Rect(cellRect.xMax - 4, cellRect.y, 8, cellRect.height);
			EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
			var e = Event.current;
			switch (e.rawType)
			{
				case EventType.MouseDown:
					if (handleRect.Contains(e.mousePosition))
					{
						draggingColumn = columnIndex;
						dragStartMouseX = e.mousePosition.x;
						dragStartWidth = columns[columnIndex].width;
						isReordering = false;
					}
					else if (cellRect.Contains(e.mousePosition))
					{
						if (sortColumnIndex == columnIndex)
							sortAscending = !sortAscending;
						else
						{
							sortColumnIndex = columnIndex;
							sortAscending = true;
						}

						ApplySorting();
						e.Use();
						reorderSourceColumn = columnIndex;
						isReordering = true;
					}

					break;
				case EventType.MouseDrag:
					if (draggingColumn == columnIndex)
					{
						columns[columnIndex].width =
							Mathf.Max(20, dragStartWidth + (e.mousePosition.x - dragStartMouseX));
						Repaint();
						e.Use();
					}
					else if (isReordering && reorderSourceColumn == columnIndex)
					{
						e.Use();
					}

					break;
				case EventType.MouseUp:
					if (draggingColumn == columnIndex)
					{
						draggingColumn = -1;
						SaveColumns();
						e.Use();
					}
					else if (isReordering && reorderSourceColumn >= 0)
					{
						int target = GetColumnIndexAtPosition(e.mousePosition.x);
						if (target >= 0 && target != reorderSourceColumn)
							ReorderColumn(reorderSourceColumn, target);
						isReordering = false;
						reorderSourceColumn = -1;
						e.Use();
					}

					break;
			}
		}

		private void DrawPropertiesGrid()
		{
			int totalCols = columns.Count;
			int dropIndex = -1;
			var e = Event.current;
			if (isReordering && e.rawType == EventType.Repaint)
				dropIndex = GetColumnIndexAtPosition(e.mousePosition.x);

			using (new SOERegion())
			{
				for (int i = 0; i < totalCols; i++)
				{
					DrawHeaderCell(columns[i].label, columns[i].width, i);
					if (i < columns.Count - 1)
						GUILayout.Space(cellPadding);
					if (i == dropIndex)
					{
						Rect hdrRect = GUILayoutUtility.GetLastRect();
						EditorGUI.DrawRect(hdrRect, new Color(0f, 0.5f, 1f, 0.3f));
					}
				}
			}

			var toAdd = new List<ScriptableObject>();
			var toRemove = new List<ScriptableObject>();
			int rowIndex = 0;

			foreach (var obj in TypeHandler.CurrentTypeObjects)
			{
				using (new SOERegion())
				{
					for (int i = 0; i < columns.Count; i++)
					{
						var col = columns[i];

						var opts = new[] {GUILayout.Width(col.width), GUILayout.Height(18)};

						if (col.kind == Column.Kind.BuiltIn)
						{
							col.drawAction(obj, toAdd, toRemove, opts);
						}
						else
						{
							var so = new SerializedObject(obj);
							var prop = so.FindProperty(col.propertyPath);
							if (prop == null)
							{
								GUILayout.Label(GUIContent.none, opts);
							}
							else
							{
								EditorGUI.BeginChangeCheck();
								switch (prop.propertyType)
								{
									case SerializedPropertyType.Float:
										float newF = EditorGUILayout.FloatField(prop.floatValue, opts);
										if (EditorGUI.EndChangeCheck())
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												var p = tgt.FindProperty(col.propertyPath);
												p.floatValue = newF;
												tgt.ApplyModifiedProperties();
											}

										break;
									case SerializedPropertyType.Integer:
										int newI = EditorGUILayout.IntField(prop.intValue, opts);
										if (EditorGUI.EndChangeCheck())
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												var p = tgt.FindProperty(col.propertyPath);
												p.intValue = newI;
												tgt.ApplyModifiedProperties();
											}

										break;
									case SerializedPropertyType.String:
										string newS = EditorGUILayout.TextField(prop.stringValue, opts);
										if (EditorGUI.EndChangeCheck())
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												var p = tgt.FindProperty(col.propertyPath);
												p.stringValue = newS;
												tgt.ApplyModifiedProperties();
											}

										break;
									case SerializedPropertyType.Color:
										Color newC = EditorGUILayout.ColorField(GUIContent.none, prop.colorValue, true,
											true, false, opts);
										if (EditorGUI.EndChangeCheck())
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												var p = tgt.FindProperty(col.propertyPath);
												p.colorValue = newC;
												tgt.ApplyModifiedProperties();
											}

										break;
									case SerializedPropertyType.ObjectReference:
									{
										UnityEngine.Object newObj =
											EditorGUILayout.ObjectField(
												prop.objectReferenceValue,
												typeof(UnityEngine.Object),
												allowSceneObjects: false,
												opts);
										if (EditorGUI.EndChangeCheck())
										{
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												var p = tgt.FindProperty(col.propertyPath);
												p.objectReferenceValue = newObj;
												tgt.ApplyModifiedProperties();
											}
										}
									}
										break;
									default:
										EditorGUILayout.PropertyField(prop, GUIContent.none, opts);
										if (EditorGUI.EndChangeCheck())
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												tgt.ApplyModifiedProperties();
											}

										break;
								}
							}
						}

						if (i < columns.Count - 1)
							GUILayout.Space(cellPadding);
					}
				}

				Rect rowRect = GUILayoutUtility.GetLastRect();
				bool isSelected = selectedRows.Contains(rowIndex);
				if (Event.current.type == EventType.Repaint && isSelected)
					EditorGUI.DrawRect(rowRect, new Color(0.2f, 0.5f, 1f, 0.1f));

				if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
				{
					if (Event.current.shift && lastClickedRow >= 0)
					{
						int start = Mathf.Min(lastClickedRow, rowIndex);
						int end = Mathf.Max(lastClickedRow, rowIndex);
						for (int i = start; i <= end; i++) selectedRows.Add(i);
					}
					else if (Event.current.control)
					{
						if (!selectedRows.Remove(rowIndex))
							selectedRows.Add(rowIndex);
					}
					else
					{
						selectedRows.Clear();
						selectedRows.Add(rowIndex);
					}

					lastClickedRow = rowIndex;
					Event.current.Use();
				}

				rowIndex++;
			}

			SOEIO.AddAssets(toAdd);
			SOEIO.RemoveAssets(toRemove);
		}

		private static List<string> GetPropertyPaths(SerializedProperty iter)
		{
			var paths = new List<string>();
			if (iter.NextVisible(true))
				do
				{
					paths.Add(iter.propertyPath);
				} while (iter.NextVisible(false));

			return paths;
		}
	}
}