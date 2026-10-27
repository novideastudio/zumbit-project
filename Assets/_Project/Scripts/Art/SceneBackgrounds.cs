using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dados de UM cenario de combate: qual arte usar, onde fica a rua e onde ficam os pontos de
/// atmosfera (sol, luzes, fogo, fumaca). Todas as posicoes sao NORMALIZADAS na imagem:
/// x = 0 (esquerda) .. 1 (direita), y = 0 (topo) .. 1 (base).
/// Para ajustar um ponto, basta mexer nos numeros abaixo; nenhum outro script precisa mudar.
/// </summary>
public sealed class SceneProfile
{
    public struct Lamp
    {
        public float x, y, size, alpha;
        public bool flicker;
        public Lamp(float x, float y, float size = 0.55f, float alpha = 0.18f, bool flicker = false)
        { this.x = x; this.y = y; this.size = size; this.alpha = alpha; this.flicker = flicker; }
    }

    public string id;
    public string path;
    /// <summary>
    /// Altura (0 topo .. 1 base) da linha-base do cenario. Os pes dos personagens ficam um pouco ABAIXO dela
    /// (MissionSetup usa GroundY - 0,28 = ~0,028 da altura), entao o valor certo e "meio do asfalto - 0,03".
    /// Posicao fixa (sem ajuste ao vivo): para mudar a altura dos sobreviventes use laneOffsetY no GameTuning.asset.
    /// </summary>
    public float groundRow = 0.75f;
    public Vector2 sun;
    public float sunSize = 1.1f;
    public Lamp[] lamps = new Lamp[0];
    /// <summary>Fogueiras/barris ja PINTADOS na arte: recebem luz tremula e brasas (sem sprite de chama por cima).</summary>
    public Vector2[] fires = new Vector2[0];
    /// <summary>Colunas de fumaca pintadas na arte: recebem fiapos suaves subindo.</summary>
    public Vector2[] smokes = new Vector2[0];
}

public static class SceneBackgrounds
{
    const string Dir = "Art/Backgrounds/";

    /// <summary>Cenario padrao (menu, locais sem mapeamento).</summary>
    public const string DefaultId = "ruins";
    public const string DefaultPath = Dir + "wave_" + DefaultId;

    // id do LOCAL (GameDatabase.Locations) -> id do CENARIO (arquivo wave_<id>.png)
    static readonly Dictionary<string, string> byLocation = new Dictionary<string, string>
    {
        { "residencial", "ruins"    },   // ruas de casas abandonadas
        { "suburbio",    "farm"     },   // casas e fazendas
        { "shopping",    "shopping" },   // dentro do shopping
        { "industrial",  "dock"     },   // porto, guindaste, containers
        { "hospital",    "hospital" },
        { "delegacia",   "rpd"      },
        { "metro",       "metro"    },
        { "centro",      "city"     },   // centro urbano (compartilha a arte com o Shopping)
        { "base",        "military" },
    };

    static Dictionary<string, SceneProfile> profiles;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => profiles = null;

    public static SceneProfile ForLocation(LocationDef loc)
        => Get(loc != null && byLocation.TryGetValue(loc.id, out var s) ? s : DefaultId);

    public static string PathFor(LocationDef loc) => ForLocation(loc).path;

    public static SceneProfile Get(string sceneId)
    {
        if (profiles == null) Build();
        return profiles.TryGetValue(sceneId, out var p) ? p : profiles[DefaultId];
    }

    static void Add(SceneProfile p) { p.path = Dir + "wave_" + p.id; profiles[p.id] = p; }

    static void Build()
    {
        profiles = new Dictionary<string, SceneProfile>();
        SceneProfile.Lamp Lp(float x, float y, float size = 0.55f, float alpha = 0.18f, bool flicker = false)
            => new SceneProfile.Lamp(x, y, size, alpha, flicker);

        Add(new SceneProfile
        {
            id = "ruins", groundRow = 0.66f, sun = new Vector2(0.624f, 0.33f), sunSize = 1.0f,
            lamps = new[] { Lp(0.045f, 0.33f, 0.6f, 0.16f), Lp(0.955f, 0.19f, 0.7f, 0.2f), Lp(0.12f, 0.36f, 0.4f, 0.12f) },
            fires = new[] { new Vector2(0.02f, 0.59f) },
            smokes = new[] { new Vector2(0.40f, 0.22f), new Vector2(0.80f, 0.20f) },
        });
        Add(new SceneProfile
        {
            id = "shopping",
            groundRow = 0.66f,
            sun = new Vector2(0.624f, 0.33f),
            sunSize = 1.0f,
        });
        Add(new SceneProfile
        {
            id = "farm", groundRow = 0.62f, sun = new Vector2(0.46f, 0.34f), sunSize = 1.0f,
            lamps = new[] { Lp(0.034f, 0.40f, 0.55f, 0.18f), Lp(0.137f, 0.425f, 0.45f, 0.16f) },
            smokes = new[] { new Vector2(0.83f, 0.27f), new Vector2(0.63f, 0.28f) },
        });
        Add(new SceneProfile
        {
            id = "city", groundRow = 0.70f, sun = new Vector2(0.734f, 0.214f), sunSize = 1.1f,
            lamps = new[] { Lp(0.169f, 0.127f, 0.8f, 0.24f), Lp(0.958f, 0.17f, 0.8f, 0.24f), Lp(0.60f, 0.47f, 0.5f, 0.12f) },
            fires = new[] { new Vector2(0.025f, 0.60f), new Vector2(0.282f, 0.597f), new Vector2(0.75f, 0.597f) },
            smokes = new[] { new Vector2(0.45f, 0.28f), new Vector2(0.80f, 0.20f) },
        });
        Add(new SceneProfile
        {
            id = "dock", groundRow = 0.66f, sun = new Vector2(0.556f, 0.42f), sunSize = 1.3f,
            lamps = new[] { Lp(0.225f, 0.205f, 0.9f, 0.26f), Lp(0.769f, 0.205f, 0.9f, 0.26f), Lp(0.04f, 0.50f, 0.5f, 0.12f) },
        });
        Add(new SceneProfile
        {
            id = "hospital", groundRow = 0.72f, sun = new Vector2(0.23f, 0.27f), sunSize = 1.0f,
            lamps = new[]
            {
                Lp(0.60f, 0.47f, 1.5f, 0.28f), Lp(0.434f, 0.35f, 0.6f, 0.2f), Lp(0.029f, 0.39f, 0.6f, 0.2f),
                Lp(0.946f, 0.365f, 0.6f, 0.2f),
            },
        });
        Add(new SceneProfile
        {
            id = "metro", groundRow = 0.75f, sun = new Vector2(0.615f, 0.25f), sunSize = 1.1f,
            lamps = new[]
            {
                Lp(0.156f, 0.462f, 0.5f, 0.2f), Lp(0.268f, 0.463f, 0.5f, 0.2f), Lp(0.385f, 0.462f, 0.5f, 0.2f),
                Lp(0.548f, 0.462f, 0.5f, 0.2f), Lp(0.615f, 0.47f, 1.4f, 0.26f), Lp(0.677f, 0.462f, 0.5f, 0.2f),
                Lp(0.779f, 0.462f, 0.5f, 0.2f), Lp(0.964f, 0.371f, 0.6f, 0.2f), Lp(0.125f, 0.20f, 0.6f, 0.2f),
            },
        });
        Add(new SceneProfile
        {
            id = "military", groundRow = 0.65f, sun = new Vector2(0.387f, 0.25f), sunSize = 1.0f,
            lamps = new[]
            {
                Lp(0.109f, 0.09f, 0.8f, 0.3f, true), Lp(0.655f, 0.065f, 0.8f, 0.3f, true), Lp(0.561f, 0.088f, 0.6f, 0.24f, true),
                Lp(0.325f, 0.426f, 0.5f, 0.2f), Lp(0.394f, 0.426f, 0.5f, 0.2f), Lp(0.447f, 0.426f, 0.5f, 0.2f),
                Lp(0.615f, 0.425f, 0.6f, 0.22f), Lp(0.793f, 0.422f, 0.6f, 0.22f),
            },
            smokes = new[] { new Vector2(0.50f, 0.20f) },
        });
        Add(new SceneProfile
        {
            id = "rpd", groundRow = 0.73f, sun = new Vector2(0.233f, 0.31f), sunSize = 1.0f,
            lamps = new[]
            {
                Lp(0.537f, 0.489f, 0.6f, 0.22f), Lp(0.726f, 0.489f, 0.6f, 0.22f), Lp(0.976f, 0.403f, 0.7f, 0.22f),
                Lp(0.332f, 0.441f, 0.5f, 0.18f), Lp(0.141f, 0.443f, 0.5f, 0.18f), Lp(0.417f, 0.493f, 0.5f, 0.18f),
                Lp(0.16f, 0.34f, 0.5f, 0.18f),
            },
            smokes = new[] { new Vector2(0.335f, 0.30f) },
        });
    }
}
