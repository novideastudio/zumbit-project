#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public sealed class PcTitleTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/_Project/Resources/Art/PCTitleUI/",System.StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;
        t.mipmapEnabled=false;t.alphaIsTransparency=false;t.wrapMode=TextureWrapMode.Clamp;
        t.filterMode=FilterMode.Bilinear;t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
#endif
