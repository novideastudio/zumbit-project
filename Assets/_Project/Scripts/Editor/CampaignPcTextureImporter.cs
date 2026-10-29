#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public sealed class CampaignPcTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/_Project/Resources/Art/PCCampaignUI/",System.StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;
        t.mipmapEnabled=false;t.alphaIsTransparency=true;t.wrapMode=TextureWrapMode.Clamp;
        t.filterMode=FilterMode.Bilinear;t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
#endif
