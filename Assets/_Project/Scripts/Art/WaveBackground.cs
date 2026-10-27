using UnityEngine;

/// <summary>
/// Cenario de combate. A arte vem do LOCAL escolhido no mapa (veja SceneBackgrounds):
/// cada local tem seu proprio wave_*.png, altura de rua e pontos de atmosfera.
/// </summary>
public class WaveBackground : MonoBehaviour
{
    /// <summary>Linha da rua no MUNDO (pes de sobreviventes e zumbis). Fixa; quem se move e o cenario.</summary>
    public const float GroundY = -0.5f;

    /// <summary>
    /// Altura do cenario em unidades de mundo. TODAS as artes sao normalizadas para esta altura
    /// (elas tem 724 a 809 px), entao heroi/zumbi tem a mesma proporcao em qualquer local
    /// e a camera (ortho 5 = 10 unidades) enquadra todos do mesmo jeito.
    /// </summary>
    public const float WorldHeight = 10.1f;

    /// <summary>Preenchidos em Start a partir do local atual. Pode ser forcado antes (ex.: cena de teste).</summary>
    public string sceneId;

    /// <summary>Altura da rua DENTRO da imagem, como fracao da altura (0 = topo, 1 = base).</summary>
    [Range(0.3f, 0.98f)] public float groundRowFraction = 0.75f;

    SceneProfile profile;
    SpriteRenderer sr;

    public SceneProfile Profile => profile;

    void Start()
    {
        // O estande deve sempre usar sua arte exclusiva, sem depender de outro ID de local
        // nem de um resultado null que possa ter ficado no cache do ArtLoader.
        bool isRange = GameSession.IsRange || (GameSession.Location != null && GameSession.Location.id == "range");
        profile = isRange
            ? SceneBackgrounds.Get("range")
            : (string.IsNullOrEmpty(sceneId) ? SceneBackgrounds.ForLocation(GameSession.Location) : SceneBackgrounds.Get(sceneId));

        var sp = isRange ? Resources.Load<Sprite>(profile.path) : ArtLoader.Get(profile.path);
        if (isRange && sp == null)
            Debug.LogError($"Zombit: o fundo exclusivo do estande não foi carregado: Resources/{profile.path}. Confira wave_range.png e seu .meta.");
        if (sp == null && profile.id != SceneBackgrounds.DefaultId)
        {
            Debug.LogWarning($"Zombit: cenario '{profile.path}' nao encontrado; usando o padrao.");
            profile = SceneBackgrounds.Get(SceneBackgrounds.DefaultId);
            sp = ArtLoader.Get(profile.path);
        }
        else if (isRange && sp != null)
        {
            Debug.Log("Zombit: fundo exclusivo do estande carregado: " + profile.path);
        }
        if (sp == null) { Debug.LogError("Zombit: nenhum cenario de fundo encontrado em Resources/Art/Backgrounds."); Destroy(gameObject); return; }

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = -10000;

        groundRowFraction = profile.groundRow;

        // Escala para que a altura do cenario seja sempre WorldHeight.
        transform.localScale = Vector3.one * (WorldHeight / (sp.rect.height / sp.pixelsPerUnit));
        Place();

        var cam = Camera.main;
        if (cam) cam.backgroundColor = Color.black;

        var fx = GetComponent<AmbientSceneEffects>();
        if (fx == null) fx = gameObject.AddComponent<AmbientSceneEffects>();
        fx.profile = profile;   // Start do efeito roda no proximo frame, ja com o perfil certo

        // Vinheta + tom quente por cima de tudo: une cenario, sobreviventes e zumbis na mesma atmosfera.
        if (GetComponent<SceneGrade>() == null) gameObject.AddComponent<SceneGrade>();
    }

    // Move o CENARIO para que a fracao escolhida da imagem caia exatamente em GroundY.
    void Place()
    {
        float centerY = GroundY - (0.5f - groundRowFraction) * WorldHeight;
        transform.position = new Vector3(0f, centerY, 10f);

        // Enquadra a camera para a resolucao atual: cenario cobrindo a tela toda e rua sempre na mesma altura.
        CameraFramer.Attach(sr.bounds, GroundY);
    }
}
