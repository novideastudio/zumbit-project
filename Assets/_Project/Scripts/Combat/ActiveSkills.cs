using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Habilidades ATIVAS do jogador, para a dificuldade vir de decisao e nao so de repetir ate ficar forte:
///  - CLIQUE em um zumbi: foca o tiro nele (elites explosivos/blindados, chefe). Clique no vazio solta o foco.
///  - ESPACO: Onda de choque. Empurra e atordoa quem esta perto (cooldown). Bom contra flanco e filas.
/// Valores iniciais: ajuste com playtest.
/// </summary>
public sealed class ActiveSkills : MonoBehaviour
{
    public float shockCooldown = 14f;
    public float shockRadius = 3.4f;
    public float shockKnockback = 2.4f;
    public float shockStun = 1.3f;
    public float shockDamageMult = 1.5f;      // x (dano da arma + bonus)
    public float focusPickRadius = 1.6f;

    Hero hero;
    Health heroHealth;
    MissionFlow flow;
    float shockReadyAt;
    SpriteRenderer reticle;

    void Start()
    {
        hero = GetComponent<Hero>();
        heroHealth = GetComponent<Health>();

        var go = new GameObject("FocusReticle");
        reticle = go.AddComponent<SpriteRenderer>();
        reticle.sprite = MakeRing();
        reticle.color = new Color(1f, 0.25f, 0.2f, 0.95f);
        reticle.sortingOrder = 22500;
        go.SetActive(false);

        // O status da onda de choque agora fica no HUD (botao clicavel embaixo a esquerda).
    }

    void OnDestroy()
    {
        if (reticle != null) Destroy(reticle.gameObject);
    }

    void Update()
    {
        if (hero == null || Time.timeScale == 0f) return;
        if (heroHealth != null && heroHealth.Current <= 0f) return;
        if (flow == null) flow = FindFirstObjectByType<MissionFlow>();
        if (flow != null && flow.IsFinished) return;

        HandleFocusClick();
        UpdateReticle();

        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame && Time.time >= shockReadyAt) ShockWave();
    }

    // ---- usado pelo HUD (botao da onda de choque) ----
    public float ShockCooldownRemaining => Mathf.Max(0f, shockReadyAt - Time.time);
    public float ShockReadyRatio => shockCooldown <= 0f ? 1f : Mathf.Clamp01(1f - ShockCooldownRemaining / shockCooldown);

    /// <summary>Dispara a onda de choque pelo clique no HUD (mesmas regras da tecla ESPACO).</summary>
    public bool TryShockWave()
    {
        if (hero == null || Time.timeScale == 0f) return false;
        if (heroHealth != null && heroHealth.Current <= 0f) return false;
        if (flow == null) flow = FindFirstObjectByType<MissionFlow>();
        if (flow != null && flow.IsFinished) return false;
        if (Time.time < shockReadyAt) return false;
        ShockWave();
        return true;
    }

    void HandleFocusClick()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 w = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        w.z = 0f;
        Enemy best = null;
        float bestD = focusPickRadius * focusPickRadius;
        foreach (var e in Enemy.Alive)
        {
            if (e == null || e.IsDead) continue;
            Vector3 a = e.AimPoint; a.z = 0f;
            float d = (a - w).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        hero.focusTarget = best; // null = solta o foco
        // Modo dupla: o parceiro tambem foca o mesmo zumbi (fogo concentrado).
        foreach (var m in HeroSquad.Members)
            if (m.hero != null && m.hero != hero) m.hero.focusTarget = best;
    }

    void UpdateReticle()
    {
        var t = hero.focusTarget;
        if (t == null || t.IsDead) { hero.focusTarget = null; reticle.gameObject.SetActive(false); return; }
        reticle.gameObject.SetActive(true);
        Vector3 p = t.AimPoint; p.z = 0f;
        reticle.transform.position = p;
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 9f);
        reticle.transform.localScale = Vector3.one * (t.IsBoss ? 1.5f : 0.8f) * pulse;
    }

    void ShockWave()
    {
        shockReadyAt = Time.time + shockCooldown;
        Vector3 origin = transform.position;
        // Dano equivalente M4A1: a onda de choque tem a mesma forca com qualquer arma.
        float dmg = (hero.weapon != null ? hero.weapon.ReferenceDamage : 10f) + hero.damageBonus;
        dmg *= Mathf.Max(0.1f, hero.damageMult) * shockDamageMult;

        for (int i = Enemy.Alive.Count - 1; i >= 0; i--)
        {
            var e = Enemy.Alive[i];
            if (e == null || e.IsDead) continue;
            if (Mathf.Abs(e.transform.position.x - origin.x) > shockRadius) continue;
            e.Hit(dmg);
            if (!e.IsDead) e.Knockback(shockKnockback, shockStun);
        }
        PixelCombatVFX.Shockwave(origin + new Vector3(0f, -0.4f, 0f), shockRadius);
    }

    static Sprite ringSprite;
    static Sprite MakeRing()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            bool ring = d > 12f && d < 15.5f;
            bool cross = (Mathf.Abs(x - c) < 1.2f || Mathf.Abs(y - c) < 1.2f) && d > 7f && d < 15.5f && !(d > 9f && d < 12f);
            px[y * n + x] = (ring || cross) ? Color.white : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 24f);
        return ringSprite;
    }
}
