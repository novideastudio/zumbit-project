#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public sealed class PcSurvivorTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/_Project/Resources/Art/PCSurvivorUI/",System.StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;
        t.alphaIsTransparency=true;t.maxTextureSize=2048;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
#endif
