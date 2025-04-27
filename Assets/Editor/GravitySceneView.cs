using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class GravitySceneViewWindow : EditorWindow
{
	private const string PrefTab = "GravitySceneView_selectedTab";
	private const string PrefFixedDt = "GravitySceneView_fixedDt";
	private const string PrefDropKey = "GravitySceneView_dropKey";
	private const string PrefDropMod = "GravitySceneView_dropMod";
	private const string PrefSpawnKey = "GravitySceneView_spawnKey";
	private const string PrefSpawnMod = "GravitySceneView_spawnMod";
	private const string PrefSpawnerEnabled = "GravitySceneView_spawnerEnabled";
	private const string PrefGravityX = "GravitySceneView_gravityX";
	private const string PrefGravityY = "GravitySceneView_gravityY";
	private const string PrefGravityZ = "GravitySceneView_gravityZ";
	private const string PrefLayerMask = "GravitySceneView_layerMask";
	private const string PrefPrefabGuid = "GravitySceneView_prefabGuid";
	private const string PrefSpawnPosition = "GravitySceneView_spawnPos";

	private int selectedTab;
	private float fixedDt;
	private KeyCode dropKey;
	private EventModifiers dropMod;
	private KeyCode spawnKey;
	private EventModifiers spawnMod;
	private bool spawnerEnabled;
	private Vector3 gravity;
	private LayerMask layerMask;

	private GameObject prefab;
	private Vector3 spawnPosition;
	private Quaternion spawnRotation = Quaternion.identity;

	private SimulationMode prevSimMode;
	private bool isSimulating;
	private readonly List<Rigidbody> dropRBs = new List<Rigidbody>();
	private readonly Dictionary<Rigidbody, bool> rbPreExisting = new Dictionary<Rigidbody, bool>();
	private readonly Dictionary<Collider, bool> colPreExisting = new Dictionary<Collider, bool>();

	[MenuItem("Window/Gravity Scene View")]
	public static void ShowWindow()
	{
		GetWindow<GravitySceneViewWindow>("Gravity Scene View");
	}

	private void OnEnable()
	{
		selectedTab = EditorPrefs.GetInt(PrefTab, 0);
		fixedDt = EditorPrefs.GetFloat(PrefFixedDt, 0.02f);
		dropKey = (KeyCode) EditorPrefs.GetInt(PrefDropKey, (int) KeyCode.D);
		dropMod = (EventModifiers) EditorPrefs.GetInt(PrefDropMod, (int) EventModifiers.None);
		spawnKey = (KeyCode) EditorPrefs.GetInt(PrefSpawnKey, (int) KeyCode.F);
		spawnMod = (EventModifiers) EditorPrefs.GetInt(PrefSpawnMod, (int) EventModifiers.None);
		spawnerEnabled = EditorPrefs.GetBool(PrefSpawnerEnabled, true);
		gravity = new Vector3(
			EditorPrefs.GetFloat(PrefGravityX, Physics.gravity.x),
			EditorPrefs.GetFloat(PrefGravityY, Physics.gravity.y),
			EditorPrefs.GetFloat(PrefGravityZ, Physics.gravity.z)
		);
		layerMask = EditorPrefs.GetInt(PrefLayerMask, ~0);
		string guid = EditorPrefs.GetString(PrefPrefabGuid, string.Empty);
		if (!string.IsNullOrEmpty(guid))
		{
			prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
		}

		spawnPosition = StringToVector3(EditorPrefs.GetString(PrefSpawnPosition, Vector3.zero.ToString()));
		prevSimMode = Physics.simulationMode;
		Physics.simulationMode = SimulationMode.Script;
		EditorApplication.update += EditorUpdate;
		SceneView.duringSceneGui += OnSceneGUI;
	}

	private void OnDisable()
	{
		Physics.simulationMode = prevSimMode;
		EditorApplication.update -= EditorUpdate;
		SceneView.duringSceneGui -= OnSceneGUI;
	}

	private void OnGUI()
	{
		EditorGUI.BeginChangeCheck();
		selectedTab = GUILayout.Toolbar(selectedTab, new[] {"Scene Gravity", "Spawner"});
		if (EditorGUI.EndChangeCheck())
		{
			EditorPrefs.SetInt(PrefTab, selectedTab);
		}

		GUILayout.Space(10);
		if (selectedTab == 0)
		{
			EditorGUI.BeginChangeCheck();
			GUILayout.Label("Simulation Settings", EditorStyles.boldLabel);
			fixedDt = EditorGUILayout.FloatField("Fixed Timestep", fixedDt);
			dropKey = (KeyCode) EditorGUILayout.EnumPopup("Drop Key", dropKey);
			dropMod = (EventModifiers) EditorGUILayout.EnumPopup("Drop Modifier", dropMod);
			gravity = EditorGUILayout.Vector3Field("Gravity", gravity);
			layerMask = EditorGUILayout.MaskField("Layer Mask", layerMask, InternalEditorUtility.layers);
			if (EditorGUI.EndChangeCheck())
			{
				EditorPrefs.SetFloat(PrefFixedDt, fixedDt);
				EditorPrefs.SetInt(PrefDropKey, (int) dropKey);
				EditorPrefs.SetInt(PrefDropMod, (int) dropMod);
				EditorPrefs.SetFloat(PrefGravityX, gravity.x);
				EditorPrefs.SetFloat(PrefGravityY, gravity.y);
				EditorPrefs.SetFloat(PrefGravityZ, gravity.z);
				EditorPrefs.SetInt(PrefLayerMask, layerMask);
			}

			GUILayout.Space(10);
			if (GUILayout.Button(isSimulating ? "Simulating..." : $"Drop Selected ({dropMod}+{dropKey})"))
			{
				BeginDropSelection();
			}

			if (GUILayout.Button("Stop Simulations"))
			{
				StopAllSimulations();
			}
		}
		else if (selectedTab == 1)
		{
			EditorGUI.BeginChangeCheck();
			GUILayout.Label("Spawner Settings", EditorStyles.boldLabel);
			spawnerEnabled = EditorGUILayout.Toggle("Enable Spawner", spawnerEnabled);
			prefab = (GameObject) EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
			spawnKey = (KeyCode) EditorGUILayout.EnumPopup("Spawn Key", spawnKey);
			spawnMod = (EventModifiers) EditorGUILayout.EnumPopup("Spawn Modifier", spawnMod);
			spawnPosition = EditorGUILayout.Vector3Field("Spawn Position", spawnPosition);
			spawnRotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Spawn Rotation", spawnRotation.eulerAngles));
			if (EditorGUI.EndChangeCheck())
			{
				EditorPrefs.SetBool(PrefSpawnerEnabled, spawnerEnabled);
				if (prefab != null)
				{
					EditorPrefs.SetString(PrefPrefabGuid,
						AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
				}

				EditorPrefs.SetInt(PrefSpawnKey, (int) spawnKey);
				EditorPrefs.SetInt(PrefSpawnMod, (int) spawnMod);
				EditorPrefs.SetString(PrefSpawnPosition, spawnPosition.ToString());
			}

			GUILayout.Space(10);
			EditorGUI.BeginDisabledGroup(!spawnerEnabled);
			if (GUILayout.Button(isSimulating ? "Simulating..." : $"Spawn & Drop ({spawnMod}+{spawnKey})"))
			{
				SpawnAndDropPrefab();
			}

			if (GUILayout.Button("Stop Simulations"))
			{
				StopAllSimulations();
			}

			EditorGUI.EndDisabledGroup();
		}
	}

	private void OnSceneGUI(SceneView sceneView)
	{
		Event e = Event.current;
		if (selectedTab == 0 && e.type == EventType.KeyDown && e.keyCode == dropKey &&
		    (dropMod == EventModifiers.None || (e.modifiers & dropMod) != 0))
		{
			BeginDropSelection();
			e.Use();
		}

		if (selectedTab == 1 && spawnerEnabled && prefab != null && e.type == EventType.KeyDown &&
		    e.keyCode == spawnKey && (spawnMod == EventModifiers.None || (e.modifiers & spawnMod) != 0))
		{
			SpawnAndDropPrefab();
			e.Use();
		}

		if (selectedTab == 1 && spawnerEnabled && prefab != null)
		{
			EditorGUI.BeginChangeCheck();
			Vector3 newPos = Handles.PositionHandle(spawnPosition, spawnRotation);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Move Spawn Position");
				spawnPosition = newPos;
				EditorPrefs.SetString(PrefSpawnPosition, spawnPosition.ToString());
			}

			Handles.Label(spawnPosition + Vector3.up * 0.5f, "Spawn Point");
		}
	}

	private void BeginDropSelection()
	{
		isSimulating = false;
		Vector3 prevGr = Physics.gravity;
		Physics.gravity = gravity;
		foreach (GameObject go in Selection.gameObjects)
		{
			if (((1 << go.layer) & layerMask) == 0)
			{
				continue;
			}

			Collider col = go.GetComponent<Collider>();
			bool existedCol = col != null;
			if (!existedCol)
			{
				col = go.AddComponent<BoxCollider>();
				colPreExisting[col] = false;
			}

			Undo.RecordObject(go.transform, "Gravity Drop");
			Rigidbody rb = go.GetComponent<Rigidbody>();
			bool existedRb = rb != null;
			if (!existedRb)
			{
				rb = Undo.AddComponent<Rigidbody>(go);
			}

			rb.isKinematic = false;
			rbPreExisting[rb] = existedRb;
			dropRBs.Add(rb);
		}

		Physics.gravity = prevGr;
		isSimulating = dropRBs.Count > 0;
	}

	private void SpawnAndDropPrefab()
	{
		if (prefab == null)
		{
			return;
		}

		GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
		if (go == null)
		{
			Debug.LogError("Failed to instantiate prefab.");
			return;
		}

		Undo.RegisterCreatedObjectUndo(go, "Spawn Prefab");
		go.transform.position = spawnPosition;
		go.transform.rotation = spawnRotation;
		Collider col = go.GetComponent<Collider>();
		bool existedCol = col != null;
		if (!existedCol)
		{
			col = go.AddComponent<BoxCollider>();
			colPreExisting[col] = false;
		}

		Rigidbody rb = go.GetComponent<Rigidbody>();
		bool existedRb = rb != null;
		if (!existedRb)
		{
			rb = Undo.AddComponent<Rigidbody>(go);
		}

		if (rb == null)
		{
			Debug.LogError("Failed to add Rigidbody.");
			return;
		}

		rb.isKinematic = false;
		rbPreExisting[rb] = existedRb;
		dropRBs.Add(rb);
		isSimulating = true;
	}

	private void StopAllSimulations()
	{
		foreach (KeyValuePair<Rigidbody, bool> kv in rbPreExisting)
		{
			if (kv.Key != null && !kv.Value)
			{
				Undo.DestroyObjectImmediate(kv.Key);
			}
		}

		foreach (KeyValuePair<Collider, bool> kv in colPreExisting)
		{
			if (kv.Key != null && !kv.Value)
			{
				Undo.DestroyObjectImmediate(kv.Key);
			}
		}

		dropRBs.Clear();
		rbPreExisting.Clear();
		colPreExisting.Clear();
		isSimulating = false;
		SceneView.RepaintAll();
	}

	private void EditorUpdate()
	{
		if (!isSimulating)
		{
			return;
		}

		Physics.Simulate(fixedDt);
		bool allSleeping = true;
		foreach (Rigidbody rb in dropRBs)
		{
			if (rb != null && !rb.IsSleeping())
			{
				allSleeping = false;
				break;
			}
		}

		if (!allSleeping)
		{
			return;
		}

		StopAllSimulations();
	}

	private static Vector3 StringToVector3(string s)
	{
		s = s.Trim('(', ')');
		string[] parts = s.Split(',');
		return new Vector3(
			float.Parse(parts[0]),
			float.Parse(parts[1]),
			float.Parse(parts[2])
		);
	}
}