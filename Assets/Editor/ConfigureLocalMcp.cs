using UnityEditor;
using System.IO;

// Supply the existing local installation without changing the system PATH.
[InitializeOnLoad]
public static class ConfigureLocalMcp
{
    static ConfigureLocalMcp()
    {
        const string key = "MCPForUnity.UvxPath";
        const string localUvx = "C:/Users/zemss/Documents/Codex/tools/data-env/Scripts/uvx.exe";
        if (string.IsNullOrEmpty(EditorPrefs.GetString(key, "")) && File.Exists(localUvx))
            EditorPrefs.SetString(key, localUvx);
    }
}
