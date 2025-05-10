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

		[MenuItem("Window/Energise Tools/Scriptable Object Editor &%S")]
		public static void ShowWindow() => GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");

		static ScriptableObjectEditorWindow()
		{
			AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
		}

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
					EditorGUILayout.LabelField(obj.name, EditorStyles.textField, opts);
				}));
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

			columns.Clear();
			var orderList =
				SOEPrefs.LoadInterleavedColumnOrderForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes);
			if (orderList != null)
			{
				foreach (var entry in orderList)
				{
					if (entry.StartsWith("[H]"))
					{
						string label = entry.Substring(3);
						var def = BuiltInHeaders.FirstOrDefault(h => h.Label == label);
						if (def == null) continue;
						var action = GetBuiltInDrawAction(def.Label);
						columns.Add(new Column(Column.Kind.BuiltIn, def.Label, def.Width, null, action));
					}
					else
					{
						string path = entry.Substring(3);
						float wi = Mathf.Max(100, path.Length * 10);
						columns.Add(new Column(Column.Kind.Property, path, wi, path));
					}
				}
			}
			else
			{
				foreach (var h in BuiltInHeaders)
				{
					var action = GetBuiltInDrawAction(h.Label);
					columns.Add(new Column(Column.Kind.BuiltIn, h.Label, h.Width, null, action));
				}

				foreach (var path in propPaths)
					columns.Add(new Column(Column.Kind.Property, path, Mathf.Max(100, path.Length * 10), path));
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

					selectionParams.includeDerivedTypes =
						EditorGUILayout.ToggleLeft(
							new GUIContent("Include Derived", "Tick to include inherited/derived types in view"),
							selectionParams.includeDerivedTypes,
							GUILayout.Width(120));
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
			if (sortColumnIndex < 0) return;
			if (sortColumnIndex == 1)
			{
				TypeHandler.CurrentTypeObjects = (sortAscending
					? TypeHandler.CurrentTypeObjects.OrderBy(o => o.name)
					: TypeHandler.CurrentTypeObjects.OrderByDescending(o => o.name)).ToList();
			}
			else if (sortColumnIndex >= 2)
			{
				var first = new SerializedObject(TypeHandler.CurrentTypeObjects[0]);
				var iter = first.GetIterator();
				var paths = new List<string>();
				if (iter.NextVisible(true))
					do
					{
						paths.Add(iter.propertyPath);
					} while (iter.NextVisible(false));

				if (sortColumnIndex == 2)
				{
					TypeHandler.CurrentTypeObjects = (sortAscending
						? TypeHandler.CurrentTypeObjects.OrderBy(o => o.name)
						: TypeHandler.CurrentTypeObjects.OrderByDescending(o => o.name)).ToList();
				}
				else if (sortColumnIndex >= 3)
				{
					int propIndex = sortColumnIndex - 3;
					if (propIndex < paths.Count)
					{
						string path = paths[propIndex];
						var so = new SerializedObject(TypeHandler.CurrentTypeObjects[0]);
						var prop = so.FindProperty(path);
						if (prop.propertyType == SerializedPropertyType.Color)
						{
							TypeHandler.CurrentTypeObjects = (sortAscending
								? TypeHandler.CurrentTypeObjects.OrderBy(o =>
								{
									var c = new SerializedObject(o).FindProperty(path).colorValue;
									return c.r + c.g + c.b + c.a;
								})
								: TypeHandler.CurrentTypeObjects.OrderByDescending(o =>
								{
									var c = new SerializedObject(o).FindProperty(path).colorValue;
									return c.r + c.g + c.b + c.a;
								})).ToList();
						}
						else
						{
							TypeHandler.CurrentTypeObjects = (sortAscending
									? TypeHandler.CurrentTypeObjects.OrderBy(o => TypeHandler.GetPropertyValue(o, path))
									: TypeHandler.CurrentTypeObjects.OrderByDescending(o =>
										TypeHandler.GetPropertyValue(o, path)))
								.ToList();
						}
					}
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
						if (target >= 0 && target != reorderSourceColumn) ReorderColumn(reorderSourceColumn, target);
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
			using (new SOERegion())
			{
				for (int i = 0; i < totalCols; i++)
					DrawHeaderCell(columns[i].label, columns[i].width, i);
			}

			var toAdd = new List<ScriptableObject>();
			var toRemove = new List<ScriptableObject>();
			foreach (var obj in TypeHandler.CurrentTypeObjects)
			{
				var so = new SerializedObject(obj);
				using (new SOERegion())
				{
					for (int c = 0; c < columns.Count; c++)
					{
						var col = columns[c];
						if (col.kind == Column.Kind.BuiltIn)
						{
							col.drawAction(obj, toAdd, toRemove,
								new[] {GUILayout.Width(col.width), GUILayout.Height(18)});
						}
						else
						{
							var prop = so.FindProperty(col.propertyPath);
							if (prop == null)
							{
								GUILayout.Label(GUIContent.none, GUILayout.Width(col.width));
								continue;
							}

							var field = obj.GetType().GetField(prop.name,
								BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							var rangeAttr = field?.GetCustomAttribute<RangeAttribute>(true);
							var minAttr = field?.GetCustomAttribute<MinAttribute>(true);
							var textAreaAttr = field?.GetCustomAttribute<TextAreaAttribute>(true);
							var colorUsageAttr = field?.GetCustomAttribute<ColorUsageAttribute>(true);
							var opts = new[] {GUILayout.Width(col.width)};
							switch (prop.propertyType)
							{
								case SerializedPropertyType.Float when rangeAttr != null:
									prop.floatValue = EditorGUILayout.Slider(prop.floatValue, rangeAttr.min,
										rangeAttr.max, opts);
									break;
								case SerializedPropertyType.Integer when rangeAttr != null:
									prop.intValue = EditorGUILayout.IntSlider(prop.intValue, (int) rangeAttr.min,
										(int) rangeAttr.max, opts);
									break;
								case SerializedPropertyType.Float when minAttr != null:
									prop.floatValue = Mathf.Max(minAttr.min,
										EditorGUILayout.FloatField(prop.floatValue, opts));
									break;
								case SerializedPropertyType.Integer when minAttr != null:
									prop.intValue = Mathf.Max((int) minAttr.min,
										EditorGUILayout.IntField(prop.intValue, opts));
									break;
								case SerializedPropertyType.String when textAreaAttr != null:
									string s = EditorGUILayout.TextArea(prop.stringValue, opts);
									prop.stringValue = s;
									break;
								case SerializedPropertyType.Color:
									prop.colorValue = EditorGUILayout.ColorField(GUIContent.none, prop.colorValue, true,
										colorUsageAttr?.showAlpha ?? true, colorUsageAttr?.hdr ?? false, opts);
									break;
								default:
									EditorGUILayout.PropertyField(prop, GUIContent.none, opts);
									break;
							}
						}
					}
				}

				so.ApplyModifiedProperties();
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

		private List<string> GetPropertyPathList()
		{
			return columns.Where(c => c.kind == Column.Kind.Property).Select(c => c.propertyPath).ToList();
		}
	}
}