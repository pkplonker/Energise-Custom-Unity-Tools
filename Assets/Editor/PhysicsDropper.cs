using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class GravitySceneViewWindow : EditorWindow
{
    const string PrefFixedDt = "GravitySceneView_fixedDt";
    const string PrefDropKey = "GravitySceneView_dropKey";
    const string PrefGravityX = "GravitySceneView_gravityX";
    const string PrefGravityY = "GravitySceneView_gravityY";
    const string PrefGravityZ = "GravitySceneView_gravityZ";
    const string PrefLayerMask = "GravitySceneView_layerMask";
    const string PrefPrefabGuid = "GravitySceneView_prefabGuid";
    const string PrefSpawnPosition = "GravitySceneView_spawnPos";

    float fixedDt;
    KeyCode dropKey;
    Vector3 gravity;
    LayerMask layerMask;

    GameObject prefab;
    Vector3 spawnPosition;
    Quaternion spawnRotation = Quaternion.identity;

    bool autoSimulationPrev;
    bool isSimulating;
    List<Rigidbody> dropRBs = new List<Rigidbody>();
    Dictionary<Rigidbody, bool> rbPreExisting = new Dictionary<Rigidbody, bool>();

    [MenuItem("Window/Gravity Scene View")]
    public static void ShowWindow()
    {
        GetWindow<GravitySceneViewWindow>("Gravity Scene View");
    }

    void OnEnable()
    {
        fixedDt = EditorPrefs.GetFloat(PrefFixedDt, 0.02f);
        dropKey = (KeyCode)EditorPrefs.GetInt(PrefDropKey, (int)KeyCode.D);
        gravity = new Vector3(
            EditorPrefs.GetFloat(PrefGravityX, Physics.gravity.x),
            EditorPrefs.GetFloat(PrefGravityY, Physics.gravity.y),
            EditorPrefs.GetFloat(PrefGravityZ, Physics.gravity.z)
        );
        layerMask = EditorPrefs.GetInt(PrefLayerMask, ~0);
        string guid = EditorPrefs.GetString(PrefPrefabGuid, "");
        if (!string.IsNullOrEmpty(guid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }
        spawnPosition = StringToVector3(EditorPrefs.GetString(PrefSpawnPosition, Vector3.zero.ToString()));
        autoSimulationPrev = Physics.autoSimulation;
        Physics.autoSimulation = false;
        EditorApplication.update += EditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        Physics.autoSimulation = autoSimulationPrev;
        EditorApplication.update -= EditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        GUILayout.Label("Simulation Settings", EditorStyles.boldLabel);
        fixedDt = EditorGUILayout.FloatField("Fixed Timestep", fixedDt);
        dropKey = (KeyCode)EditorGUILayout.EnumPopup("Drop Key", dropKey);
        gravity = EditorGUILayout.Vector3Field("Gravity", gravity);
        int mask = EditorGUILayout.MaskField("Layer Mask", layerMask, InternalEditorUtility.layers);
        layerMask = mask;
        EditorGUILayout.Space();
        GUILayout.Label("Prefab Spawner", EditorStyles.boldLabel);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        spawnPosition = EditorGUILayout.Vector3Field("Spawn Position", spawnPosition);
        spawnRotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Spawn Rotation", spawnRotation.eulerAngles));
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetFloat(PrefFixedDt, fixedDt);
            EditorPrefs.SetInt(PrefDropKey, (int)dropKey);
            EditorPrefs.SetFloat(PrefGravityX, gravity.x);
            EditorPrefs.SetFloat(PrefGravityY, gravity.y);
            EditorPrefs.SetFloat(PrefGravityZ, gravity.z);
            EditorPrefs.SetInt(PrefLayerMask, layerMask);
            if (prefab != null)
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                string prefGuid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString(PrefPrefabGuid, prefGuid);
            }
            EditorPrefs.SetString(PrefSpawnPosition, spawnPosition.ToString());
        }
        EditorGUILayout.Space();
        if (GUILayout.Button(isSimulating ? "Simulating..." : $"Drop Selected ({dropKey})"))
        {
            if (!isSimulating)
            {
                BeginDropSelection();
            }
        }
        if (prefab != null && GUILayout.Button("Spawn & Drop Prefab"))
        {
            SpawnAndDropPrefab();
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        if (!isSimulating && e.type == EventType.KeyDown && e.keyCode == dropKey)
        {
            BeginDropSelection();
            e.Use();
        }
        if (prefab != null)
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
        Vector3 gravityPrev = Physics.gravity;
        Physics.gravity = gravity;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (((1 << go.layer) & layerMask) == 0)
            {
                continue;
            }
            Collider col = go.GetComponent<Collider>();
            if (col == null)
            {
                continue;
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
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Spawn Prefab");
        go.transform.position = spawnPosition;
        go.transform.rotation = spawnRotation;
        if (go.GetComponent<Collider>() == null)
        {
            go.AddComponent<BoxCollider>();
        }
        Rigidbody rb = Undo.AddComponent<Rigidbody>(go);
        rb.isKinematic = false;
        dropRBs.Clear();
        rbPreExisting.Clear();
        rbPreExisting[rb] = false;
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
