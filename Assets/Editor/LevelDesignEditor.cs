using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class LevelDesignEditor : EditorWindow
{
    private Vector2 scrollPosition;
    private List<GameObject> prefabs = new List<GameObject>();
    private GameObject selectedPrefab;

    public enum EditorToolMode { Placement, VertexPaint }
    public enum PaintChannel { Grass_Black, Dirt_Red, Rock_Green, Sand_Blue }

    private EditorToolMode toolMode = EditorToolMode.Placement;

    // Paint Variables
    private PaintChannel selectedChannel = PaintChannel.Dirt_Red;
    private float paintRadius = 3f;
    private float paintStrength = 0.5f;

    // Feature 1: Random Scale & Y-Rotation
    private bool useRandomRotation = true;
    private float customYRotation = 0f;
    private bool useRandomScale = false;
    private Vector2 scaleRange = new Vector2(0.8f, 1.2f);

    // Feature 5: Grid Snapping & Y-Offset
    private bool useGridSnap = false;
    private float gridSize = 1f;
    private float yOffset = 0f;

    // Chunk System Variables
    private Transform chunkRoot;
    private bool showChunkBounds = true;
    private float chunkSize = 50f;

    // Preview System
    private GameObject previewObject;
    private GameObject lastSelectedPrefab;

    [MenuItem("Tools/Level Design Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelDesignEditor>("Level Design Editor");
    }

    private void OnEnable()
    {
        LoadPrefabs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyPreview();
    }

    private void OnDestroy()
    {
        DestroyPreview();
    }

    private void DestroyPreview()
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
            previewObject = null;
        }
    }

    private void LoadPrefabs()
    {
        prefabs.Clear();
        string folderPath = "Assets/Polyart/PolyartStudio/DreamscapeMeadows/Prefabs";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Polyart Level Design Editor", EditorStyles.boldLabel);

        if (GUILayout.Button("Reload Prefabs"))
        {
            LoadPrefabs();
        }

        GUILayout.Space(10);
        toolMode = (EditorToolMode)GUILayout.Toolbar((int)toolMode, new string[] { "Object Placement", "Vertex Paint" });
        GUILayout.Space(5);

        if (toolMode == EditorToolMode.Placement)
        {
            GUILayout.Label("Placement Settings", EditorStyles.boldLabel);
            
            useRandomRotation = EditorGUILayout.Toggle("Random Y Rotation", useRandomRotation);
            if (!useRandomRotation)
            {
                customYRotation = EditorGUILayout.FloatField("Custom Y Rotation", customYRotation);
            }
            
            useRandomScale = EditorGUILayout.Toggle("Random Scale", useRandomScale);
            if (useRandomScale)
            {
                scaleRange = EditorGUILayout.Vector2Field("Scale Range (Min/Max)", scaleRange);
            }

            GUILayout.Space(5);
            useGridSnap = EditorGUILayout.Toggle("Enable Grid Snapping", useGridSnap);
            if (useGridSnap)
            {
                gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
                if (gridSize <= 0.1f) gridSize = 0.1f;
            }
            yOffset = EditorGUILayout.FloatField("Y Offset", yOffset);
        }
        else if (toolMode == EditorToolMode.VertexPaint)
        {
            GUILayout.Label("Vertex Paint Settings", EditorStyles.boldLabel);
            selectedChannel = (PaintChannel)EditorGUILayout.EnumPopup("Paint Texture", selectedChannel);
            paintRadius = EditorGUILayout.Slider("Brush Radius", paintRadius, 0.5f, 20f);
            paintStrength = EditorGUILayout.Slider("Brush Strength", paintStrength, 0.01f, 1f);
            
            EditorGUILayout.HelpBox("Select Chunk Root to paint on it. Modifies vertex colors of the Mesh. Ensure your material uses a Vertex Color blending shader.", MessageType.Info);
        }

        GUILayout.Space(10);
        GUILayout.Label("Chunk System (Auto Parenting)", EditorStyles.boldLabel);
        chunkRoot = (Transform)EditorGUILayout.ObjectField("Chunk Root", chunkRoot, typeof(Transform), true);
        showChunkBounds = EditorGUILayout.Toggle("Show Chunk Bounds", showChunkBounds);
        if (showChunkBounds)
        {
            chunkSize = EditorGUILayout.FloatField("Chunk Size", chunkSize);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create New Chunk"))
        {
            CreateNewChunk();
        }
        if (GUILayout.Button("Save Chunk as Prefab"))
        {
            SaveChunkAsPrefab();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Selected Prefab:", EditorStyles.label);

        if (selectedPrefab != null)
        {
            GUILayout.Label(selectedPrefab.name, EditorStyles.helpBox);
        }
        else
        {
            GUILayout.Label("None", EditorStyles.helpBox);
        }

        GUILayout.Space(10);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        
        int rowCount = 3;
        int i = 0;
        
        GUILayout.BeginHorizontal();
        foreach (var prefab in prefabs)
        {
            if (i > 0 && i % rowCount == 0)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }

            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            if (GUILayout.Button(new GUIContent(preview, prefab.name), GUILayout.Width(80), GUILayout.Height(80)))
            {
                selectedPrefab = prefab;
            }
            i++;
        }
        GUILayout.EndHorizontal();
        
        GUILayout.EndScrollView();
    }

    private void CreateNewChunk()
    {
        GameObject newChunk = new GameObject("Chunk_New");
        
        GameObject plane = new GameObject("GroundPlane");
        MeshFilter mf = plane.AddComponent<MeshFilter>();
        MeshRenderer mr = plane.AddComponent<MeshRenderer>();
        plane.AddComponent<MeshCollider>();

        // Generate High-Res Mesh for Vertex Painting (1 vertex per meter approx)
        int segments = Mathf.CeilToInt(chunkSize);
        Mesh highResMesh = GenerateHighResPlane(chunkSize, segments);
        mf.sharedMesh = highResMesh;
        plane.GetComponent<MeshCollider>().sharedMesh = highResMesh;

        // Generates or loads the Custom Vertex Splat Material
        Material terrainMat = LoadOrCreateSplatMaterial();
            
        mr.sharedMaterial = terrainMat;

        plane.transform.SetParent(newChunk.transform);
        plane.transform.localPosition = Vector3.zero;
        
        chunkRoot = newChunk.transform;
        Undo.RegisterCreatedObjectUndo(newChunk, "Create New Chunk");
    }

    private Mesh GenerateHighResPlane(float size, int segments)
    {
        Mesh mesh = new Mesh { name = "ChunkPlaneMesh" };
        int vCount = (segments + 1) * (segments + 1);
        Vector3[] vertices = new Vector3[vCount];
        Vector2[] uvs = new Vector2[vCount];
        Color[] colors = new Color[vCount];
        int[] triangles = new int[segments * segments * 6];

        float halfSize = size * 0.5f;
        float segmentSize = size / segments;

        int v = 0;
        for (int z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++)
            {
                vertices[v] = new Vector3(x * segmentSize - halfSize, 0, z * segmentSize - halfSize);
                uvs[v] = new Vector2((float)x / segments, (float)z / segments);
                colors[v] = new Color(0, 0, 0, 0); // Base texture (all channels 0)
                v++;
            }
        }

        int t = 0;
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int i = x + z * (segments + 1);
                triangles[t++] = i;
                triangles[t++] = i + (segments + 1);
                triangles[t++] = i + 1;
                
                triangles[t++] = i + 1;
                triangles[t++] = i + (segments + 1);
                triangles[t++] = i + (segments + 1) + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material LoadOrCreateSplatMaterial()
    {
        string matPath = "Assets/GameAssets/Materials/M_CustomVertexTerrain.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader splatShader = Shader.Find("Custom/URP_VertexSplatTerrain");
            if (splatShader == null)
            {
                Debug.LogError("Could not find Custom/URP_VertexSplatTerrain shader! Make sure the shader file exists.");
                return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            }

            mat = new Material(splatShader);
            
            // Try to assign Polyart Textures automatically
            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polyart/PolyartStudio/DreamscapeMeadows/Textures/Terrain/T_Grass_Ground_C.png");
            Texture2D dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polyart/PolyartStudio/DreamscapeMeadows/Textures/Terrain/T_DirtGround_C.png");
            Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polyart/PolyartStudio/DreamscapeMeadows/Textures/Terrain/T_Cobblestone_C.png");
            Texture2D sandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polyart/PolyartStudio/DreamscapeMeadows/Textures/Terrain/T_SandGround_C.tga");

            // Also try alternative fallback names if not found
            if (rockTex == null) rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polyart/PolyartStudio/DreamscapeMeadows/Textures/Terrain/T_StoneGround_C.tga");

            if (grassTex) mat.SetTexture("_MainTex", grassTex);
            if (dirtTex) mat.SetTexture("_RedTex", dirtTex);
            if (rockTex) mat.SetTexture("_GreenTex", rockTex);
            if (sandTex) mat.SetTexture("_BlueTex", sandTex);

            mat.SetFloat("_Tiling", 15f);

            if (!AssetDatabase.IsValidFolder("Assets/GameAssets/Materials"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GameAssets")) AssetDatabase.CreateFolder("Assets", "GameAssets");
                AssetDatabase.CreateFolder("Assets/GameAssets", "Materials");
            }

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }
        return mat;
    }

    private void SaveChunkAsPrefab()
    {
        if (chunkRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "No Chunk Root selected to save.", "OK");
            return;
        }

        string savePath = "Assets/GameAssets/Prefabs/Chunks";
        if (!AssetDatabase.IsValidFolder("Assets/GameAssets")) AssetDatabase.CreateFolder("Assets", "GameAssets");
        if (!AssetDatabase.IsValidFolder("Assets/GameAssets/Prefabs")) AssetDatabase.CreateFolder("Assets/GameAssets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/GameAssets/Prefabs/Chunks")) AssetDatabase.CreateFolder("Assets/GameAssets/Prefabs", "Chunks");

        // Save customized meshes so they don't break in the prefab
        string meshDir = $"{savePath}/Meshes";
        if (!AssetDatabase.IsValidFolder(meshDir)) AssetDatabase.CreateFolder(savePath, "Meshes");

        MeshFilter[] mfs = chunkRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("ChunkPlaneMesh"))
            {
                // Save this specific mesh as an asset since it contains unique vertex color data
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshDir}/{chunkRoot.name}_{mf.gameObject.name}_mesh.asset");
                AssetDatabase.CreateAsset(mf.sharedMesh, meshPath);
            }
        }

        string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/{chunkRoot.name}.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(chunkRoot.gameObject, fullPath, InteractionMode.UserAction);
        Debug.Log($"Chunk saved to {fullPath}");
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (showChunkBounds && chunkRoot != null)
        {
            Handles.color = Color.green;
            Vector3 center = chunkRoot.position;
            Vector3 size = new Vector3(chunkSize, 50f, chunkSize);
            Handles.DrawWireCube(center, size);
        }

        if (toolMode == EditorToolMode.Placement)
        {
            if (selectedPrefab == null)
            {
                DestroyPreview();
                return;
            }

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }

            // --- Preview Logic ---
            if (previewObject == null || selectedPrefab != lastSelectedPrefab)
            {
                DestroyPreview();
                previewObject = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
                previewObject.hideFlags = HideFlags.HideAndDontSave;
                
                // Disable colliders so it doesn't block rays
                foreach (var col in previewObject.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
                lastSelectedPrefab = selectedPrefab;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 placePos = Vector3.zero;
            bool didHit = false;

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                placePos = hit.point;
                didHit = true;
            }
            else
            {
                Plane mathPlane = new Plane(Vector3.up, Vector3.zero);
                if (mathPlane.Raycast(ray, out float enter))
                {
                    placePos = ray.GetPoint(enter);
                    didHit = true;
                }
            }

            if (didHit)
            {
                // Update Preview Transform
                Vector3 previewPos = placePos;
                if (useGridSnap)
                {
                    previewPos.x = Mathf.Round(previewPos.x / gridSize) * gridSize;
                    previewPos.z = Mathf.Round(previewPos.z / gridSize) * gridSize;
                }
                previewPos.y += yOffset;
                previewObject.transform.position = previewPos;

                if (!useRandomRotation)
                {
                    previewObject.transform.rotation = Quaternion.Euler(0, customYRotation, 0);
                }

                // Force repaint to make the preview follow mouse smoothly
                if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
                {
                    sceneView.Repaint();
                }
            }

            // --- Placement Logic ---
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && didHit)
            {
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
                
                if (useGridSnap)
                {
                    placePos.x = Mathf.Round(placePos.x / gridSize) * gridSize;
                    placePos.z = Mathf.Round(placePos.z / gridSize) * gridSize;
                }
                placePos.y += yOffset;
                
                newObj.transform.position = placePos;

                if (useRandomRotation)
                {
                    newObj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                }
                else
                {
                    newObj.transform.rotation = Quaternion.Euler(0, customYRotation, 0);
                }

                if (useRandomScale)
                {
                    float randScale = Random.Range(scaleRange.x, scaleRange.y);
                    newObj.transform.localScale = new Vector3(randScale, randScale, randScale);
                }

                if (chunkRoot != null)
                {
                    newObj.transform.SetParent(chunkRoot);
                }

                Undo.RegisterCreatedObjectUndo(newObj, "Place Object");
                e.Use();
            }
        }
        else if (toolMode == EditorToolMode.VertexPaint)
        {
            DestroyPreview();
            
            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            // Only raycast against colliders for painting
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Draw brush cursor
                Handles.color = new Color(0f, 1f, 1f, 0.5f);
                
                // Offset slightly to prevent z-fighting
                Handles.DrawWireDisc(hit.point + hit.normal * 0.05f, hit.normal, paintRadius);

                sceneView.Repaint();

                if ((e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0 && !e.alt)
                {
                    PaintVertices(hit.collider.gameObject, hit.point);
                    e.Use();
                }
            }
        }
    }

    private void PaintVertices(GameObject hitObject, Vector3 hitPoint)
    {
        MeshFilter mf = hitObject.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        
        // Ensure chunk root relationship if a chunk root is selected
        if (chunkRoot != null && !hitObject.transform.IsChildOf(chunkRoot) && hitObject.transform != chunkRoot)
            return;

        // Clone mesh if it's not our custom generated dynamic mesh instance yet
        Mesh mesh = mf.sharedMesh;
        if (!mesh.name.Contains("ChunkPlaneMesh_Instance"))
        {
            if (!mesh.isReadable) {
                Debug.LogWarning($"Mesh {mesh.name} is not readable. Cannot paint vertices.");
                return;
            }
            mesh = Instantiate(mf.sharedMesh);
            mesh.name = "ChunkPlaneMesh_Instance";
            mf.sharedMesh = mesh;
        }

        Vector3[] vertices = mesh.vertices;
        Color[] colors = mesh.colors;
        if (colors == null || colors.Length != vertices.Length)
        {
            colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.black;
        }

        bool wasModified = false;
        Color targetColor = GetTargetColor(selectedChannel);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = hitObject.transform.TransformPoint(vertices[i]);
            float dist = Vector3.Distance(worldPos, hitPoint);
            
            if (dist < paintRadius)
            {
                float falloff = 1f - (dist / paintRadius); // basic linear falloff
                
                // Allow stronger accumulation on drag
                float weight = Mathf.Clamp01(falloff * paintStrength * 0.5f);

                if (selectedChannel == PaintChannel.Grass_Black)
                {
                    // Erase towards black
                    colors[i] = Color.Lerp(colors[i], Color.black, weight);
                }
                else
                {
                    // For R, G, B, we want to maximize the target channel and reduce others depending on blend implementation
                    float r = targetColor.r > 0 ? Mathf.Lerp(colors[i].r, 1f, weight) : colors[i].r;
                    float g = targetColor.g > 0 ? Mathf.Lerp(colors[i].g, 1f, weight) : colors[i].g;
                    float b = targetColor.b > 0 ? Mathf.Lerp(colors[i].b, 1f, weight) : colors[i].b;
                    
                    colors[i] = new Color(r, g, b, colors[i].a);
                }
                wasModified = true;
            }
        }

        if (wasModified)
        {
            Undo.RecordObject(hitObject.GetComponent<MeshRenderer>(), "Vertex Paint"); // register undo
            mesh.colors = colors;
            EditorUtility.SetDirty(mesh);
            hitObject.GetComponent<MeshFilter>().sharedMesh = mesh; // ensure it updates
        }
    }

    private Color GetTargetColor(PaintChannel channel)
    {
        switch (channel)
        {
            case PaintChannel.Dirt_Red: return Color.red;
            case PaintChannel.Rock_Green: return Color.green;
            case PaintChannel.Sand_Blue: return Color.blue;
            default: return Color.black;
        }
    }
}
