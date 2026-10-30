#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public sealed class PcRangeTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/_Project/Resources/Art/PCTrainingUI/",System.StringComparison.Ordinal))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        string key=System.IO.Path.GetFileNameWithoutExtension(assetPath);
        bool frame=key=="panel" || key=="red" || key=="green" || key=="gold" || key=="selected";
        importer.spriteBorder=frame?new Vector4(16,16,16,16):Vector4.zero;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
    }
}
#endif
