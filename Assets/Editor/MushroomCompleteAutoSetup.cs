#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// First-launch integration for the Mushroom Hunt Complete distribution.
/// Keeps the user's original Forest.unity layout; changes only the player, HUD
/// and collectible configuration. The imported CC0 FBX is already present after
/// SETUP_PROJECT.bat completes, so Unity does not need internet access.
/// </summary>
[InitializeOnLoad]
public static class MushroomCompleteAutoSetup
{
    const string ScenePath = "Assets/Scenes/Forest.unity";
    const string ArmsPrefabPath = "Assets/ThirdParty/WRADArms/arms.fbx";
    const string SkinPath = "Assets/ThirdParty/WRADArms/arm_albedo_pale.png";
    const string MaterialPath = "Assets/ThirdParty/WRADArms/MushroomWRADSkin.mat";
    const string MarkerPath = "ProjectSettings/MushroomHuntCompleteInstalled.txt";

    private static int attempts;

    static MushroomCompleteAutoSetup()
    {
        EditorApplication.delayCall += TryFirstSetup;
    }

    private static void TryFirstSetup()
    {
        if (File.Exists(MarkerPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (++attempts < 100) EditorApplication.delayCall += TryFirstSetup;
            return;
        }
        if (!File.Exists(ScenePath) || !File.Exists(ArmsPrefabPath) || !File.Exists(SkinPath))
        {
            Debug.LogWarning("Mushroom Hunt Complete: missing original scene or WRAD files. Run SETUP_PROJECT.bat again.");
            return;
        }
        try
        {
            bool success = ApplySetup(false);
            if (success)
            {
                File.WriteAllText(MarkerPath,
                    "Mushroom Hunt Complete v1; original scene preserved; 5 mushrooms; WRAD arms installed.\n");
                // The optional Environment V2 initializer observes this marker on the next editor update.
                // It applies the forest/rock expansion only after hands setup has saved Forest.unity.
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("Mushroom Hunt Complete: automatic scene setup failed. Use menu Mushroom Hunt > Complete > Reapply setup.\n" + exception);
        }
    }

    [MenuItem("Mushroom Hunt/Complete/Reapply setup to Forest scene")]
    public static void ReapplyFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Mushroom Hunt", "Exit Play Mode first.", "OK");
            return;
        }
        if (ApplySetup(true))
        {
            File.WriteAllText(MarkerPath, "Mushroom Hunt Complete setup manually reapplied.\n");
            EditorUtility.DisplayDialog("Mushroom Hunt", "Forest scene configured. You can press Play.", "OK");
        }
    }

    public static bool ApplySetup(bool verbose)
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("Mushroom Hunt: Forest.unity was not found.");
            return false;
        }
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isDirty && !string.IsNullOrEmpty(active.path) && active.path != ScenePath)
        {
            Debug.LogWarning("Mushroom Hunt: save the modified active scene before installing the project.");
            return false;
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var game = UnityEngine.Object.FindFirstObjectByType<MushroomGame>();
        var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>();
        if (!game || !player)
        {
            Debug.LogError("Mushroom Hunt: the original Player or MushroomGame is missing from Forest.unity.");
            return false;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefabPath);
        Texture2D skin = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinPath);
        if (!prefab || !skin)
        {
            Debug.LogError("Mushroom Hunt: WRAD FBX / skin failed to import. Inspect Assets/ThirdParty/WRADArms.");
            return false;
        }
        Camera camera = game.view ? game.view : player.GetComponentInChildren<Camera>(true);
        if (!camera) { Debug.LogError("Mushroom Hunt: first-person camera missing."); return false; }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Unlit/Texture");
            if (!shader) { Debug.LogError("Mushroom Hunt: built-in material shaders unavailable."); return false; }
            material = new Material(shader) { name = "MushroomWRADSkin" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.mainTexture = skin;
        material.color = Color.white;
        EditorUtility.SetDirty(material);

        Transform root = camera.transform.Find("WRAD_Viewmodel");
        if (!root)
        {
            root = new GameObject("WRAD_Viewmodel").transform;
            root.SetParent(camera.transform, false);
        }
        Transform current = root.Find("WRAD_Arms_Model");
        if (!current)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
            if (!instance)
            {
                Debug.LogError("Mushroom Hunt: could not instantiate WRAD arms FBX.");
                return false;
            }
            instance.name = "WRAD_Arms_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned) skinned.updateWhenOffscreen = true;
            }
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                if (bounds.size.x > 0.001f)
                {
                    float scale = Mathf.Clamp(1.25f / bounds.size.x, 0.01f, 25f);
                    instance.transform.localScale = Vector3.one * scale;
                    bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    Vector3 localCenter = root.InverseTransformPoint(bounds.center);
                    instance.transform.localPosition = new Vector3(0f, -0.30f, 0.90f) - localCenter;
                }
            }
        }

        camera.nearClipPlane = 0.015f;
        EditorUtility.SetDirty(camera);
        Transform oldModel = FindChild(camera.transform.parent, "Hands");
        if (oldModel)
            foreach (Renderer oldRenderer in oldModel.GetComponentsInChildren<Renderer>(true))
                oldRenderer.enabled = false;

        FirstPersonHands hands = player.GetComponent<FirstPersonHands>();
        if (!hands) hands = player.gameObject.AddComponent<FirstPersonHands>();
        hands.viewmodelRoot = root;
        hands.showHands = true;
        EditorUtility.SetDirty(hands);

        game.target = 5;
        game.collected = 0;
        game.view = camera;
        game.hands = hands;
        player.game = game;
        if (!player.head) player.head = camera.transform.parent ? camera.transform.parent : camera.transform;
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(player);

        int mushrooms = 0;
        foreach (var pickup in UnityEngine.Object.FindObjectsByType<MushroomPickup>(FindObjectsSortMode.None))
        {
            mushrooms++;
            if (pickup.GetComponentInChildren<Collider>(true)) continue;
            SphereCollider trigger = pickup.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            trigger.center = new Vector3(0f, 0.35f, 0f);
        }
        if (mushrooms < 5) Debug.LogWarning("Mushroom Hunt: fewer than 5 mushroom objects found in the scene.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MUSHROOM HUNT COMPLETE: Forest, 5-mushroom objective, WRAD arms, pickup and build menu configured. Found " + mushrooms + " mushrooms.");
        return true;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (!root) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChild(child, name);
            if (found) return found;
        }
        return null;
    }
}
#endif
