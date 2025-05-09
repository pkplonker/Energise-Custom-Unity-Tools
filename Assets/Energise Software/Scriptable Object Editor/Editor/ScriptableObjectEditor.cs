using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ScriptableObjectEditor
{
    public partial class ScriptableObjectEditorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<Type> scriptableObjectTypes;
        private string[] typeNames;
        private int selectedTypeIndex;

        private List<ScriptableObject> currentTypeObjects = new();
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

        // New search strings
        private string typeSearchString = string.Empty;
        private string instanceSearchString = string.Empty;

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

        private void LoadAvailableAssemblies()
        {
            availableAssemblies = GetAssembliesWithScriptableObjects();
            assemblyNames = availableAssemblies
                .Select(assembly => assembly.GetName().Name)
                .Prepend("All Assemblies")
                .ToArray();
        }

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

        private void LoadScriptableObjectTypes()
        {
            IEnumerable<Type> types = selectedAssemblyIndex == 0
                ? availableAssemblies.SelectMany(a => a.GetTypes())
                : availableAssemblies[selectedAssemblyIndex - 1].GetTypes();

            scriptableObjectTypes = types
                .Where(t => t.IsSubclassOf(typeof(ScriptableObject)) && !t.IsAbstract && IsInAssetsFolder(t))
                .OrderBy(t => t.Name)
                .ToList();

            // apply type search filter
            if (!string.IsNullOrEmpty(typeSearchString))
            {
                scriptableObjectTypes = scriptableObjectTypes
                    .Where(t => t.Name.IndexOf(typeSearchString, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            typeNames = scriptableObjectTypes.Select(t => t.Name).ToArray();
            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex, 0, typeNames.Length - 1);
        }

        private static bool IsInAssetsFolder(Type type)
        {
            var guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] {assetsFolderPath});
            return guids.Any();
        }

        private void LoadObjectsOfType(Type type)
        {
            currentTypeObjects.Clear();
            if (type == null) return;

            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] {assetsFolderPath});
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (obj == null) continue;

                if ((includeDerivedTypes && type.IsAssignableFrom(obj.GetType())) ||
                    (!includeDerivedTypes && obj.GetType() == type))
                {
                    currentTypeObjects.Add(obj);
                }
            }

            // apply instance search filter
            if (!string.IsNullOrEmpty(instanceSearchString))
            {
                currentTypeObjects = currentTypeObjects
                    .Where(o => o.name.IndexOf(instanceSearchString, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);
            // folder & assembly selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path", GUILayout.Width(40));
            assetsFolderPath = EditorGUILayout.TextField(assetsFolderPath, GUILayout.Width(200));
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string sel = EditorUtility.OpenFolderPanel("Select Scriptable Object Folder", assetsFolderPath, "");
                if (!string.IsNullOrEmpty(sel))
                {
                    assetsFolderPath = sel.Replace(Application.dataPath, "Assets");
                    LoadScriptableObjectTypes();
                    LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
                }
            }
            
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Refresh"), GUILayout.Width(35)))
            {
	            LoadAvailableAssemblies();
	            LoadScriptableObjectTypes();
	            LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
            }
            int newAsm = EditorGUILayout.Popup(selectedAssemblyIndex, assemblyNames, GUILayout.Width(200));
            if (newAsm != selectedAssemblyIndex)
            {
	            selectedAssemblyIndex = newAsm;
	            LoadScriptableObjectTypes();
	            LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            // type search and selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Type", GUILayout.Width(40));
            int newTypeIdx = EditorGUILayout.Popup(selectedTypeIndex, typeNames, GUILayout.Width(200));
            if (newTypeIdx != selectedTypeIndex)
            {
	            selectedTypeIndex = newTypeIdx;
	            LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
            }
            includeDerivedTypes = EditorGUILayout.ToggleLeft("Include Derived", includeDerivedTypes, GUILayout.Width(120));
            EditorGUILayout.LabelField("Search Types", GUILayout.Width(80));

            var newTypeSearch = EditorGUILayout.TextField(typeSearchString, GUILayout.Width(200));
            if (newTypeSearch != typeSearchString)
            {
                typeSearchString = newTypeSearch;
                LoadScriptableObjectTypes();
                LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
            }
            
            EditorGUILayout.LabelField("Search Instances", GUILayout.Width(100));
            var newInstSearch = EditorGUILayout.TextField(instanceSearchString, GUILayout.Width(200));
            if (newInstSearch != instanceSearchString)
            {
                instanceSearchString = newInstSearch;
                LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
            }
            // if (GUILayout.Button("Load Objects", GUILayout.Width(100)))
            // {
            //     LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
            // }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (currentTypeObjects.Any() && typeNames.Any())
            {
                DrawPropertiesGrid();
            }

            EditorGUILayout.EndVertical();
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