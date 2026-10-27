using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feedback de dano do heroi: vinheta vermelha na tela + leve tremor de camera quando a vida CAI,
/// e um pulso suave de vinheta com vida baixa. O PixelCharacterMotion ja pisca o sprite; isto cobre a tela.
///
/// Como o heroi fica parado segurando hordas (muitos golpes por segundo), os efeitos sao limitados:
/// o tremor acumula "trauma" com teto baixo e a vinheta tem alfa maximo moderado.
/// O tremor pode ser desligado em Opcoes (PlayerPrefs "zombit_shake").
/// </summary>
// Roda ANTES dos outros LateUpdate (ex.: PlayerNicknameDisplay) para o offset da camera ja estar aplicado.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class HeroDamageFeedback : MonoBehaviour
{
    const string ShakeKey = "zombit_shake";
    const float MaxTrauma = 0.6f;
    const float MaxShakeUnits = 0.25f;   // deslocamento maximo da camera ortografica, em unidades de mundo
    const float TraumaDecayPerSecond = 1.6f;
    const float FlashDecayPerSecond = 2.4f;
    const float MaxFlashAlpha = 0.6f;

    static int shakeSetting = -1;
    public static bool ShakeEnabled
    {
        get
        {
            if (shakeSetting < 0) shakeSetting = PlayerPrefs.GetInt(ShakeKey, 1);
            return shakeSetting == 1;
        }
        set
        {
            shakeSetting = value ? 1 : 0;
            PlayerPrefs.SetInt(ShakeKey, shakeSetting);
            PlayerPrefs.Save();
        }
    }

    // Tremor pedido por outros efeitos (explosao, onda de choque). Consumido no LateUpdate.
    static float pendingTrauma;
    public static void Shake(float amount) { pendingTrauma = Mathf.Min(0.9f, pendingTrauma + Mathf.Max(0f, amount)); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { shakeSetting = -1; pendingTrauma = 0f; }

    Health health;
    Image vignette;
    Canvas canvas;
    Camera cam;
    Vector3 appliedOffset;
    float lastHealth = -1f, lastMax = -1f;
    float flash, trauma;
    static Sprite vignetteSprite;

    void Awake()
    {
        health = GetComponent<Health>();
        BuildOverlay();
    }

    void OnEnable() { if (health != null) health.Changed += OnHealthChanged; }

    void OnDisable()
    {
        if (health != null) health.Changed -= OnHealthChanged;
        RemoveCameraOffset();
    }

    void OnDestroy()
    {
        RemoveCameraOffset();
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void BuildOverlay()
    {
        // Ordem -1: fica ATRAS do HUD (ordem 0), so tinge a cena.
        canvas = UIFactory.MakeCanvas("DamageFeedbackCanvas", -1);
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;

        var rt = UIFactory.Node("DamageVignette", canvas.transform);
        UIFactory.Stretch(rt);
        vignette = rt.gameObject.AddComponent<Image>();
        vignette.sprite = GetVignetteSprite();
        vignette.color = new Color(0.85f, 0.02f, 0.04f, 0f);
        vignette.raycastTarget = false;
    }

    void OnHealthChanged(float current, float max)
    {
        // So conta como dano se a vida atual caiu COM o mesmo maximo
        // (equipar/desequipar item muda o maximo e nao deve piscar a tela).
        bool sameMax = lastMax > 0f && Mathf.Approximately(max, lastMax);
        if (sameMax && lastHealth >= 0f && current < lastHealth)
        {
            float fraction = max > 0f ? (lastHealth - current) / max : 0f;
            flash = Mathf.Min(1f, flash + 0.30f + fraction * 4f);
            trauma = Mathf.Min(MaxTrauma, trauma + 0.06f + fraction * 1.5f);
            if (current <= 0f) { flash = 1f; trauma = MaxTrauma; }
        }
        lastHealth = current;
        lastMax = max;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        bool dead = health != null && health.Current <= 0f;

        // ---- vinheta ----
        flash = Mathf.Max(0f, flash - FlashDecayPerSecond * dt);
        float alpha = flash * MaxFlashAlpha;
        if (health != null && health.max > 0f && !dead)
        {
            float ratio = health.Current / health.max;
            if (ratio <= 0.25f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                alpha = Mathf.Max(alpha, (0.16f + 0.14f * pulse) * Mathf.Clamp01(1f - ratio / 0.25f * 0.5f));
            }
        }
        if (dead) alpha = Mathf.Max(alpha, 0.45f);
        if (vignette != null && !Mathf.Approximately(vignette.color.a, alpha))
        {
            Color c = vignette.color;
            c.a = alpha;
            vignette.color = c; // so reescreve quando muda (evita rebuild do Canvas todo quadro)
        }

        // ---- tremor de camera ----
        RemoveCameraOffset();
        if (pendingTrauma > 0f)
        {
            if (ShakeEnabled && !dead) trauma = Mathf.Min(0.85f, Mathf.Max(trauma, pendingTrauma));
            pendingTrauma = 0f;
        }
        if (!ShakeEnabled || Time.timeScale <= 0f || dead) { trauma = 0f; return; }
        trauma = Mathf.Max(0f, trauma - TraumaDecayPerSecond * dt);
        if (trauma <= 0f) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float amount = MaxShakeUnits * trauma * trauma;
        Vector2 jitter = Random.insideUnitCircle * amount;
        appliedOffset = new Vector3(jitter.x, jitter.y, 0f);
        cam.transform.position += appliedOffset;
    }

    // Desfaz o deslocamento do quadro anterior: a camera volta sempre a posicao "real".
    void RemoveCameraOffset()
    {
        if (appliedOffset == Vector3.zero) return;
        if (cam != null) cam.transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
    }

    // Textura radial gerada em codigo: transparente no centro, vermelha nas bordas.
    static Sprite GetVignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Zombit_DamageVignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny) / 1.4142f;   // 0 no centro, 1 nos cantos
                float a = Mathf.SmoothStep(0.38f, 1f, d);
                a *= a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.hideFlags = HideFlags.HideAndDontSave;
        return vignetteSprite;
    }
}
