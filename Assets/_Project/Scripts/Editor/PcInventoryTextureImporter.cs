#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public sealed class PcInventoryTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/_Project/Resources/Art/PCInventoryUI/",System.StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;t.alphaIsTransparency=true;
        t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;
    }
}
#endif
