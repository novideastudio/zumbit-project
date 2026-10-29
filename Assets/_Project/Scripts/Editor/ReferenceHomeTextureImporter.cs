#if UNITY_EDITOR
using UnityEditor;
public sealed class ReferenceHomeTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/_Project/Resources/Art/PCReferenceUI/",System.StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;
        t.textureType=TextureImporterType.Default;t.textureShape=TextureImporterShape.Texture2D;
        t.mipmapEnabled=false;t.alphaIsTransparency=true;t.filterMode=UnityEngine.FilterMode.Bilinear;
        t.wrapMode=UnityEngine.TextureWrapMode.Clamp;t.maxTextureSize=2048;
        t.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
#endif
