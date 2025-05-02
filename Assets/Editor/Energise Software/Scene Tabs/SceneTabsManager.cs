using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class SceneTabInjector
{
	static Type dockAreaType;
	static MethodInfo addTabMethod;
	static FieldInfo panesField;

	static SceneTabInjector()
	{
		dockAreaType = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(a => a.GetTypes())
			.FirstOrDefault(t => t.Name == "DockArea");
		if (dockAreaType == null)
		{
			return;
		}

		addTabMethod = dockAreaType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			.FirstOrDefault(m => m.Name == "AddTab" &&
			                     ((m.GetParameters().Length == 2 &&
			                       m.GetParameters()[0].ParameterType == typeof(EditorWindow)) ||
			                      (m.GetParameters().Length == 3 &&
			                       m.GetParameters()[0].ParameterType == typeof(int))));
		if (addTabMethod == null)
		{
			return;
		}

		panesField = dockAreaType.GetField("m_Panes", BindingFlags.Instance | BindingFlags.NonPublic);
		if (panesField == null)
		{
			return;
		}

		EditorApplication.delayCall += InjectProofOfConceptTab;
	}

	private static void InjectProofOfConceptTab()
	{
		var sceneView = SceneView.lastActiveSceneView;
		if (sceneView == null) return;

		var allDocks = Resources.FindObjectsOfTypeAll(dockAreaType);
		object targetDock = null;
		foreach (var dock in allDocks)
		{
			var panes = panesField.GetValue(dock) as IList;
			if (panes != null && panes.Cast<EditorWindow>().Contains(sceneView))
			{
				targetDock = dock;
				break;
			}
		}

		if (targetDock == null)
		{
			return;
		}

		var pocView = ScriptableObject.CreateInstance<SceneView>();
		pocView.titleContent = new GUIContent("POC_Tab");

		var panesList = panesField.GetValue(targetDock) as IList;
		if (addTabMethod.GetParameters().Length == 2)
		{
			addTabMethod.Invoke(targetDock, new object[] {pocView, true});
		}
		else
		{
			addTabMethod.Invoke(targetDock, new object[] {panesList.Count, pocView, true});
		}
	}
}