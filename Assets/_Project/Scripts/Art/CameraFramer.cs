using UnityEngine;

/// <summary>
/// Enquadramento da camera de combate para QUALQUER resolucao/proporcao de tela.
///
/// Regras (referencia de design: 16:9 / 1920x1080, ortho 5 = 17,8 x 10 unidades):
///  1. O cenario SEMPRE cobre a tela inteira ("cover"): nunca aparece faixa preta dentro da area de jogo.
///  2. A faixa de combate (do ultimo sobrevivente do trio ate o ponto onde os zumbis nascem)
///     SEMPRE cabe na largura da tela.
///  3. A rua (WaveBackground.GroundY) fica sempre na mesma altura relativa da tela,
///     deixando a parte de baixo do cenario (cerca/barricadas em primeiro plano) atras do HUD.
///  4. Se a tela for estreita demais (4:3, 5:4) ou larga demais (32:9) para cumprir 1 e 2 ao mesmo
///     tempo, usa barras pretas (letterbox/pillarbox) como os jogos de console fazem.
///
/// Personagens e zumbis estao em unidades de mundo, entao escalam junto com o cenario:
/// a proporcao personagem x cenario e identica em 720p, 1080p, 1440p ou 4K.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraFramer : MonoBehaviour
{
    public const float DesignOrtho = 5f;              // 16:9 de referencia
    public const float MinPlayWidth = 17.6f;          // largura de combate que nunca pode ser cortada
    public const float MinOrtho = 4.4f;               // limite de zoom (ultrawide nao deixa os personagens gigantes)
    public const float GroundScreenFraction = 0.34f;  // rua a 34% da altura da tela (0 = base, 1 = topo)
    public const float PlayCenterX = 0f;
    public const float SpawnMargin = 1.0f;            // zumbis nascem 1 unidade fora da borda direita

    public static CameraFramer Instance { get; private set; }

    Camera cam;
    Camera letterboxCam;
    Bounds bgBounds;
    float groundY;
    int lastW, lastH;
    bool hasTarget;
    Vector3 lastTarget;   // posicao "real" da camera, sem o tremor de dano

    /// <summary>Chamado pelo WaveBackground depois de posicionar o cenario.</summary>
    public static void Attach(Bounds backgroundBounds, float groundLineY)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var f = cam.GetComponent<CameraFramer>();
        if (f == null) f = cam.gameObject.AddComponent<CameraFramer>();
        f.cam = cam;
        f.bgBounds = backgroundBounds;
        f.groundY = groundLineY;
        f.Apply(true);
    }

    /// <summary>X fora da tela, a direita, onde os zumbis devem nascer nesta resolucao.</summary>
    public static float RightSpawnX(float fallback = 9f)
    {
        if (Instance == null || Instance.cam == null || !Instance.hasTarget) return fallback;
        float halfW = Instance.cam.orthographicSize * Instance.cam.aspect;
        return Instance.lastTarget.x + halfW + SpawnMargin;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (letterboxCam) Destroy(letterboxCam.gameObject);
    }

    void LateUpdate()
    {
        if (Screen.width != lastW || Screen.height != lastH) Apply(false);
    }

    void Apply(bool force)
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || bgBounds.size.x <= 0f || bgBounds.size.y <= 0f) return;
        lastW = Screen.width; lastH = Screen.height;

        cam.orthographic = true;
        float bgW = bgBounds.size.x, bgH = bgBounds.size.y;
        float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);

        // Faixa de proporcoes em que as regras 1 e 2 cabem juntas; fora dela, barras pretas.
        float minAspect = MinPlayWidth / bgH;
        float maxAspect = Mathf.Max(minAspect, bgW / (2f * MinOrtho));
        float aspect = Mathf.Clamp(screenAspect, minAspect, maxAspect);

        Rect rect = new Rect(0f, 0f, 1f, 1f);
        if (screenAspect < aspect - 0.0001f)
        {
            float h = screenAspect / aspect;                // letterbox (barras em cima e embaixo)
            rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        else if (screenAspect > aspect + 0.0001f)
        {
            float w = aspect / screenAspect;                // pillarbox (barras nas laterais)
            rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        cam.rect = rect;
        UpdateLetterbox(rect.width < 0.999f || rect.height < 0.999f);

        float coverMax = Mathf.Min(bgH * 0.5f, bgW / (2f * aspect));
        float playMin = Mathf.Max(MinOrtho, MinPlayWidth / (2f * aspect));
        float ortho = playMin > coverMax ? coverMax : Mathf.Clamp(DesignOrtho, playMin, coverMax);
        cam.orthographicSize = ortho;

        float halfW = ortho * aspect;
        float x = Mathf.Clamp(PlayCenterX, bgBounds.min.x + halfW, bgBounds.max.x - halfW);
        // base da tela = y - ortho; rua = base + 2*ortho*fracao  =>  y = rua - 2*ortho*fracao + ortho
        float y = groundY - 2f * ortho * GroundScreenFraction + ortho;
        y = Mathf.Clamp(y, bgBounds.min.y + ortho, bgBounds.max.y - ortho);

        var target = new Vector3(x, y, cam.transform.position.z);
        if (force || !hasTarget) cam.transform.position = target;
        else cam.transform.position += target - lastTarget;   // preserva um tremor de dano em andamento
        lastTarget = target;
        hasTarget = true;
    }

    // Com cam.rect menor que a tela, a URP nao limpa a area de fora: uma camera vazia pinta de preto.
    void UpdateLetterbox(bool needed)
    {
        if (!needed)
        {
            if (letterboxCam) letterboxCam.enabled = false;
            return;
        }
        if (letterboxCam == null)
        {
            var go = new GameObject("LetterboxClear");
            letterboxCam = go.AddComponent<Camera>();
            letterboxCam.cullingMask = 0;
            letterboxCam.clearFlags = CameraClearFlags.SolidColor;
            letterboxCam.backgroundColor = Color.black;
            letterboxCam.orthographic = true;
            letterboxCam.rect = new Rect(0f, 0f, 1f, 1f);
        }
        letterboxCam.depth = cam.depth - 10f;
        letterboxCam.enabled = true;
    }
}
