using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;

namespace ScriptableObjectEditor
{
	public partial class ScriptableObjectEditorWindow : EditorWindow
	{
		[SerializeField]
		private int selectedTypeIndex;

		[SerializeField]
		private int selectedAssemblyIndex;

		private Vector2 scrollPosition;

		private Dictionary<string, bool> regions = new();
		private int createCount;

		private static SelectionParams selectionParams;
		private HashSet<int> selectedRows = new();
		private int lastClickedRow = -1;
		private float cellPadding = 4;
		private int tabChoice;
		private List<Tab> tabs;

		private ColumnManager columnManager = new();

		[MenuItem("Window/Energise Tools/Scriptable Object Editor &%S")]
		public static void ShowWindow() => GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");

		static ScriptableObjectEditorWindow() =>
			AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

		private static void OnAfterAssemblyReload()
		{
			var window = GetWindow<ScriptableObjectEditorWindow>();
			TypeHandler.Load(selectionParams);
			window.Initalise();
			window.RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[window.selectedTypeIndex]);
			window.Repaint();
		}

		private void Initalise()
		{
			columnManager.InitializeColumns();
			var window = GetWindow<ScriptableObjectEditorWindow>();
			selectionParams = new SelectionParams
				{selectedTypeIndex = window.selectedTypeIndex, selectedAssemblyIndex = window.selectedAssemblyIndex};
			TypeHandler.Load(selectionParams);
			RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
			tabs = new List<Tab>()
			{
				new("Hide", () => { }),
				new("Asset Management", DrawAssetManagement),
				new("Stats", DrawStats),
				new("Info", RenderInfo.Render),
			};
			tabChoice = SOEPrefs.Load("tabChoice", 0);
		}

		private struct Tab
		{
			public string Label;
			public Action Draw;

			public Tab(string label, Action draw)
			{
				Label = label;
				Draw = draw;
			}
		}

		private void OnEnable()
		{
			Initalise();
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

			columnManager.Refresh(selectedTypeIndex, propPaths);

			columnManager.ApplySorting();
		}

		private void OnGUI()
		{
			using (new SOERegion(true))
			{
				string[] labels = tabs.Select(t => t.Label).ToArray();
				tabChoice = GUILayout.Toolbar(tabChoice, labels, GUILayout.Height(16));
				SOEPrefs.Save("tabChoice", tabChoice);
				tabs[tabChoice].Draw();

				EditorGUILayout.Space();

				using (new SOERegion())
				{
					if (GUILayout.Button(EditorGUIUtility.IconContent(
							    "d_Toolbar Plus",
							    "|Add new instances"
						    ), GUILayout.Width(30),
						    GUILayout.Height(18)))
					{
						Type type = TypeHandler.ScriptableObjectTypes[selectedTypeIndex];
						TypeHandler.CreateNewInstance(createCount, type, selectionParams.assetsFolderPath,
							selectedTypeIndex);
						RefreshObjectsOfType(type);
					}

					createCount = EditorGUILayout.IntField(new GUIContent(
						"",
						"|How many items should be created?"
					), createCount, GUILayout.Width(40));
					createCount = Mathf.Max(1, createCount);
					if (GUILayout.Button(
						    EditorGUIUtility.IconContent("FilterByLabel", "|Clear all column filters"),
						    GUILayout.Width(24),
						    GUILayout.Height(18)))
					{
						selectionParams.instanceSearchString = string.Empty;
						columnManager.ClearFilterStrings();

						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
						columnManager.ApplyAllFilters();
					}

					if (GUILayout.Button(
						    new GUIContent(EditorGUIUtility.IconContent("d_winbtn_win_close").image,
							    "Clear current selection"),
						    GUILayout.Width(20),
						    GUILayout.Height(18)))
					{
						selectedRows.Clear();
						lastClickedRow = -1;
					}
				}
			}

			int dropIndex = columnManager.GetDropIndex();

			EditorGUILayout.Space();
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

			DrawHeaders(columnManager.ColumnCount, dropIndex);
			int drop = columnManager.CurrentDropIndex;
			if (drop >= 0 && drop < columnManager.ColumnCount)
			{
				Rect dropRect = columnManager.LastHeaderCellRects[drop];
				EditorGUI.DrawRect(dropRect, new Color(0f, 0.5f, 1f, 0.3f));
			}

			if (TypeHandler.CurrentTypeObjects.Any() && TypeHandler.TypeNames.Any())
			{
				DrawPropertiesGrid();
			}

			EditorGUILayout.EndScrollView();
		}

		private static void DrawStats()
		{
			using (new EditorGUILayout.VerticalScope())
			{
				GUILayout.Label($"Count: {TypeHandler.CurrentTypeObjects.Count}", GUILayout.Width(100));
				GUILayout.Label($"Total Memory: {TypeHandler.MemoryStats.totalMemoryAll / 1024f:F1} KB",
					GUILayout.Width(140));
				GUILayout.Label($"Filtered Memory: {TypeHandler.MemoryStats.totalMemoryFiltered / 1024f:F1} KB",
					GUILayout.Width(160));
			}
		}

		private void DrawAssetManagement()
		{
			using (new EditorGUILayout.VerticalScope())
			{
				using (new SOERegion())
				{
					GUILayout.Label("Path: ", GUILayout.Width(40));
					selectionParams.assetsFolderPath =
						GUILayout.TextField(selectionParams.assetsFolderPath, GUILayout.Width(200));

					if (GUILayout.Button(
						    EditorGUIUtility.IconContent("d_Import", "|Select folder to show scriptable objects in"),
						    GUILayout.Width(30)))
					{
						string sel = EditorUtility.OpenFolderPanel("Select Scriptable Object Folder",
							selectionParams.assetsFolderPath, "");
						if (!string.IsNullOrEmpty(sel))
						{
							selectionParams.assetsFolderPath = sel.Replace(Application.dataPath, "Assets");
							RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
						}
					}

					if (GUILayout.Button(
						    EditorGUIUtility.IconContent("d_Refresh",
							    "|Refresh the loaded assemblies/Scriptable objects"), GUILayout.Width(35)))
					{
						TypeHandler.LoadAvailableAssemblies(selectionParams);
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes.FirstOrDefault());
					}
				}

				using (new SOERegion())
				{
					GUILayout.Label("Assembly: ", GUILayout.Width(60));
					int newAsm = EditorGUILayout.Popup(selectedAssemblyIndex, TypeHandler.AssemblyNames,
						GUILayout.Width(200));
					if (newAsm != selectedAssemblyIndex)
					{
						selectedAssemblyIndex = newAsm;
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					}
				}

				using (new SOERegion())
				{
					GUILayout.Label("Type: ", GUILayout.Width(40));

					var filteredNames = TypeHandler.TypeNames
						.Where(n => n.IndexOf(selectionParams.typeSearchString, StringComparison.OrdinalIgnoreCase) >=
						            0)
						.ToArray();
					int[] originalIndexes = TypeHandler.TypeNames
						.Select((n, idx) => new {n, idx})
						.Where(x => filteredNames.Contains(x.n))
						.Select(x => x.idx)
						.ToArray();
					int selInFiltered = Array.IndexOf(originalIndexes, selectedTypeIndex);
					int newSelInFiltered = EditorGUILayout.Popup(selInFiltered >= 0 ? selInFiltered : 0, filteredNames,
						GUILayout.Width(200));
					if (newSelInFiltered >= 0 && newSelInFiltered < originalIndexes.Length)
					{
						int newTypeIdx = originalIndexes[newSelInFiltered];
						if (newTypeIdx != selectedTypeIndex)
						{
							selectedTypeIndex = newTypeIdx;
							RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
						}
					}

					GUILayout.Label("Search: ", GUILayout.Width(50));
					string newTypeFilter = GUILayout.TextField(selectionParams.typeSearchString, GUILayout.Width(100));
					if (newTypeFilter != selectionParams.typeSearchString)
					{
						selectionParams.typeSearchString = newTypeFilter;
						TypeHandler.LoadScriptableObjectTypes(selectionParams);
					}

					EditorGUI.BeginChangeCheck();
					bool inc = EditorGUILayout.ToggleLeft("Include Derived", selectionParams.includeDerivedTypes,
						GUILayout.Width(120));
					if (EditorGUI.EndChangeCheck())
					{
						selectionParams.includeDerivedTypes = inc;
						TypeHandler.LoadScriptableObjectTypes(selectionParams);
						RefreshObjectsOfType(TypeHandler.ScriptableObjectTypes[selectedTypeIndex]);
					}
				}
			}
		}

		private void DrawHeaders(int totalCols, int dropIndex)
		{
			using (new SOERegion())
			{
				for (int i = 0; i < totalCols; i++)
				{
					DrawHeaderCell(i);
					if (i < columnManager.Columns.Count - 1)
						GUILayout.Space(cellPadding);
					if (i == dropIndex)
					{
						Rect hdrRect = GUILayoutUtility.GetLastRect();
						EditorGUI.DrawRect(hdrRect, new Color(0f, 0.5f, 1f, 0.3f));
					}
				}
			}
		}

		private void DrawHeaderCell(int columnIndex)
		{
			columnManager.EnsureColumnFiltersAndRects();

			var content = new GUIContent(columnManager.GetSortedLabel(columnIndex));
			Rect cellRect = GUILayoutUtility.GetRect(content, EditorStyles.boldLabel,
				GUILayout.Width(columnManager.Columns[columnIndex].Width));
			GUI.Label(cellRect, content, EditorStyles.boldLabel);

			Rect iconRect = new Rect(cellRect.xMax - 22, cellRect.y + 2, 14, 14);
			if (GUI.Button(iconRect, EditorGUIUtility.IconContent("Search Icon", "Filter scriptable object property"),
				    GUIStyle.none))
			{
				ShowFilterPopup(columnIndex, iconRect);
			}

			columnManager.HandleColumnInput(
				selectedTypeIndex,
				columnIndex,
				cellRect,
				() => Repaint()
			);
		}

		private void ShowFilterPopup(int columnIndex, Rect triggerRect) =>
			PopupWindow.Show(triggerRect, new ColumnFilterPopup(columnIndex, columnManager));

		private void DrawPropertiesGrid()
		{
			var toAdd = new List<ScriptableObject>();
			var toRemove = new List<ScriptableObject>();
			int rowIndex = 0;

			foreach (var obj in TypeHandler.CurrentTypeObjects)
			{
				using (new SOERegion())
				{
					for (int i = 0; i < columnManager.ColumnCount; i++)
					{
						var col = columnManager.Columns[i];

						var opts = new[] {GUILayout.Width(col.Width), GUILayout.Height(18)};

						if (col.ColType == Column.ColumnType.BuiltIn)
						{
							col.DrawAction(obj, toAdd, toRemove, opts);
						}
						else
						{
							var so = new SerializedObject(obj);
							var prop = so.FindProperty(col.PropertyPath);
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
												var p = tgt.FindProperty(col.PropertyPath);
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
												var p = tgt.FindProperty(col.PropertyPath);
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
												var p = tgt.FindProperty(col.PropertyPath);
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
												var p = tgt.FindProperty(col.PropertyPath);
												p.colorValue = newC;
												tgt.ApplyModifiedProperties();
											}

										break;
									case SerializedPropertyType.ObjectReference:
									{
										Rect fieldRect = GUILayoutUtility.GetRect(col.Width, 18);

										EditorGUI.BeginChangeCheck();
										UnityEngine.Object newObj = EditorGUI.ObjectField(
											fieldRect,
											prop.objectReferenceValue,
											typeof(UnityEngine.Object),
											false
										);
										if (EditorGUI.EndChangeCheck())
										{
											foreach (int idx in selectedRows)
											{
												var tgt = new SerializedObject(TypeHandler.CurrentTypeObjects[idx]);
												var p = tgt.FindProperty(col.PropertyPath);
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

						if (i < columnManager.Columns.Count - 1)
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