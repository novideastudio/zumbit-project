using UnityEngine;

/// <summary>
/// Procedural feedback for single-image pixel-art characters.
/// IMPORTANT: this component must never restore the object's full position each frame,
/// because Enemy/Hero movement is performed by changing Transform.position in Update.
/// </summary>
public sealed class PixelCharacterMotion : MonoBehaviour
{
    public bool isEnemy;
    /// <summary>Cor base do sprite (elites com modificador e chefes furiosos usam tint).</summary>
    public Color tint = Color.white;
    public float bobAmount = 0.025f;
    public float bobSpeed = 7f;

    Vector3 baseScale;
    SpriteRenderer sr;
    Health health;
    float recoil;
    float flash;
    float lastHealth = -1f;
    float lastBob;
    // Brilho de level up: tinge o sprite por um instante e volta ao normal (veja LevelUpEffect).
    float glowAge = -1f, glowDuration = 1.2f;
    Color glowColor = new Color(1f, 0.86f, 0.28f, 1f);
    bool moving;
    bool dead;
    float deathTime;

    void Awake()
    {
        baseScale = transform.localScale;
        sr = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();
        SubscribeHealth();
    }

    void Start()
    {
        // MissionSetup can change the hero's scale after Awake.
        baseScale = transform.localScale;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (health == null) health = GetComponent<Health>();
        SubscribeHealth();
    }

    void SubscribeHealth()
    {
        if (health == null) return;
        health.Changed -= OnHealthChanged;
        health.Died -= OnDied;
        health.Changed += OnHealthChanged;
        health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.Changed -= OnHealthChanged;
            health.Died -= OnDied;
        }
    }

    void OnHealthChanged(float current, float max)
    {
        // So pisca quando a vida DIMINUI. Antes qualquer mudanca com vida < max (incluindo a
        // regeneracao do Medic) mantinha o personagem vermelho continuamente.
        if (lastHealth >= 0f && current < lastHealth) flash = 0.09f;
        lastHealth = current;
    }

    void OnDied()
    {
        dead = true;
        deathTime = 0f;
    }

    public void SetMoving(bool value) { moving = value; }
    public void Recoil() { recoil = 0.11f; }

    /// <summary>Pulsa o sprite na cor dada (2 batidas que se apagam) e volta ao tint normal.</summary>
    public void LevelGlow(Color color, float duration = 1.25f)
    {
        glowColor = color;
        glowDuration = Mathf.Max(0.2f, duration);
        glowAge = 0f;
    }

    // 0..1: duas pulsacoes (a 2a mais fraca) com decaimento suave.
    float GlowAmount()
    {
        if (glowAge < 0f) return 0f;
        float t = glowAge / glowDuration;
        if (t >= 1f) return 0f;
        float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2.2f));
        return pulse * (1f - t) * 0.92f + (1f - Mathf.SmoothStep(0f, 0.18f, t)) * 0.45f;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (flash > 0f) flash -= dt;
        if (recoil > 0f) recoil -= dt;
        if (glowAge >= 0f) { glowAge += Time.unscaledDeltaTime; if (glowAge >= glowDuration) glowAge = -1f; }

        if (dead)
        {
            deathTime += dt;
            float t = Mathf.Clamp01(deathTime / 0.28f);
            float facing = Mathf.Sign(transform.localScale.x);
            if (facing == 0f) facing = 1f;
            transform.localScale = new Vector3(facing * Mathf.Abs(baseScale.x) * (1f + t * 0.12f),
                baseScale.y * (1f - t * 0.7f), baseScale.z);
            if (sr)
            {
                Color c = sr.color;
                c.a = 1f - t;
                sr.color = c;
            }
            return;
        }

        float bob = moving
            ? Mathf.Sin(Time.time * bobSpeed) * bobAmount
            : Mathf.Sin(Time.time * 2.2f) * bobAmount * 0.2f;

        // Apply only the change in bob offset. Preserve movement written by Update.
        Vector3 p = transform.localPosition;
        p.y += bob - lastBob;
        transform.localPosition = p;
        lastBob = bob;

        // Preserve the facing direction set by Enemy.Update while applying recoil.
        float facingSign = Mathf.Sign(transform.localScale.x);
        if (facingSign == 0f) facingSign = 1f;
        float kick = recoil > 0f ? recoil / 0.11f : 0f;
        transform.localScale = new Vector3(
            facingSign * Mathf.Abs(baseScale.x) * (1f + kick * 0.035f),
            baseScale.y * (1f - kick * 0.025f),
            baseScale.z);

        if (sr)
        {
            Color baseColor = flash > 0f
                ? Color.Lerp(tint, new Color(1f, 0.45f, 0.42f), Mathf.Clamp01(flash / 0.09f))
                : tint;
            float g = GlowAmount();
            if (g > 0f) baseColor = Color.Lerp(baseColor, glowColor, Mathf.Clamp01(g));
            if (flash > 0f || g > 0f) sr.color = baseColor;
            else if (sr.color.a >= 0.99f) sr.color = tint;
        }
    }
}
