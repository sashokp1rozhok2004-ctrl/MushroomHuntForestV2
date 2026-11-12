using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Apply the patch to the existing Forest scene without rebuilding or losing
/// manually placed trees/mushrooms. Accessible in the Unity top menu.
/// </summary>
public static class MushroomUpgradeTools
{
    [MenuItem("Mushroom Hunt/Apply gameplay upgrades to open scene")]
    public static void ApplyUpgrades()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Mushroom Hunt", "Stop Play Mode first.", "OK");
            return;
        }

        MushroomGame game = Object.FindFirstObjectByType<MushroomGame>();
        FirstPersonPlayer player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        MushroomPickup[] pickups = Object.FindObjectsByType<MushroomPickup>(FindObjectsSortMode.None);
        if (!game || !player)
        {
            EditorUtility.DisplayDialog("Mushroom Hunt",
                "The open scene has no MushroomGame / FirstPersonPlayer.\n" +
                "Open Assets/Scenes/Forest.unity, then run this command again.", "OK");
            return;
        }

        Undo.RecordObject(game, "Configure mushroom target and references");
        game.target = 5;
        game.collected = 0;
        if (!game.view) game.view = player.GetComponentInChildren<Camera>(true);
        if (!game.hands) game.hands = player.GetComponent<FirstPersonHands>();
        EditorUtility.SetDirty(game);

        Undo.RecordObject(player, "Wire player to mushroom game");
        player.game = game;
        if (!player.head && game.view)
            player.head = game.view.transform.parent ? game.view.transform.parent : game.view.transform;
        EditorUtility.SetDirty(player);

        if (game.hands)
        {
            Undo.RecordObject(game.hands, "Hide old FPS hands");
            game.hands.showHands = game.hands.viewmodelRoot != null;
            EditorUtility.SetDirty(game.hands);
        }

        int collidersAdded = 0;
        foreach (MushroomPickup pickup in pickups)
        {
            if (pickup.GetComponentInChildren<Collider>(true)) continue;
            SphereCollider collider = Undo.AddComponent<SphereCollider>(pickup.gameObject);
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.radius = 0.65f;
            collidersAdded++;
        }

        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Mushroom Hunt — updated!",
            "Goal: 5 mushrooms\n" +
            "Scene mushrooms: " + pickups.Length + "\n" +
            "Missing colliders added: " + collidersAdded + "\n" +
            "WRAD hands: " + (game.hands && game.hands.viewmodelRoot ? "enabled" : "not installed yet") + ".\n\n" +
            "Press Play to test. The original forest layout is preserved.", "OK");
    }

    [MenuItem("Mushroom Hunt/Check assignment requirements")]
    public static void CheckRequirements()
    {
        MushroomGame game = Object.FindFirstObjectByType<MushroomGame>();
        FirstPersonPlayer player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        MushroomPickup[] mushrooms = Object.FindObjectsByType<MushroomPickup>(FindObjectsSortMode.None);
        var report = new StringBuilder();
        report.AppendLine("FIRST PERSON: " + (player ? "OK" : "MISSING"));
        report.AppendLine("CAMERA: " + (game && (game.view || game.GetComponentInChildren<Camera>()) ? "OK" : "MISSING"));
        report.AppendLine("COLLECTIBLE SCRIPT: " + (mushrooms.Length > 0 ? "OK" : "MISSING"));
        report.AppendLine("MUSHROOMS IN SCENE: " + mushrooms.Length);
        report.AppendLine("TARGET IN INSPECTOR: " + (game ? game.target.ToString() : "MISSING"));
        report.AppendLine("WIN UI: MushroomGame.OnGUI");
        report.AppendLine("WINDOWS BUILD: Mushroom Hunt > Build Windows");
        report.AppendLine("\nThis checks object references, not runtime behaviour. Test in Play Mode too.");
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Mushroom Hunt — scene check", report.ToString(), "OK");
    }
}
