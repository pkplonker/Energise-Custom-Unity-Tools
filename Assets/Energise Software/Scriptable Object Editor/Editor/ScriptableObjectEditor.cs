using UnityEditor;
using UnityEngine;
using System;
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
		private List<float> columnWidths = new();
		private int sortColumnIndex = -1;
		private bool sortAscending = true;
		private Dictionary<string, bool> regions = new();
		private int createCount;
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

			window.RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[window.selectedTypeIndex]);
			window.Repaint();
		}

		private void OnEnable()
		{
			var window = GetWindow<ScriptableObjectEditorWindow>();

			selectionParams = new SelectionParams
			{
				selectedTypeIndex = window.selectedTypeIndex,
				selectedAssemblyIndex = window.selectedAssemblyIndex
			};
			TypeHandler.Load(selectionParams);
			RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
		}

		private bool GetExpandedRegion(string key)
		{
			if (regions.ContainsKey(key))
			{
				return regions[key];
			}

			regions.Add(key, false);
			return false;
		}

		private void RefreshObjectsOfType(Type type)
		{
			TypeHandler.LoadObjectsOfType(type, selectionParams);
			ApplySorting();
		}

		private void OnGUI()
		{
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

			using (new SOERegion(true))
			{
				EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);
				var expand = Fold("Assemblies");
				if (expand)
				{
					using (new SOERegion())
					{
						EditorGUILayout.LabelField("Path", GUILayout.Width(40));
						selectionParams.assetsFolderPath =
							EditorGUILayout.TextField(selectionParams.assetsFolderPath, GUILayout.Width(200));
						if (GUILayout.Button("Browse", GUILayout.Width(80)))
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
					EditorGUILayout.LabelField("Type", GUILayout.Width(40));
					int newTypeIdx =
						EditorGUILayout.Popup(selectedTypeIndex, TypeHandler.TypeNames, GUILayout.Width(200));
					if (newTypeIdx != selectedTypeIndex)
					{
						selectedTypeIndex = newTypeIdx;
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					}

					selectionParams.includeDerivedTypes =
						EditorGUILayout.ToggleLeft("Include Derived", selectionParams.includeDerivedTypes,
							GUILayout.Width(120));
					EditorGUILayout.LabelField("Filter Types", GUILayout.Width(80));

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
					EditorGUILayout.LabelField("Filter Instances", GUILayout.Width(100));
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

		private bool Fold(string key)
		{
			var val = EditorGUILayout.Foldout(GetExpandedRegion(key), key);
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

		private void DrawHeaderCell(string label, float width, int columnIndex)
		{
			var content = new GUIContent(label + (sortColumnIndex == columnIndex ? (sortAscending ? " ▲" : " ▼") : ""));
			Rect cellRect = GUILayoutUtility.GetRect(content, EditorStyles.boldLabel, GUILayout.Width(width));
			GUI.Label(cellRect, content, EditorStyles.boldLabel);
			Rect handleRect = new Rect(cellRect.xMax - 4, cellRect.y, 8, cellRect.height);
			EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
			var e = Event.current;
			if (e.type == EventType.MouseDown)
			{
				if (handleRect.Contains(e.mousePosition))
				{
					draggingColumn = columnIndex;
					dragStartMouseX = e.mousePosition.x;
					dragStartWidth = columnWidths[columnIndex];
					e.Use();
				}
				else if (cellRect.Contains(e.mousePosition))
				{
					if (sortColumnIndex == columnIndex) sortAscending = !sortAscending;
					else
					{
						sortColumnIndex = columnIndex;
						sortAscending = true;
					}

					RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					e.Use();
				}
			}
			else if (e.type == EventType.MouseDrag && draggingColumn == columnIndex)
			{
				columnWidths[columnIndex] = Mathf.Max(20, dragStartWidth + (e.mousePosition.x - dragStartMouseX));
				Repaint();
				e.Use();
			}
			else if (e.type == EventType.MouseUp && draggingColumn == columnIndex)
			{
				draggingColumn = -1;
				SOEPrefs.SaveColumnWidthsForCurrentType(selectedTypeIndex, TypeHandler.ScriptableObjectTypes,
					columnWidths);
				e.Use();
			}
		}

		private void DrawPropertiesGrid()
		{
			if (!TypeHandler.CurrentTypeObjects.Any()) return;

			var firstSO = new SerializedObject(TypeHandler.CurrentTypeObjects[0]);
			var iter = firstSO.GetIterator();
			var propertyPaths = new List<string>();
			if (iter.NextVisible(true))
				do
				{
					propertyPaths.Add(iter.propertyPath);
				} while (iter.NextVisible(false));

			int totalCols = 3 + propertyPaths.Count;
			if (columnWidths.Count != totalCols)
			{
				columnWidths.Clear();
				columnWidths.Add(35);
				columnWidths.Add(35);
				columnWidths.Add(150);
				foreach (var path in propertyPaths)
					columnWidths.Add(Mathf.Max(100, path.Length * 10));
			}

			using (new SOERegion())
			{
				DrawHeaderCell("Actions", columnWidths[0], 0);
				DrawHeaderCell("Delete", columnWidths[1], 1);
				DrawHeaderCell("Instance Name", columnWidths[2], 2);
				for (int i = 0; i < propertyPaths.Count; i++)
					DrawHeaderCell(propertyPaths[i], columnWidths[i + 3], i + 3);
			}

			var toAdd = new List<ScriptableObject>();
			var toRemove = new List<ScriptableObject>();

			foreach (var obj in TypeHandler.CurrentTypeObjects)
			{
				var so = new SerializedObject(obj);
				using (new SOERegion())
				{
					if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus"),
						    GUILayout.Width(columnWidths[0]),
						    GUILayout.Height(18)))
						toAdd.Add(obj);
					if (GUILayout.Button(EditorGUIUtility.IconContent("d_TreeEditor.Trash"),
						    GUILayout.Width(columnWidths[1]), GUILayout.Height(18)))
						toRemove.Add(obj);
					EditorGUILayout.LabelField(obj.name, EditorStyles.textField, GUILayout.Width(columnWidths[2]));

					for (int i = 0; i < propertyPaths.Count; i++)
					{
						var path = propertyPaths[i];
						var prop = so.FindProperty(path);
						if (prop == null) continue;

						FieldInfo field = obj.GetType()
							.GetField(prop.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						var rangeAttr = field?.GetCustomAttribute<RangeAttribute>(true);
						var minAttr = field?.GetCustomAttribute<MinAttribute>(true);
						var textAreaAttr = field?.GetCustomAttribute<TextAreaAttribute>(true);
						var colorUsageAttr = field?.GetCustomAttribute<ColorUsageAttribute>(true);

						GUILayoutOption widthOpt = GUILayout.Width(columnWidths[i + 3]);

						switch (prop.propertyType)
						{
							case SerializedPropertyType.Float when rangeAttr != null:
								prop.floatValue = EditorGUILayout.Slider(prop.floatValue, rangeAttr.min, rangeAttr.max,
									widthOpt);
								break;
							case SerializedPropertyType.Integer when rangeAttr != null:
								prop.intValue = EditorGUILayout.IntSlider(prop.intValue, (int) rangeAttr.min,
									(int) rangeAttr.max, widthOpt);
								break;
							case SerializedPropertyType.Float when minAttr != null:
								prop.floatValue = Mathf.Max(minAttr.min,
									EditorGUILayout.FloatField(prop.floatValue, widthOpt));
								break;
							case SerializedPropertyType.Integer when minAttr != null:
								prop.intValue = Mathf.Max((int) minAttr.min,
									EditorGUILayout.IntField(prop.intValue, widthOpt));
								break;
							case SerializedPropertyType.String when textAreaAttr != null:
								string str = prop.stringValue;
								str = EditorGUILayout.TextArea(str, GUILayout.Height(textAreaAttr.minLines * 14),
									GUILayout.Width(columnWidths[i + 3]));
								prop.stringValue = str;
								break;
							case SerializedPropertyType.Color:
								bool showAlpha = colorUsageAttr?.showAlpha ?? true;
								bool hdr = colorUsageAttr?.hdr ?? false;
								prop.colorValue = EditorGUILayout.ColorField(GUIContent.none, prop.colorValue, true,
									showAlpha, hdr, widthOpt);
								break;
							default:
								EditorGUILayout.PropertyField(prop, GUIContent.none, widthOpt);
								break;
						}
					}
				}

				so.ApplyModifiedProperties();
			}

			SOEIO.AddAssets(toAdd);
			SOEIO.RemoveAssets(toRemove);
		}
	}
}