#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ArtImporter : AssetPostprocessor
{
    // Mudar este numero forca a Unity a reimportar as artes com as configuracoes novas.
    public override uint GetVersion() => 3;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/_Project/Resources/Art/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.mipmapEnabled = false;
        // Necessario para SpriteMetrics medir a altura visivel (sao PNGs pequenos; custo de memoria irrelevante).
        ti.isReadable = assetPath.Contains("/Zombies/") || assetPath.Contains("/Characters/") || assetPath.Contains("/Range/");
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = 4096;
        // Pixel art (zumbis/personagens/itens) fica sem compressao para manter a nitidez.
        // city_map.png (1536x1024, multiplo de 4) e uma ilustracao grande e tolera BC7 de alta qualidade:
        // ~6 MB -> ~1,5 MB de VRAM. Os backgrounds de cenario (ex.: wave_ruins 1774x887) NAO sao multiplos
        // de 4, entao a Unity os manteria sem compressao de qualquer jeito; por isso ficam como estao.
        bool compressHq = assetPath.EndsWith("/Map/city_map.png");
        ti.textureCompression = compressHq
            ? TextureImporterCompression.CompressedHQ
            : TextureImporterCompression.Uncompressed;
        ti.filterMode = assetPath.Contains("/Zombies/") ? FilterMode.Point : FilterMode.Bilinear;

        bool bottom = assetPath.Contains("/Zombies/") || assetPath.Contains("/Range/") ||
                      (assetPath.Contains("/Characters/") && assetPath.EndsWith("_full.png"));

        var set = new TextureImporterSettings();
        ti.ReadTextureSettings(set);
        set.spriteMeshType = SpriteMeshType.FullRect;
        set.spriteAlignment = (int)(bottom ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        ti.SetTextureSettings(set);
    }
}
#endif
