#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

/// <summary>
/// One-click installer of an independently published CC0 first-person viewmodel.
/// Asset source: https://github.com/wwwriks/wrad-arms by wriks, CC0-1.0.
/// The FBX and texture are downloaded by Unity Editor into this local project;
/// no downloads or asset redistribution happen at runtime/in the built game.
/// </summary>
public static class WRADHandsSetup
{
    const string Source = "https://raw.githubusercontent.com/wwwriks/wrad-arms/main/";
    const string Folder = "Assets/ThirdParty/WRADArms";
    const string ModelPath = Folder + "/arms.fbx";
    const string TexturePath = Folder + "/arm_albedo_pale.png";
    const string MaterialPath = Folder + "/WRADSkin.mat";

    [MenuItem("Mushroom Hunt/Hands/1. Download and install WRAD arms (CC0)")]
    public static async void DownloadAndInstall()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("WRAD Arms", "Stop Play Mode first.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("WRAD Arms — CC0",
            "Download free WRAD Arms (FBX + skin texture) from the author's public GitHub and install them on the current first-person camera?\n\n" +
            "License: CC0. Existing forest, mushrooms, and player controller will be preserved.\n\n" +
            "Save your work or make a backup first.", "Download and install", "Cancel"))
            return;

        try
        {
            Directory.CreateDirectory(Folder);
            await DownloadIfMissing("arms.fbx", ModelPath, 50000);
            await DownloadIfMissing("arm_albedo_pale.png", TexturePath, 10000);
            await DownloadIfMissing("LICENSE", Folder + "/LICENSE.txt", 100);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImports();
            InstallFromLocalAssets();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("WRAD Arms — download failed",
                "Unity could not download the model automatically.\n\n" + e.Message +
                "\n\nAlternative: open https://github.com/wwwriks/wrad-arms , download ZIP, copy arms.fbx and arm_albedo_pale.png to " + Folder +
                ", then run Mushroom Hunt > Hands > 2. Install from downloaded files.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static async Task DownloadIfMissing(string filename, string destination, int minLength)
    {
        if (File.Exists(destination) && new FileInfo(destination).Length > minLength) return;
        EditorUtility.DisplayProgressBar("WRAD Arms", "Downloading " + filename + "...", 0.3f);
        using (var request = UnityWebRequest.Get(Source + filename))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Delay(60);
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException("Could not download " + filename + ": " + request.error);
            byte[] data = request.downloadHandler.data;
            if (data == null || data.Length < minLength)
                throw new IOException("Downloaded " + filename + " is unexpectedly small.");
            File.WriteAllBytes(destination, data);
        }
    }

    static void ConfigureImports()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }
        TextureImporter texture = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (texture != null)
        {
            texture.textureType = TextureImporterType.Default;
            texture.sRGBTexture = true;
            texture.SaveAndReimport();
        }
    }

    [MenuItem("Mushroom Hunt/Hands/2. Install from downloaded files")]
    public static void InstallFromLocalAssets()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("WRAD Arms", "Stop Play Mode first.", "OK");
            return;
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (!asset || !texture)
        {
            EditorUtility.DisplayDialog("WRAD Arms — files missing",
                "Cannot find " + ModelPath + " or " + TexturePath + ".\n\n" +
                "Use menu command 1 (automatic download), or manually download the WRAD Arms GitHub ZIP and copy these two files into " + Folder + ".", "OK");
            return;
        }

        MushroomGame game = UnityEngine.Object.FindFirstObjectByType<MushroomGame>();
        FirstPersonPlayer player = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>();
        if (!game || !player)
        {
            EditorUtility.DisplayDialog("WRAD Arms", "Open Assets/Scenes/Forest.unity before installing the arms.", "OK");
            return;
        }
        Camera camera = game.view ? game.view : player.GetComponentInChildren<Camera>(true);
        if (!camera)
        {
            EditorUtility.DisplayDialog("WRAD Arms", "A first-person camera was not found on Player.", "OK");
            return;
        }
        Material material = CreateMaterial(texture);
        Transform old = camera.transform.Find("WRAD_Viewmodel");
        if (old)
        {
            if (!EditorUtility.DisplayDialog("WRAD Arms", "A WRAD viewmodel is already installed. Replace it?", "Replace", "Cancel")) return;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        GameObject root = new GameObject("WRAD_Viewmodel");
        Undo.RegisterCreatedObjectUndo(root, "Install WRAD viewmodel");
        root.transform.SetParent(camera.transform, false);
        GameObject modelInstance = PrefabUtility.InstantiatePrefab(asset, root.transform) as GameObject;
        if (!modelInstance)
        {
            Undo.DestroyObjectImmediate(root);
            EditorUtility.DisplayDialog("WRAD Arms", "Unable to instantiate the downloaded FBX.", "OK");
            return;
        }
        modelInstance.name = "WRAD_Arms_Model";
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        foreach (Animator animator in modelInstance.GetComponentsInChildren<Animator>(true))
            animator.enabled = false; // procedural bone animation is authoritative

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skin)
                skin.updateWhenOffscreen = true;
        }

        AutoFrame(root.transform, modelInstance.transform);
        camera.nearClipPlane = 0.015f;
        EditorUtility.SetDirty(camera);

        // Retain the original model in the scene, but hide its renderers.
        Transform originalModel = camera.transform.parent ? FindRecursive(camera.transform.parent, "Hands") : null;
        if (originalModel)
            foreach (Renderer oldRenderer in originalModel.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(oldRenderer, "Hide old PSX hands");
                oldRenderer.enabled = false;
            }

        FirstPersonHands hands = player.GetComponent<FirstPersonHands>();
        if (!hands) hands = Undo.AddComponent<FirstPersonHands>(player.gameObject);
        Undo.RecordObject(hands, "Configure WRAD hands");
        hands.viewmodelRoot = root.transform;
        hands.showHands = true;
        EditorUtility.SetDirty(hands);

        Undo.RecordObject(game, "Connect WRAD animation to mushroom pickup");
        game.hands = hands;
        game.target = 5;
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        bool hasFingers = FindRecursive(root.transform, "finger_index1.r") && FindRecursive(root.transform, "wrist.r");
        string message = "WRAD Arms model installed on MainCamera.\n" +
            "Original PSX hands are hidden.\n" +
            "Animation bones: " + (hasFingers ? "found" : "NOT FOUND — inspect FBX importer") + ".\n\n" +
            "Press Play, aim at a mushroom, press E.\n" +
            "H toggles hands.\n\n" +
            "If they look too big or face backward: adjust WRAD_Viewmodel and its child WRAD_Arms_Model under MainCamera, or use the model flip menu.";
        Debug.Log(message);
        EditorUtility.DisplayDialog("WRAD Arms — installed", message, "OK");
    }

    static Material CreateMaterial(Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Unlit/Texture");
            material = new Material(shader) { name = "WRADSkin" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.mainTexture = texture;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static void AutoFrame(Transform root, Transform model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.x < 0.001f) return;

        // Width ~ 1.25 m at camera depth 0.85 m works as a starting FPS pose.
        float scale = Mathf.Clamp(1.25f / bounds.size.x, 0.01f, 25f);
        model.localScale = Vector3.one * scale;
        // Recalculate after scaling. Move the visual *relative* to the camera.
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 currentCenter = root.InverseTransformPoint(bounds.center);
        model.localPosition += new Vector3(0f, -0.30f, 0.90f) - currentCenter;
    }

    static Transform FindRecursive(Transform parent, string name)
    {
        if (!parent) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindRecursive(child, name);
            if (found) return found;
        }
        return null;
    }

    [MenuItem("Mushroom Hunt/Hands/Flip model facing 180 degrees")]
    static void FlipModel()
    {
        FirstPersonHands hands = UnityEngine.Object.FindFirstObjectByType<FirstPersonHands>();
        if (!hands || !hands.viewmodelRoot || hands.viewmodelRoot.childCount == 0)
        {
            EditorUtility.DisplayDialog("WRAD Arms", "Install the WRAD viewmodel first.", "OK");
            return;
        }
        Transform model = hands.viewmodelRoot.GetChild(0);
        Undo.RecordObject(model, "Flip WRAD arms");
        model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;
        EditorSceneManager.MarkSceneDirty(hands.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
    }
}
#endif
