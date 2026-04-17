using UnityEditor;
using UnityEngine;

public class MakeSprites
{
    [MenuItem("Tools/Make Shadow Icons Sprites")]
    public static void MakeSpritesNow()
    {
        string path = "Assets/Resources/Icons/Shadows";
        string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { path });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
        }
        Debug.Log("Converted all textures to Sprites.");
    }
}
