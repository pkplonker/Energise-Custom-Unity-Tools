using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class GravitySceneViewWindow : EditorWindow
{
    const string PrefTab = "GravitySceneView_selectedTab";
    const string PrefFixedDt = "GravitySceneView_fixedDt";
    const string PrefDropKey = "GravitySceneView_dropKey";
    const string PrefSpawnKey = "GravitySceneView_spawnKey";
    const string PrefSpawnerEnabled = "GravitySceneView_spawnerEnabled";
    const string PrefGravityX = "GravitySceneView_gravityX";
    const string PrefGravityY = "GravitySceneView_gravityY";
    const string PrefGravityZ = "GravitySceneView_gravityZ";
    const string PrefLayerMask = "GravitySceneView_layerMask";
    const string PrefPrefabGuid = "GravitySceneView_prefabGuid";
    const string PrefSpawnPosition = "GravitySceneView_spawnPos";

    int selectedTab;
    float fixedDt;
    KeyCode dropKey;
    KeyCode spawnKey;
    bool spawnerEnabled;
    Vector3 gravity;
    LayerMask layerMask;

    GameObject prefab;
    Vector3 spawnPosition;
    Quaternion spawnRotation = Quaternion.identity;

    SimulationMode prevSimMode;
    bool isSimulating;
    List<Rigidbody> dropRBs = new List<Rigidbody>();
    Dictionary<Rigidbody, bool> rbPreExisting = new Dictionary<Rigidbody, bool>();
    Dictionary<Collider, bool> colPreExisting = new Dictionary<Collider, bool>();

    [MenuItem("Window/Gravity Scene View")]
    public static void ShowWindow()
    {
        GetWindow<GravitySceneViewWindow>("Gravity Scene View");
    }

    void OnEnable()
    {
        selectedTab = EditorPrefs.GetInt(PrefTab, 0);
        fixedDt = EditorPrefs.GetFloat(PrefFixedDt, 0.02f);
        dropKey = (KeyCode)EditorPrefs.GetInt(PrefDropKey, (int)KeyCode.D);
        spawnKey = (KeyCode)EditorPrefs.GetInt(PrefSpawnKey, (int)KeyCode.F);
        spawnerEnabled = EditorPrefs.GetBool(PrefSpawnerEnabled, true);
        gravity = new Vector3(
            EditorPrefs.GetFloat(PrefGravityX, Physics.gravity.x),
            EditorPrefs.GetFloat(PrefGravityY, Physics.gravity.y),
            EditorPrefs.GetFloat(PrefGravityZ, Physics.gravity.z)
        );
        layerMask = EditorPrefs.GetInt(PrefLayerMask, ~0);
        string guid = EditorPrefs.GetString(PrefPrefabGuid, "");
        if (!string.IsNullOrEmpty(guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        spawnPosition = StringToVector3(EditorPrefs.GetString(PrefSpawnPosition, Vector3.zero.ToString()));
        prevSimMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        EditorApplication.update += EditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        Physics.simulationMode = prevSimMode;
        EditorApplication.update -= EditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        selectedTab = GUILayout.Toolbar(selectedTab, new[] { "Scene Gravity", "Spawner" });
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
            dropKey = (KeyCode)EditorGUILayout.EnumPopup("Drop Key", dropKey);
            gravity = EditorGUILayout.Vector3Field("Gravity", gravity);
            int mask = EditorGUILayout.MaskField("Layer Mask", layerMask, InternalEditorUtility.layers);
            layerMask = mask;
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetFloat(PrefFixedDt, fixedDt);
                EditorPrefs.SetInt(PrefDropKey, (int)dropKey);
                EditorPrefs.SetFloat(PrefGravityX, gravity.x);
                EditorPrefs.SetFloat(PrefGravityY, gravity.y);
                EditorPrefs.SetFloat(PrefGravityZ, gravity.z);
                EditorPrefs.SetInt(PrefLayerMask, layerMask);
            }
            GUILayout.Space(10);
            if (GUILayout.Button(isSimulating ? "Simulating..." : $"Drop Selected ({dropKey})"))
            {
                if (!isSimulating)
                {
                    BeginDropSelection();
                }
            }
        }
        else if (selectedTab == 1)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Spawner Settings", EditorStyles.boldLabel);
            spawnerEnabled = EditorGUILayout.Toggle("Enable Spawner", spawnerEnabled);
            prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            spawnKey = (KeyCode)EditorGUILayout.EnumPopup("Spawn Key", spawnKey);
            spawnPosition = EditorGUILayout.Vector3Field("Spawn Position", spawnPosition);
            spawnRotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Spawn Rotation", spawnRotation.eulerAngles));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PrefSpawnerEnabled, spawnerEnabled);
                if (prefab != null)
                {
                    string path = AssetDatabase.GetAssetPath(prefab);
                    EditorPrefs.SetString(PrefPrefabGuid, AssetDatabase.AssetPathToGUID(path));
                }
                EditorPrefs.SetInt(PrefSpawnKey, (int)spawnKey);
                EditorPrefs.SetString(PrefSpawnPosition, spawnPosition.ToString());
            }
            GUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(!spawnerEnabled);
            if (GUILayout.Button(isSimulating ? "Simulating..." : $"Spawn & Drop ({spawnKey})"))
            {
                if (!isSimulating)
                {
                    SpawnAndDropPrefab();
                }
            }
            EditorGUI.EndDisabledGroup();
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        if (!isSimulating && e.type == EventType.KeyDown && e.keyCode == dropKey && selectedTab == 0)
        {
            BeginDropSelection();
            e.Use();
        }
        if (!isSimulating && spawnerEnabled && prefab != null && e.type == EventType.KeyDown && e.keyCode == spawnKey && selectedTab == 1)
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

    void BeginDropSelection()
    {
        isSimulating = false;
        dropRBs.Clear();
        rbPreExisting.Clear();
        colPreExisting.Clear();
        Vector3 gravityPrev = Physics.gravity;
        Physics.gravity = gravity;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (((1 << go.layer) & layerMask) == 0)
            {
                continue;
            }
            Collider existingCol = go.GetComponent<Collider>();
            bool colExisted = (existingCol != null);
            if (!colExisted)
            {
                existingCol = go.AddComponent<BoxCollider>();
                colPreExisting[existingCol] = false;
            }
            Undo.RecordObject(go.transform, "Gravity Drop");
            Rigidbody rb = go.GetComponent<Rigidbody>();
            bool existed = (rb != null);
            if (!existed)
            {
                rb = Undo.AddComponent<Rigidbody>(go);
            }
            rb.isKinematic = false;
            rbPreExisting[rb] = existed;
            dropRBs.Add(rb);
        }
        Physics.gravity = gravityPrev;
        if (dropRBs.Count > 0)
        {
            isSimulating = true;
        }
    }

    void SpawnAndDropPrefab()
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
        Collider existingCol = go.GetComponent<Collider>();
        bool colExisted = (existingCol != null);
        if (!colExisted)
        {
            existingCol = go.AddComponent<BoxCollider>();
            colPreExisting[existingCol] = false;
        }
        Rigidbody rb = go.GetComponent<Rigidbody>();
        bool rbExisted = (rb != null);
        if (!rbExisted)
        {
            rb = Undo.AddComponent<Rigidbody>(go);
        }
        if (rb == null)
        {
            Debug.LogError("Failed to add Rigidbody to the spawned prefab.");
            return;
        }
        rb.isKinematic = false;
        rbPreExisting[rb] = rbExisted;
        dropRBs.Clear();
        colPreExisting.Clear();
        dropRBs.Add(rb);
        isSimulating = true;
    }

    void EditorUpdate()
    {
        if (isSimulating)
        {
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
            if (allSleeping)
            {
                foreach (Rigidbody rb in dropRBs)
                {
                    if (rb != null && !rbPreExisting[rb])
                    {
                        Undo.DestroyObjectImmediate(rb);
                    }
                }
                foreach (KeyValuePair<Collider, bool> kv in colPreExisting)
                {
                    if (kv.Key != null && !kv.Value)
                    {
                        Undo.DestroyObjectImmediate(kv.Key);
                    }
                }
                isSimulating = false;
                SceneView.RepaintAll();
            }
        }
    }

    static Vector3 StringToVector3(string s)
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