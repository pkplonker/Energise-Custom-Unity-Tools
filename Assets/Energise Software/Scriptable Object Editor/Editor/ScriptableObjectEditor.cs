using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ScriptableObjectEditor
{
	public partial class ScriptableObjectEditorWindow : EditorWindow
	{
		private Vector2 scrollPosition;
		private List<Type> scriptableObjectTypes;
		private string[] typeNames;
		private int selectedTypeIndex;

		private readonly List<ScriptableObject> currentTypeObjects = new();
		private static string assetsFolderPath = "Assets";

		private List<Assembly> availableAssemblies;
		private string[] assemblyNames;
		private int selectedAssemblyIndex;

		private bool includeDerivedTypes = true;
		private DateTime lastAssemblyCheckTime = DateTime.Now;
		private int draggingColumn = -1;
		private float dragStartMouseX;
		private float dragStartWidth;
		private List<float> columnWidths = new List<float>();

		[MenuItem("Window/Energise Tools/Scriptable Object Editor &%S")]
		public static void ShowWindow() => GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");
		
		static ScriptableObjectEditorWindow()
		{
			AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
		}

		private static void OnAfterAssemblyReload()
		{
			var window = GetWindow<ScriptableObjectEditorWindow>();
			window.LoadAvailableAssemblies();
			window.LoadScriptableObjectTypes();
			window.LoadObjectsOfType(window.scriptableObjectTypes.FirstOrDefault());
			window.Repaint();
		}

		private void OnEnable()
		{
			LoadAvailableAssemblies();
			LoadScriptableObjectTypes();
			LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
		}

		/// <summary>
		/// Reloads the assemblies
		/// </summary>
		private void LoadAvailableAssemblies()
		{
			availableAssemblies = GetAssembliesWithScriptableObjects();

			assemblyNames = availableAssemblies
				.Select(assembly => assembly.GetName().Name)
				.Prepend("All Assemblies")
				.ToArray();
		}

		/// <summary>
		/// Gets all assemblies containing a scriptable object
		/// </summary>
		/// <returns>The collection of assemblies</returns>
		private static List<Assembly> GetAssembliesWithScriptableObjects()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly =>
					!assembly.FullName.StartsWith("UnityEngine") &&
					!assembly.FullName.StartsWith("UnityEditor") &&
					!assembly.FullName.StartsWith("Unity."))
				.Where(assembly =>
					assembly.GetTypes().Any(type =>
						type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract && IsInAssetsFolder(type)))
				.OrderBy(assembly => assembly.GetName().Name)
				.ToList();
		}

		/// <summary>
		/// Gets all the types derived from <see cref="scriptableObjectTypes"/>
		/// </summary>
		private void LoadScriptableObjectTypes()
		{
			IEnumerable<Type> types;

			if (selectedAssemblyIndex == 0)
			{
				types = availableAssemblies.SelectMany(assembly => assembly.GetTypes());
			}
			else
			{
				types = availableAssemblies[selectedAssemblyIndex - 1].GetTypes();
			}

			scriptableObjectTypes = types
				.Where(type =>
					type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract && IsInAssetsFolder(type))
				.OrderBy(type => type.Name)
				.ToList();

			typeNames = scriptableObjectTypes.Select(type => type.Name).ToArray();
		}

		private static bool IsInAssetsFolder(Type type)
		{
			var guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] {assetsFolderPath});
			return guids.Any();
		}

		/// <summary>
		/// Adds to "currentTypeObjects"/> all objects of the provided type
		/// </summary>
		/// <param name="type">The type to load</param>
		private void LoadObjectsOfType(Type type)
		{
			if (type == null)
			{
				currentTypeObjects.Clear();
				return;
			}

			currentTypeObjects.Clear();

			var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] {assetsFolderPath});
			foreach (var guid in guids)
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(guid);
				var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

				if (obj == null) continue;
				if (includeDerivedTypes)
				{
					if (type.IsAssignableFrom(obj.GetType()))
					{
						currentTypeObjects.Add(obj);
					}
				}
				else
				{
					if (obj.GetType() == type)
					{
						currentTypeObjects.Add(obj);
					}
				}
			}
		}

		/// <summary>
		/// Updates the UI
		/// </summary>
		private void OnGUI()
		{
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
			{
				EditorGUILayout.BeginVertical("box");
				{
					EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);

					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.LabelField("Path", GUILayout.Width(40));
						assetsFolderPath = EditorGUILayout.TextField(assetsFolderPath, GUILayout.Width(200));

						if (GUILayout.Button("Browse", GUILayout.Width(80)))
						{
							string selectedPath = EditorUtility.OpenFolderPanel("Select Scriptable Object Folder",
								assetsFolderPath, "");
							if (!string.IsNullOrEmpty(selectedPath))
							{
								assetsFolderPath = selectedPath.Replace(Application.dataPath, "Assets");
								LoadScriptableObjectTypes();
								LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
							}
						}

						EditorGUILayout.LabelField("Assembly", GUILayout.Width(60));
						var newAssemblyIndex =
							EditorGUILayout.Popup(selectedAssemblyIndex, assemblyNames, GUILayout.Width(200));
						if (newAssemblyIndex != selectedAssemblyIndex)
						{
							selectedAssemblyIndex = newAssemblyIndex;
							LoadScriptableObjectTypes();
							selectedTypeIndex = 0;
							LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
						}

						if (GUILayout.Button("Refresh Assemblies", GUILayout.Width(150)))
						{
							LoadAvailableAssemblies();
							LoadScriptableObjectTypes();
							LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
						}
					}
					EditorGUILayout.EndHorizontal();

					EditorGUILayout.Space();

					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.LabelField("Type", GUILayout.Width(40));
						var newSelectedTypeIndex =
							EditorGUILayout.Popup(selectedTypeIndex, typeNames, GUILayout.Width(200));
						if (newSelectedTypeIndex != selectedTypeIndex)
						{
							selectedTypeIndex = newSelectedTypeIndex;
							LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
						}

						var newIncludeDerivedTypes =
							EditorGUILayout.ToggleLeft("Include Derived", includeDerivedTypes, GUILayout.Width(120));
						if (newIncludeDerivedTypes != includeDerivedTypes)
						{
							includeDerivedTypes = newIncludeDerivedTypes;
							LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
						}

						if (GUILayout.Button("Load Objects", GUILayout.Width(100)))
						{
							LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
						}
					}
					EditorGUILayout.EndHorizontal();

					EditorGUILayout.Space();

					if (currentTypeObjects.Any() && typeNames.Any())
					{
						EditorGUILayout.BeginVertical();
						{
							DrawPropertiesGrid();
						}
						EditorGUILayout.EndVertical();
					}
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawHeaderCell(string label, float width, int columnIndex)
		{
			var content = new GUIContent(label);
			Rect cellRect = GUILayoutUtility.GetRect(content, EditorStyles.boldLabel, GUILayout.Width(width));
			GUI.Label(cellRect, content, EditorStyles.boldLabel);
			Rect handleRect = new Rect(cellRect.xMax - 4, cellRect.y, 8, cellRect.height);
			EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
			var e = Event.current;
			if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
			{
				draggingColumn = columnIndex;
				dragStartMouseX = e.mousePosition.x;
				dragStartWidth = columnWidths[columnIndex];
				e.Use();
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
				e.Use();
			}
		}

		private void DrawPropertiesGrid()
		{
			if (currentTypeObjects.Count == 0) return;

			// Build list of property paths once
			var firstSO = new SerializedObject(currentTypeObjects[0]);
			var propIter = firstSO.GetIterator();
			var propertyPaths = new List<string>();
			if (propIter.NextVisible(true))
			{
				do
				{
					propertyPaths.Add(propIter.propertyPath);
				} while (propIter.NextVisible(false));
			}

			int totalCols = 2 + propertyPaths.Count;
			if (columnWidths.Count != totalCols)
			{
				columnWidths.Clear();
				columnWidths.Add(60); // Actions
				columnWidths.Add(150); // Name
				foreach (var path in propertyPaths)
					columnWidths.Add(Mathf.Max(100, path.Length * 10));
			}

			EditorGUILayout.BeginHorizontal("box");
			DrawHeaderCell("Actions", columnWidths[0], 0);
			DrawHeaderCell("Instance Name", columnWidths[1], 1);
			for (int i = 0; i < propertyPaths.Count; i++)
				DrawHeaderCell(propertyPaths[i], columnWidths[i + 2], i + 2);
			EditorGUILayout.EndHorizontal();

			var toAdd = new List<ScriptableObject>();

			// Rows
			foreach (var obj in currentTypeObjects)
			{
				var so = new SerializedObject(obj);
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus"), GUILayout.Width(columnWidths[0]),
					    GUILayout.Height(18)))
				{
					toAdd.Add(obj);
				}

				EditorGUILayout.LabelField(obj.name, EditorStyles.textField, GUILayout.Width(columnWidths[1]));
				for (int i = 0; i < propertyPaths.Count; i++)
				{
					var prop = so.FindProperty(propertyPaths[i]);
					if (prop != null)
						EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(columnWidths[i + 2]));
				}

				EditorGUILayout.EndHorizontal();
				so.ApplyModifiedProperties();
			}

			foreach (var add in toAdd)
			{
				var assetPath = AssetDatabase.GetAssetPath(add);
				if (TryGetUniqueAssetPath(assetPath, out var newAssetPath))
				{
					var newObj = Instantiate(add);
					AssetDatabase.CreateAsset(newObj, newAssetPath);
					AssetDatabase.SaveAssets();
				}
			}
		}

		private bool TryGetUniqueAssetPath(string originalPath, out string uniquePath)
		{
			uniquePath = null;
			try
			{
				var directory = Path.GetDirectoryName(originalPath);
				var extension = Path.GetExtension(originalPath);
				var name = Path.GetFileNameWithoutExtension(originalPath);
				if (directory == null) return false;

				int pos = name.Length - 1;
				while (pos >= 0 && char.IsDigit(name[pos])) pos--;
				bool hasNumber = pos < name.Length - 1;
				string baseName = hasNumber ? name.Substring(0, pos + 1) : name;
				int index = 0;
				if (hasNumber && !int.TryParse(name.Substring(pos + 1), out index)) index = 0;

				string candidate;
				do
				{
					index++;
					candidate = baseName + (hasNumber ? index.ToString() : "_Copy" + index) + extension;
					uniquePath = Path.Combine(directory, candidate);
				} while (File.Exists(uniquePath));

				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}