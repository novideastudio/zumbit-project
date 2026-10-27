using System.Collections;
using UnityEngine;

/// <summary>
/// Horda continua (substitui o ThemedSpawner por waves). Sem pausas: um "diretor" gasta CREDITOS por segundo
/// e a taxa cresce com o tempo, ate passar do que o heroi consegue matar. O poder (itens + upgrades) decide
/// ATE QUANDO ele aguenta. A missao termina quando o tempo acaba (fases x phaseSeconds), o chefe cai (se houver)
/// e a tela esta limpa.
///
///  - taxa(t) = baseRate x hordeRate x countMultiplier x (1 + ramp x t)      [zumbis/s]
///  - elite custa 4 creditos; comum 1. A cada nova fase entra um grupo de elites "de graca" (marco da fase).
///  - Cuspidores (atacam de longe) nascem a partir de ~40 s.
///  - Furia: depois de furyStart, +10% dano e +5% velocidade a cada 30 s (teto 2,5x / 1,5x).
/// Todos os numeros base estao no GameTuning (Inspector) e no Difficulty.cs.
/// </summary>
public class HordeSpawner : MonoBehaviour
{
    public Transform hero;
    public Transform[] spawnPoints;
    public string theme;
    [Tooltip("Numero de fases (vem de LocationDef.waves). Duracao total = phases x phaseSeconds.")]
    public int phases = 3;
    public float phaseSeconds = 50f;
    public float baseRate = 0.9f;
    public float ramp = 0.012f;
    public float countMultiplier = 1f;
    public float hordeRate = 1f;
    public bool hasBoss;
    public bool eliteMods;
    [Range(0f, 0.5f)] public float eliteShare = 0.07f;
    [Range(0f, 0.5f)] public float spitterShare = 0.08f;
    public float furyStart;
    public int maxAlive = 55;

    const float EliteCost = 4f, EliteFromSeconds = 25f, SpitterFromSeconds = 40f, MaxCreditsBacklog = 3f;
    static readonly Color SpitterTint = new Color(0.66f, 1f, 0.55f);

    Roster roster;
    float t;
    float credits;
    bool nextIsElite, rolled, bossSpawned;
    Enemy bossRef;
    bool bossAdds1, bossAdds2;

    IEnumerator Start()
    {
        roster = EnemyRoster.Get(theme);
        if (roster == null || roster.commons.Count == 0)
        {
            Debug.LogError($"Zombit: não há inimigos comuns configurados para o tema '{theme}'.");
            yield break;
        }
        if (spawnPoints == null || spawnPoints.Length == 0 || hero == null)
        {
            Debug.LogError("Zombit: HordeSpawner precisa de um herói e de pontos de nascimento.");
            yield break;
        }

        phases = Mathf.Max(1, phases);
        float duration = phases * Mathf.Max(10f, phaseSeconds);
        float bossAt = duration - Mathf.Max(10f, phaseSeconds) + 15f;   // chefe entra 15 s depois do inicio da ultima fase
        int phase = 0;

        GameEvents.HordeMode = true;
        GameEvents.HordeTimeLeft = duration;
        GameEvents.HordeFury = false;

        while (true)
        {
            float dt = Time.deltaTime;
            t += dt;

            // ---- fase (so informativo + grupo de elites no marco) ----
            int ph = Mathf.Min(phases, 1 + (int)(t / Mathf.Max(10f, phaseSeconds)));
            if (ph != phase)
            {
                phase = ph;
                GameEvents.WaveChanged?.Invoke(phase, phases);
                if (phase >= 2) StartCoroutine(PhaseBurst(phase));
            }
            GameEvents.HordeTimeLeft = Mathf.Max(0f, duration - t);

            // ---- furia ----
            if (furyStart > 0f && t >= furyStart)
            {
                float k = (t - furyStart) / 30f;
                Enemy.FuryDamage = Mathf.Min(2.5f, 1f + 0.10f * k);
                Enemy.FurySpeed = Mathf.Min(1.5f, 1f + 0.05f * k);
                GameEvents.HordeFury = true;
            }

            bool timeUp = t >= duration;

            // ---- chefe ----
            if (hasBoss && roster.boss != null && !bossSpawned && t >= bossAt)
            {
                bossSpawned = true;
                bossRef = Spawn(roster.boss, false);
            }

            // ---- reforcos do chefe (75% e 40% da vida): um grupo de comuns entra de uma vez ----
            if (bossRef != null && !bossRef.IsDead && roster.commons.Count > 0)
            {
                Health bossHealth = bossRef.GetComponent<Health>();
                float r = bossHealth != null && bossHealth.max > 0f
                    ? Mathf.Clamp01(bossHealth.Current / bossHealth.max)
                    : 1f;
                if (!bossAdds1 && r <= Balance.BossAddsFirstAt) { bossAdds1 = true; StartCoroutine(BossAdds(Balance.BossAddsFirst)); }
                if (!bossAdds2 && r <= Balance.BossAddsSecondAt) { bossAdds2 = true; StartCoroutine(BossAdds(Balance.BossAddsSecond)); }
            }

            // ---- diretor: gasta creditos ----
            if (!timeUp)
            {
                float rate = baseRate * hordeRate * countMultiplier * (1f + ramp * t);
                credits = Mathf.Min(credits + rate * dt, MaxCreditsBacklog + EliteCost);
                while (Enemy.Alive.Count < maxAlive)
                {
                    if (!rolled)
                    {
                        nextIsElite = roster.elites.Count > 0 && t >= EliteFromSeconds && Random.value < eliteShare;
                        rolled = true;
                    }
                    float cost = nextIsElite ? EliteCost : 1f;
                    if (credits < cost) break;
                    credits -= cost;
                    rolled = false;
                    if (nextIsElite) Spawn(roster.elites[Random.Range(0, roster.elites.Count)], false);
                    else Spawn(roster.commons[Random.Range(0, roster.commons.Count)], true);
                }
            }
            else
            {
                // Tempo acabou: o chefe precisa ter entrado e morrido, e a tela estar limpa.
                if (hasBoss && roster.boss != null && !bossSpawned)
                {
                    bossSpawned = true;
                    bossRef = Spawn(roster.boss, false);
                }
                bool bossDone = !hasBoss || roster.boss == null || (bossSpawned && (bossRef == null || bossRef.IsDead));
                if (bossDone && Enemy.Alive.Count == 0) break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);
        GameEvents.MissionComplete?.Invoke();
    }

    IEnumerator BossAdds(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Spawn(roster.commons[Random.Range(0, roster.commons.Count)], false);
            yield return new WaitForSeconds(0.35f);
        }
    }

    /// <summary>Marco de fase: um grupo de elites entra de uma vez (sem gastar creditos).</summary>
    IEnumerator PhaseBurst(int phase)
    {
        if (roster.elites.Count == 0) yield break;
        int n = 1 + phase / 2;
        for (int i = 0; i < n; i++)
        {
            Spawn(roster.elites[Random.Range(0, roster.elites.Count)], false);
            yield return new WaitForSeconds(0.9f);
        }
    }

    void OnDisable()
    {
        GameEvents.HordeMode = false;
        GameEvents.HordeTimeLeft = -1f;
        GameEvents.HordeFury = false;
        Enemy.FuryDamage = 1f;
        Enemy.FurySpeed = 1f;
    }

    Enemy Spawn(EnemyEntry e, bool canBeSpitter)
    {
        if (e == null || e.sprite == null || e.data == null) return null;
        var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return null;

        // Mesma regra do ThemedSpawner: logo fora da borda direita, sem variar a altura da pista.
        Vector3 pos = point.position;
        pos.x = Mathf.Max(pos.x, CameraFramer.RightSpawnX(pos.x));
        pos.x += Random.Range(-0.15f, 0.15f);

        var mod = (eliteMods && e.tier == Tier.Elite) ? EnemyFactory.RandomMod() : EliteMod.None;
        var enemy = EnemyFactory.Spawn(e, pos, hero, mod);

        // Parte dos comuns nasce como cuspidor (ataca de longe): obriga o jogador a priorizar alvos.
        if (enemy != null && canBeSpitter && t >= SpitterFromSeconds && e.tier == Tier.Common && Random.value < spitterShare)
        {
            enemy.kindOverride = EnemyKind.Spitter;
            var motion = enemy.GetComponent<PixelCharacterMotion>();
            if (motion != null) motion.tint = SpitterTint;
        }
        return enemy;
    }
}
