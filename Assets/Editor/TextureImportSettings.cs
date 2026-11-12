using UnityEditor;

public class TextureImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.Contains("_nor_gl"))
        {
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.NormalMap;
            importer.sRGBTexture=false;
        }
        else if (assetPath.Contains("_Rough"))
        {
            var importer=(TextureImporter)assetImporter;
            importer.sRGBTexture=false;
        }
    }
}
