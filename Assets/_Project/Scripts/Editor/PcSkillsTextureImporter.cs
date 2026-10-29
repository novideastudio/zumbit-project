#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class PcSkillsTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(
            "Assets/_Project/Resources/Art/PCSkillsUI/",
            System.StringComparison.Ordinal))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        string fileName =
            System.IO.Path.GetFileNameWithoutExtension(assetPath);

        importer.spriteBorder = fileName.StartsWith(
            "frame",
            System.StringComparison.Ordinal)
            ? new Vector4(16f, 16f, 16f, 16f)
            : Vector4.zero;

        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
    }
}
#endif