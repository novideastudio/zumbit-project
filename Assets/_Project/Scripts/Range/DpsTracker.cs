using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Acumula o dano causado nos bonecos do estande e calcula DPS, maior golpe, critico e tempo de abate (TTK).
/// O cronometro (Elapsed) comeca no PRIMEIRO dano, nao tem limite e congela quando pausado; o DPS usa esse relogio.
/// Logica pura (sem UI): o ShootingRange so le os valores.
/// </summary>
public sealed class DpsTracker
{
    public const float Window = 5f;

    struct Sample { public float t, dmg; }
    readonly Queue<Sample> window = new Queue<Sample>();
    float windowSum;
    float ttkSum;

    public bool Started { get; private set; }
    public bool Paused { get; private set; }
    /// <summary>Segundos medidos (desde o 1o dano, sem contar o tempo pausado).</summary>
    public float Elapsed { get; private set; }
    public float Total { get; private set; }
    public float MaxHit { get; private set; }
    public float PeakDps { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public int Kills { get; private set; }
    public float LastTtk { get; private set; }

    public float AvgHit => Hits > 0 ? Total / Hits : 0f;
    public float CritRate => Hits > 0 ? (float)Crits / Hits : 0f;
    public float AvgTtk => Kills > 0 ? ttkSum / Kills : 0f;

    public void Reset()
    {
        window.Clear();
        windowSum = 0f; ttkSum = 0f;
        Started = false; Paused = false;
        Elapsed = 0f; Total = 0f; MaxHit = 0f; PeakDps = 0f;
        Hits = 0; Crits = 0; Kills = 0; LastTtk = 0f;
    }

    public void SetPaused(bool paused) => Paused = paused;

    /// <summary>Chamar todo frame com Time.deltaTime. So avanca depois do 1o dano e fora da pausa.</summary>
    public void Tick(float dt)
    {
        if (!Started || Paused) return;
        Elapsed += dt;
        float w = DpsWindow();
        if (Elapsed >= 1f && w > PeakDps) PeakDps = w;
    }

    /// <summary>Dano realmente removido do boneco (ja com defesa). Ignorado enquanto pausado.</summary>
    public void Record(float applied, bool crit)
    {
        if (Paused || applied <= 0f) return;
        Started = true;
        Total += applied;
        Hits++;
        if (crit) Crits++;
        if (applied > MaxHit) MaxHit = applied;
        window.Enqueue(new Sample { t = Elapsed, dmg = applied });
        windowSum += applied;
    }

    public void RecordKill(float ttk)
    {
        if (Paused) return;
        Kills++;
        LastTtk = ttk;
        ttkSum += ttk;
    }

    /// <summary>DPS dos ultimos 5 s do cronometro (nos primeiros segundos divide pelo tempo decorrido, nao por 5).</summary>
    public float DpsWindow()
    {
        if (!Started) return 0f;
        while (window.Count > 0 && Elapsed - window.Peek().t > Window) windowSum -= window.Dequeue().dmg;
        if (window.Count == 0) return 0f;
        float span = Mathf.Clamp(Elapsed, 0.5f, Window);
        return Mathf.Max(0f, windowSum) / span;
    }

    /// <summary>DPS medio = dano total / tempo do cronometro (cai se voce parar de atirar; use PAUSAR).</summary>
    public float DpsSession => Started ? Total / Mathf.Max(0.5f, Elapsed) : 0f;

    // ---- DPS teorico a partir dos atributos do heroi (mesma formula do Hero.Fire) ----

    /// <summary>burst = sem recarga e sem erros; sustained = com recarga. Ambos ja com critico medio.</summary>
    public static void Theoretical(Hero h, out float burst, out float sustained, out float avgHit)
    {
        burst = sustained = avgHit = 0f;
        if (h == null || h.weapon == null) return;
        var w = h.weapon;
        float dmg = (w.damage + h.damageBonus * w.BonusScale) * h.damageMult * (1f + h.affixDamage);
        float cc = Mathf.Clamp01(h.critChance + h.affixCritChance);
        float cm = Mathf.Max(1f, h.critMult * (1f + h.affixCritDamage));
        avgHit = dmg * (1f + cc * (cm - 1f));
        float sps = Mathf.Max(0.1f, w.shotsPerSecond * h.fireRateMult * (1f + h.affixFireRate));
        burst = avgHit * sps;
        int mag = Mathf.Max(1, w.magazineSize);
        float cycle = mag / sps + Mathf.Max(0f, w.reloadTime);
        sustained = avgHit * mag / Mathf.Max(0.01f, cycle);
    }
}
