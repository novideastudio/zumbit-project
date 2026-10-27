using System.Collections;
using UnityEngine;

public class SurvivorAbilities : MonoBehaviour
{
    SurvivorDef def;
    Hero hero;
    Health hp;
    SkillTreeBonuses bonuses = new SkillTreeBonuses();
    WeaponData turretWeapon;
    Hero turretHero;
    float grenadeCd = 3f;
    float grenadeCooldownDuration = 3f;
    bool grenadeInFlight;

    const float GrenadeBaseCooldown = 8f;
    const float GrenadeBaseRadius = 2.5f;
    const float MedicRegenPerSecond = 0.015f; // fracao da vida maxima por segundo

    // Read-only values for the HUD; combat timing remains unchanged.
    public SurvivorDef Definition => def;
    public bool GrenadeInFlight => grenadeInFlight;
    public float GrenadeCooldownRemaining => Mathf.Max(0f, grenadeCd);
    public float GrenadeCooldownDuration => Mathf.Max(0.1f, grenadeCooldownDuration);
    public float GrenadeReadyRatio => Mathf.Clamp01(1f - GrenadeCooldownRemaining / GrenadeCooldownDuration);

    public void Init(SurvivorDef d) => Init(d, null);

    public void Init(SurvivorDef d, SkillTreeBonuses skillBonuses)
    {
        def = d;
        bonuses = skillBonuses ?? new SkillTreeBonuses();
        hero = GetComponent<Hero>();
        hp = GetComponent<Health>();
        if (d.utility == UtilityType.Turret) SpawnTurret();
    }

    void OnDestroy()
    {
        // A torreta usa uma copia (Instantiate) do WeaponData; libera para nao vazar.
        if (turretWeapon != null) Destroy(turretWeapon);
    }

    float GrenadeCooldown => GrenadeBaseCooldown * (1f - Mathf.Clamp(bonuses.abilityCooldownReduction, 0f, 0.6f));

    void Update()
    {
        if (def == null || hp == null || hp.Current <= 0f) return;
        // A torreta recebe uma fracao do bonus fixo de dano do Engineer (nivel/itens), senao ficaria irrelevante nos niveis altos.
        if (turretHero != null && turretWeapon != null && hero != null)
            turretHero.damageBonus = hero.damageBonus * Balance.TurretBonusShare * (1f + bonuses.abilityPowerPercent) / Mathf.Max(0.1f, turretWeapon.BonusScale);
        if (def.utility == UtilityType.Grenade)
        {
            grenadeCd -= Time.deltaTime;
            if (grenadeCd <= 0f && !grenadeInFlight)
            {
                var target = Closest(10f);
                if (target != null)
                {
                    grenadeCd = GrenadeCooldown;
                    grenadeCooldownDuration = GrenadeCooldown;
                    StartCoroutine(ThrowGrenade(target));
                }
            }
        }
        else if (def.utility == UtilityType.FieldMedic)
        {
            float rate = MedicRegenPerSecond * (1f + bonuses.healingPercent) * Time.deltaTime;
            hp.Heal(hp.max * rate);
            // Modo dupla: o Medic cura tambem o parceiro (quem caiu nao volta).
            foreach (var m in HeroSquad.Members)
                if (m.health != null && m.health != hp && m.Alive) m.health.Heal(m.health.max * rate);
        }
    }

    Enemy Closest(float range)
    {
        Enemy best = null;
        float bestD = range * range;
        foreach (var e in Enemy.Alive)
        {
            if (!e) continue;
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    IEnumerator ThrowGrenade(Enemy target)
    {
        grenadeInFlight = true;
        Vector3 start = transform.position + new Vector3(0.35f, 0.65f, -0.2f);
        Vector3 end = target ? target.AimPoint : start + Vector3.right * 2f;
        end.z = start.z;

        GameObject grenade = new GameObject("Grenade_Thrown");
        grenade.transform.position = start;
        var renderer = grenade.AddComponent<SpriteRenderer>();
        renderer.sprite = MakeGrenadeSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = 22000;
        grenade.transform.localScale = Vector3.one * 0.22f;

        // A visible arc: the grenade leaves the survivor's hand, rises, then drops to the target.
        const float flightTime = 0.72f;
        float elapsed = 0f;
        while (elapsed < flightTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);
            Vector3 p = Vector3.Lerp(start, end, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 1.05f;
            grenade.transform.position = p;
            grenade.transform.Rotate(0f, 0f, -720f * Time.deltaTime);
            grenade.transform.localScale = Vector3.one * (0.19f + Mathf.Sin(t * Mathf.PI) * 0.07f);
            yield return null;
        }

        if (grenade) Destroy(grenade);
        // Impact point is the street/feet plane, not the target's upper body.
        Vector3 blastPoint = end;
        blastPoint.y -= 0.35f;
        float radius = GrenadeBaseRadius * (1f + bonuses.grenadeRadiusPercent);
        float damage = (hero ? (hero.weapon.ReferenceDamage + hero.damageBonus) * 3f : 30f) * (1f + bonuses.abilityPowerPercent);
        for (int i = Enemy.Alive.Count - 1; i >= 0; i--)
        {
            var e = Enemy.Alive[i];
            if (e && (e.AimPoint - blastPoint).sqrMagnitude <= radius * radius)
                e.Hit(damage);
        }
        PixelCombatVFX.Explosion(blastPoint, radius);
        grenadeInFlight = false;
    }

    // Cache estatico: antes cada lancamento criava uma Texture2D + Sprite novos e
    // nunca os liberava (vazamento de memoria nativa a cada granada). Agora e um so sprite.
    static Sprite grenadeSprite;
    static Sprite MakeGrenadeSprite()
    {
        if (grenadeSprite != null) return grenadeSprite;
        const int w = 16, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "Zombit_Grenade_PixelSprite";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool body = x >= 4 && x <= 11 && y >= 2 && y <= 12;
            bool shoulder = x >= 5 && x <= 10 && y >= 1 && y <= 13;
            bool neck = x >= 6 && x <= 9 && y >= 12 && y <= 14;
            Color c = Color.clear;
            if (body || shoulder)
            {
                c = (x == 4 || x == 11 || y == 2 || y == 12) ? new Color(0.12f, 0.16f, 0.10f) : new Color(0.30f, 0.43f, 0.20f);
                if ((x + y) % 4 == 0) c = new Color(0.44f, 0.52f, 0.25f);
            }
            if (neck) c = new Color(0.42f, 0.43f, 0.36f);
            pixels[y * w + x] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        grenadeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        return grenadeSprite;
    }

    static Sprite turretSprite;
    // Altura e posicao da torreta: GameTuning.asset (turretHeight / turretOffset).

    // Torreta sentinela em pixel art (24x16): tripe, caixa de municao e cano apontando para a direita.
    static Sprite GetTurretSprite()
    {
        if (turretSprite != null) return turretSprite;
        string[] rows =
        {
            "........................",
            "......kkkkkkk...........",
            ".....kGGGGGGGk..........",
            ".....kGgggggGkkkkkkkkkk.",
            ".....kGggrgggkMMMMMMMMMk",
            ".....kGgggggGkmmmmmmmmmk",
            ".....kgggggggkkkkkkkkkk.",
            "......kkkkmkkk..........",
            ".........kmk............",
            "........kymyk...........",
            ".......kkmmmkk..........",
            "......km.kmk.mk.........",
            ".....km..kmk..mk........",
            "....km...kmk...mk.......",
            "...km....kmk....mk......",
            "..kkk...kkkkk...kkk.....",
        };
        int w = 24, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Zombit_Turret", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y].PadRight(w, '.');
            for (int x = 0; x < w; x++)
            {
                Color c;
                switch (row[x])
                {
                    case 'k': c = new Color(0.07f, 0.08f, 0.07f); break;
                    case 'g': c = new Color(0.33f, 0.40f, 0.22f); break;
                    case 'G': c = new Color(0.50f, 0.57f, 0.32f); break;
                    case 'm': c = new Color(0.30f, 0.32f, 0.35f); break;
                    case 'M': c = new Color(0.58f, 0.60f, 0.64f); break;
                    case 'y': c = new Color(0.95f, 0.74f, 0.20f); break;
                    case 'r': c = new Color(1.00f, 0.22f, 0.18f); break;
                    default:  c = new Color(0f, 0f, 0f, 0f); break;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(false, false);
        // 16 px por unidade => 1,5 x 1 unidade: mais baixa que os sobreviventes, como uma torreta de chao.
        turretSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        return turretSprite;
    }

    void SpawnTurret()
    {
        var tuning = GameTuning.Current;
        var go = new GameObject("Turret");
        var sr = go.AddComponent<SpriteRenderer>();
        // Arte da torreta (Resources/Art/Characters/engineer_turret_full.png, pivot nos pes).
        // Se o arquivo faltar, cai na pixel art gerada em codigo.
        var art = ArtLoader.Get("Art/Characters/engineer_turret_full");
        sr.sprite = art != null ? art : GetTurretSprite();
        sr.color = Color.white;
        // Altura VISIVEL da torreta (sem margem transparente do PNG) = GameTuning.turretHeight (padrao 0,5: ate a canela).
        float k = Mathf.Max(0.1f, tuning.turretHeight) / SpriteMetrics.WorldHeight(sr.sprite);
        go.transform.localScale = new Vector3(k, k, 1f);

        // Pes da torreta = pes do Engineer + turretOffset (um pouco atras e mais para baixo na rua).
        float heroFeetY = hero != null && hero.GetComponent<SpriteRenderer>() is SpriteRenderer hsr && hsr.sprite != null
            ? hsr.bounds.min.y + SpriteMetrics.Get(hsr.sprite).bottomPx / hsr.sprite.pixelsPerUnit * Mathf.Abs(hero.transform.localScale.y)
            : transform.position.y;
        float spriteBottom = SpriteMetrics.Get(sr.sprite).bottomPx / sr.sprite.pixelsPerUnit * k;
        go.transform.position = new Vector3(transform.position.x + tuning.turretOffset.x,
                                            heroFeetY + tuning.turretOffset.y - spriteBottom, 0f);
        go.AddComponent<YSort>();
        var th = go.AddComponent<Hero>();
        // Ponta do cano de baixo (UV do sprite: x da esquerda, y de baixo).
        th.autoMuzzle = false;   // a torreta tem 2 canos: usa o ponto manual do cano de baixo
        th.muzzleUV = art != null ? new Vector2(0.86f, 0.69f) : new Vector2(0.98f, 0.72f);
        var w = Instantiate(hero.weapon);
        // Dano "equivalente M4A1" da arma do heroi: a torreta tem a mesma forca com qualquer arma.
        w.damage = hero.weapon.ReferenceDamage * 0.6f * (1f + bonuses.abilityPowerPercent);
        w.shotsPerSecond = 2.5f * (1f + bonuses.turretFireRatePercent);
        w.range = 7f;
        w.accuracy = 0.7f;
        w.spread = 0.8f;
        w.magazineSize = 30;
        w.reloadTime = 1.5f;
        w.soundPitch = 1.05f;
        th.weapon = w;
        turretWeapon = w;
        turretHero = th;
    }
}
