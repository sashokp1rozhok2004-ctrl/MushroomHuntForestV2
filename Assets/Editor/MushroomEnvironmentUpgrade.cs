#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Non-destructive, repeatable environment upgrade for the original Mushroom Hunt Forest scene.
/// It builds a continuous ring of rocky terrain + a 360-degree hidden collision barrier,
/// expands ground and scatters distant vegetation so that the scene has no visible map edge.
/// CC0 textures: Poly Haven - forest_ground_01, mossy_rock, bark_brown_02.
/// </summary>
[InitializeOnLoad]
public static class MushroomEnvironmentUpgrade
{
    const string ScenePath = "Assets/Scenes/Forest.unity";
    const string EnvironmentRoot = "Environment_Expansion_V2";
    const string TextureRoot = "Assets/ThirdParty/PolyHaven";
    const string MaterialsDir = "Assets/Materials";
    const string Pending = "ProjectSettings/MushroomEnvironmentUpgradePending.txt";
    const string Ready = "ProjectSettings/MushroomEnvironmentUpgradeApplied.txt";
    const string CompleteMarker = "ProjectSettings/MushroomHuntCompleteInstalled.txt";

    static readonly string[] Trees = {
        "Assets/ImportedModels/FBX/BirchTree_1.fbx",
        "Assets/ImportedModels/FBX/BirchTree_2.fbx",
        "Assets/ImportedModels/FBX/CommonTree_1.fbx",
        "Assets/ImportedModels/FBX/PineTree_1.fbx",
        "Assets/ImportedModels/FBX/PineTree_2.fbx"
    };
    static readonly string[] Rocks = {
        "Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_Boulder.fbx",
        "Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_Big_001.fbx",
        "Assets/ImportedModels/FBX/Rock_1.fbx",
        "Assets/ImportedModels/FBX/Rock_2.fbx"
    };
    static readonly string[] Details = {
        "Assets/ImportedModels/FBX/Bush_1.fbx",
        "Assets/ImportedModels/FBX/Bush_2.fbx",
        "Assets/ImportedModels/FBX/Flowers.fbx",
        "Assets/ImportedModels/FBX/WoodLog.fbx"
    };

    static int attempt;
    static MushroomEnvironmentUpgrade() { EditorApplication.delayCall += TryAuto; }
    static void TryAuto()
    {
        if (!File.Exists(Pending) || File.Exists(Ready)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(CompleteMarker))
        {
            if (++attempt < 160) EditorApplication.delayCall += TryAuto;
            return;
        }
        try
        {
            if (Apply(false))
            {
                File.WriteAllText(Ready, "Environment V2 applied: extended ground, rock ridge, CC0 PBR materials, distant forest.\n");
                File.Delete(Pending);
            }
        }
        catch (Exception ex) { Debug.LogError("Mushroom Hunt environment auto setup: " + ex); }
    }

    [MenuItem("Mushroom Hunt/Environment/Apply forest textures and rocky boundary")]
    public static void ApplyFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Mushroom Hunt", "Stop Play Mode first.", "OK"); return;
        }
        if (!EditorUtility.DisplayDialog("Mushroom Hunt environment", 
            "Replace ONLY the generated environment expansion, keep original mushrooms and player?\n\n" +
            "A backup of Forest.unity is saved before editing.", "Apply", "Cancel")) return;
        bool success = Apply(true);
        if (success)
        {
            File.WriteAllText(Ready, "Environment V2 applied manually.\n");
            if (File.Exists(Pending)) File.Delete(Pending);
            EditorUtility.DisplayDialog("Mushroom Hunt", "Forest upgraded. Press Play to inspect the new border and ground.", "OK");
        }
    }

    [MenuItem("Mushroom Hunt/Environment/Select generated environment")]
    static void SelectGenerated()
    {
        var go = GameObject.Find(EnvironmentRoot);
        if (!go) { Debug.Log("Open Forest.unity and apply the upgrade first."); return; }
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    static Material CreatePbr(string matName, string textureBase, float tiles, Color tint, float glossiness)
    {
        if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = MaterialsDir + "/" + matName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Unlit/Texture");
            if (!shader) throw new Exception("Built-in Standard shader is unavailable in this project.");
            material = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(material, path);
        }
        string diffusePath = TextureRoot + "/" + textureBase + "_diff.jpg";
        string normalPath = TextureRoot + "/" + textureBase + "_nor_gl.jpg";
        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        if (!diffuse && textureBase == "forrest_ground_01")
            diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Ground_Diffuse.jpg");
        if (diffuse) material.SetTexture("_MainTex", diffuse);
        material.SetColor("_Color", tint);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", glossiness);
        material.SetTextureScale("_MainTex", Vector2.one * tiles);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (normal)
        {
            var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            }
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BumpMap", Vector2.one * tiles);
            material.SetFloat("_BumpScale", 0.65f);
            material.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }

    static GameObject Spawn(string path, Transform parent, string name, Vector3 position, float height, Material overrideMaterial = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) return null;
        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (!go) return null;
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        Bounds before = GetBounds(go);
        if (before.size.y < Mathf.Max(before.size.x, before.size.z) * 0.7f)
        {
            if (before.size.x > before.size.z) go.transform.rotation *= Quaternion.Euler(0f, 0f, 90f);
            else go.transform.rotation *= Quaternion.Euler(90f, 0f, 0f);
        }
        before = GetBounds(go);
        if (before.size.y > 0.02f)
        {
            float multiplier = Mathf.Clamp(height / before.size.y, 0.05f, 120f);
            go.transform.localScale *= multiplier;
        }
        Bounds after = GetBounds(go);
        go.transform.position += Vector3.up * (position.y - after.min.y);
        foreach (Collider collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (overrideMaterial != null) renderer.sharedMaterial = overrideMaterial;
        }
        return go;
    }

    static Mesh CreateContinuousRockRidge()
    {
        const int angles = 192;
        float[] radii = { 70.2f, 72.4f, 74.3f, 76.7f, 80.8f, 91.0f };
        float[] heights = { 0.2f, 2.5f, 8.0f, 9.7f, 3.6f, 0.05f };
        int rows = radii.Length;
        var verts = new Vector3[(angles + 1) * rows];
        var uv = new Vector2[verts.Length];
        var tris = new int[angles * (rows - 1) * 6];
        for (int ring = 0; ring < rows; ring++)
        for (int i = 0; i <= angles; i++)
        {
            int j = i % angles;
            float a = j * Mathf.PI * 2f / angles;
            float variation = 0.68f * Mathf.Sin(a * 11 + 0.3f) + 0.39f * Mathf.Sin(a * 23 - 1.1f)
                              + 0.22f * Mathf.Sin(a * 43 + 2.1f);
            float r = radii[ring] + variation * (ring == 0 ? 0.15f : 0.9f);
            float h = heights[ring] + (ring > 0 && ring < rows - 1 ? 1.5f * Mathf.Sin(a * 7 + ring * 0.7f) : 0f);
            if (ring == 2 || ring == 3) h += 1.6f * Mathf.Sin(a * 16 + ring * 1.1f);
            int k = ring * (angles + 1) + i;
            verts[k] = new Vector3(Mathf.Cos(a) * r, Mathf.Max(-0.1f, h), Mathf.Sin(a) * r);
            uv[k] = new Vector2(i / 3f, ring * 0.55f);
        }
        int t = 0;
        for (int ring = 0; ring < rows - 1; ring++)
        for (int i = 0; i < angles; i++)
        {
            int a = ring * (angles + 1) + i;
            int b = a + 1;
            int c = (ring + 1) * (angles + 1) + i;
            int d = c + 1;
            tris[t++] = a; tris[t++] = b; tris[t++] = c;
            tris[t++] = b; tris[t++] = d; tris[t++] = c;
        }
        Mesh mesh = new Mesh { name = "MushroomRidge_ContinuousRockMesh" };
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static int AddRockyBoundary(Transform parent, Material rock)
    {
        Transform root = new GameObject("RockyRidge_Seamless360").transform;
        root.SetParent(parent, false);
        GameObject meshGo = new GameObject("ContinuousCliff_Visual");
        meshGo.transform.SetParent(root, false);
        string meshPath = MaterialsDir + "/Generated_MushroomRidge_Mesh.asset";
        Mesh cliff = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (!cliff)
        {
            cliff = CreateContinuousRockRidge();
            AssetDatabase.CreateAsset(cliff, meshPath); // keep the mesh when Unity reloads the scene
        }
        meshGo.AddComponent<MeshFilter>().sharedMesh = cliff;
        meshGo.AddComponent<MeshRenderer>().sharedMaterial = rock;
        // Physical blocker is intentionally separate from the visual cliff mesh:
        // 96 overlapping segments avoid gaps even when decorative rocks are uneven.
        const int segments = 96;
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / segments;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            GameObject piece = new GameObject("RockBarrier_Collider_" + i.ToString("D2"));
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = radial * 75.0f + Vector3.up * 4f;
            piece.transform.localRotation = Quaternion.LookRotation(radial, Vector3.up);
            BoxCollider collider = piece.AddComponent<BoxCollider>();
            collider.size = new Vector3(8.7f, 17f, 8.0f);
            collider.isTrigger = false;
        }
        // Break up the regular silhouette with varied existing rock models.
        int count = 0;
        for (int i = 0; i < 80; i++)
        {
            float a = (i + UnityEngine.Random.Range(-0.33f, 0.33f)) * Mathf.PI * 2f / 80f;
            float r = UnityEngine.Random.Range(74f, 81f);
            float h = UnityEngine.Random.Range(4.6f, 9.4f);
            string rockPath = Rocks[UnityEngine.Random.Range(0, Rocks.Length)];
            if (Spawn(rockPath, root, "MossyRockBorder_" + i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), h, rock)) count++;
        }
        return count;
    }

    static void AddForestBeyondCliffs(Transform parent, Material rock, Material bark)
    {
        var forest = new GameObject("ForestBeyondTheCliffs").transform;
        forest.SetParent(parent, false);
        for (int i = 0; i < 260; i++)
        {
            // Maintain strong density right outside the cliff and progressively fade
            // the forest into fog, instead of leaving a blank world around the play area.
            float r = i < 140 ? UnityEngine.Random.Range(86f, 137f) : UnityEngine.Random.Range(137f, 237f);
            float a = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
            Vector3 pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            string tree = Trees[UnityEngine.Random.Range(0, Trees.Length)];
            GameObject go = Spawn(tree, forest, "DistantTree_" + i, pos, UnityEngine.Random.Range(8.5f, 15f));
            if (go && bark)
            {
                // Never apply bark to leaf meshes: they would turn brown.
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    string materialName = renderer.sharedMaterial ? renderer.sharedMaterial.name.ToLowerInvariant() : "";
                    string meshName = renderer.gameObject.name.ToLowerInvariant();
                    if (materialName.Contains("bark") || materialName.Contains("trunk") ||
                        materialName.Contains("wood") || meshName.Contains("trunk"))
                        renderer.sharedMaterial = bark;
                }
            }
        }
        for (int i = 0; i < 88; i++)
        {
            float r = UnityEngine.Random.Range(89f, 220f);
            float a = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
            Spawn(Rocks[UnityEngine.Random.Range(0, Rocks.Length)], forest, "DistantStone_" + i,
                new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), UnityEngine.Random.Range(1.2f, 5f), rock);
        }
        for (int i = 0; i < 115; i++)
        {
            float r = UnityEngine.Random.Range(85f, 215f);
            float a = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
            Spawn(Details[UnityEngine.Random.Range(0, Details.Length)], forest, "DistantUndergrowth_" + i,
                new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), UnityEngine.Random.Range(0.5f, 2.0f));
        }
    }

    static void UpgradeExistingNature(Material rock, Material bark)
    {
        GameObject existingForest = GameObject.Find("ImportedForest");
        if (!existingForest) return;
        foreach (Transform t in existingForest.GetComponentsInChildren<Transform>(true))
        {
            if (rock && t.name == "Rock")
            {
                foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = rock;
            }
            if (bark && t.name == "Tree")
            {
                foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>(true))
                {
                    string mat = renderer.sharedMaterial ? renderer.sharedMaterial.name.ToLowerInvariant() : "";
                    string name = renderer.name.ToLowerInvariant();
                    if (mat.Contains("bark") || mat.Contains("wood") || mat.Contains("trunk") || name.Contains("trunk"))
                        renderer.sharedMaterial = bark;
                }
            }
        }
    }

    public static bool Apply(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (!File.Exists(ScenePath)) { Debug.LogError("Forest.unity was not found."); return false; }

        // The .bak extension prevents Unity from accidentally treating the copy as another scene.
        string sceneBackup = "Assets/Scenes/Forest_before_EnvironmentV2.unity.bak";
        if (!File.Exists(sceneBackup)) File.Copy(ScenePath, sceneBackup, false);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var ground = GameObject.Find("Ground");
        var groundPhysics = GameObject.Find("GroundPhysics");
        if (!ground || !groundPhysics)
        {
            Debug.LogError("Environment upgrade: expected Ground and GroundPhysics from original Forest.unity.");
            return false;
        }

        // Never duplicate previously created terrain. All original trees and mushrooms remain.
        GameObject previous = GameObject.Find(EnvironmentRoot);
        if (previous) Object.DestroyImmediate(previous);

        Material groundMat = CreatePbr("ForestFloor_PolyHaven_PBR", "forrest_ground_01", 82f,
            new Color(0.91f, 0.96f, 0.91f, 1f), 0.1f);
        Material rockMat = CreatePbr("MossyRock_PolyHaven_PBR", "mossy_rock", 1.9f,
            new Color(0.94f, 0.96f, 0.93f, 1f), 0.16f);
        Material barkMat = CreatePbr("TreeBark_PolyHaven_PBR", "bark_brown_02", 1f,
            new Color(0.9f, 0.92f, 0.88f, 1f), 0.1f);

        // Original plane is 250m across; a 560m floor now extends well past visible trees.
        ground.transform.localScale = new Vector3(56f, 1f, 56f);
        foreach (Renderer renderer in ground.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = groundMat;
        foreach (BoxCollider collider in groundPhysics.GetComponents<BoxCollider>())
        {
            collider.size = new Vector3(600f, 2f, 600f);
            collider.center = new Vector3(0f, -1f, 0f);
            collider.isTrigger = false;
        }
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        if (player) { player.playableHalfSize = 500f; EditorUtility.SetDirty(player); }

        var oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(2140915);
        var environment = new GameObject(EnvironmentRoot).transform;
        int rockCount = AddRockyBoundary(environment, rockMat);
        AddForestBeyondCliffs(environment, rockMat, barkMat);
        UnityEngine.Random.state = oldState;

        UpgradeExistingNature(rockMat, barkMat);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.43f, 0.51f, 0.46f, 1f);
        RenderSettings.fogStartDistance = 85f;
        RenderSettings.fogEndDistance = 235f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.51f, 0.45f, 1f);
        GameObject sun = GameObject.Find("Sun");
        if (sun)
        {
            Light light = sun.GetComponent<Light>();
            if (light)
            {
                light.intensity = 1.1f;
                light.shadowStrength = 0.65f;
                light.shadows = LightShadows.Soft;
            }
        }
        var camera = Object.FindFirstObjectByType<Camera>();
        if (camera) { camera.farClipPlane = 350f; EditorUtility.SetDirty(camera); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MUSHROOM HUNT ENVIRONMENT V2: ground PBR, improved existing stone materials, " +
            rockCount + " cliff boulders, 96 overlapping solid wall colliders, 260 distant trees, 88 rocks, " +
            "115 undergrowth instances. Original player/mushroom positions preserved. Backup: " + sceneBackup);
        return true;
    }
}
#endif
