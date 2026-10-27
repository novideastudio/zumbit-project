using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Esquadrao da missao: 1 sobrevivente (solo), 2 (modo dupla) ou 3 (modo trio, so no Pesadelo).
///  - O PRINCIPAL fica na frente e toma o dano primeiro; o APOIO fica um pouco atras.
///  - Os zumbis sempre perseguem o sobrevivente VIVO mais a frente (FrontTarget).
///  - XP de cada abate e dividida (50/50 na dupla, 40% para cada no trio); ouro, abates e mochila sao compartilhados.
///  - Cada um usa a propria arma e a propria habilidade (granada, cura, torreta...).
///  - Se um cair, o outro continua (e herda a onda de choque/feedback de tela). A missao so
///    e perdida quando TODOS caem. No trio a ordem de dano e: frente -> meio -> fundo.
/// </summary>
public static class HeroSquad
{
    /// <summary>Multiplicador de zumbis no modo dupla (dois atiradores = quase o dobro de DPS).</summary>
    public const float DuoHordeMult = 1.5f;
    /// <summary>Fracao da XP de cada abate que cada sobrevivente recebe no modo dupla (50/50).</summary>
    public const float DuoXpShare = 0.5f;
    /// <summary>Modo trio: so neste nivel de dificuldade (4 = Pesadelo).</summary>
    public const int TrioDifficulty = (int)Difficulty.Pesadelo;
    /// <summary>Multiplicador de zumbis no modo trio (tres atiradores).</summary>
    public const float TrioHordeMult = 2.0f;
    /// <summary>Vida extra dos zumbis E chefes no modo trio (so mais zumbis nao segura 3x de DPS).</summary>
    public const float TrioZombieHealthMult = 1.25f;
    /// <summary>XP de cada abate para CADA sobrevivente no trio (40% x 3 = 120%: compensa a divisao em tres).</summary>
    public const float TrioXpShare = 0.4f;
    // Posicoes da formacao (X, passo X/Y em cascata) agora vem de GameTuning.asset. As constantes abaixo sao so o valor padrao antigo.
    /// <summary>Distancia (unidades) do apoio atras do principal.</summary>
    public const float PartnerOffsetX = 1.35f;
    /// <summary>Trio: tudo um pouco mais para a direita e mais junto, para o terceiro caber na tela.</summary>
    public const float TrioFrontX = -5.8f;
    public const float TrioOffsetX = 1.15f;
    /// <summary>Leve recuo vertical do apoio: desenha atras do principal (profundidade) sem sair da rua.</summary>
    public const float PartnerOffsetY = 0.08f;
    /// <summary>
    /// Sobrevivente que CAIU numa missao vencida: perde o progresso da missao e rola a perda de equipamento,
    /// como numa derrota (morte e morte). Ponha false para o time "resgatar" quem caiu.
    /// </summary>
    public const bool FallenAllyPenalizedOnWin = true;

    public sealed class Member
    {
        public PlayerStats stats;
        public Hero hero;
        public Health health;
        public SurvivorDef def;
        public bool isPrimary;
        public int startLevel, startXp;
        public bool Alive => health != null && health.Current > 0f;
        public string Name => def != null ? def.name : "Sobrevivente";
    }

    static readonly List<Member> members = new List<Member>();
    public static IReadOnlyList<Member> Members => members;
    public static int Count => members.Count;
    public static bool IsDuo => members.Count > 1;
    public static Member PrimaryMember => members.Count > 0 ? members[0] : null;
    public static Member PartnerMember => members.Count > 1 ? members[1] : null;
    public static PlayerStats Primary => PrimaryMember != null ? PrimaryMember.stats : null;
    public static bool IsTrio => members.Count > 2;
    /// <summary>Apoio n (1 = meio, 2 = fundo) ou null.</summary>
    public static Member Support(int n) => n >= 1 && n < members.Count ? members[n] : null;

    // ---- balanceamento por tamanho do esquadrao ----
    public static float XpShareFor(int squadSize) => squadSize >= 3 ? TrioXpShare : squadSize == 2 ? DuoXpShare : 1f;
    public static float HordeMultFor(int squadSize) => squadSize >= 3 ? TrioHordeMult : squadSize == 2 ? DuoHordeMult : 1f;
    public static float ZombieHealthMultFor(int squadSize) => squadSize >= 3 ? TrioZombieHealthMult : 1f;
    public static float FrontXFor(int squadSize) => squadSize >= 3 ? GameTuning.Current.trioFrontX : GameTuning.Current.duoFrontX;
    public static float OffsetXFor(int squadSize) => squadSize >= 3 ? GameTuning.Current.trioStepX : GameTuning.Current.duoStepX;

    /// <summary>Disparado quando um sobrevivente cai e ainda ha outro de pe.</summary>
    public static event Action<Member> AllyDown;

    public static void Clear()
    {
        members.Clear();
        AllyDown = null;
    }

    public static Member Register(PlayerStats stats, SurvivorDef def, bool primary, int startLevel, int startXp)
    {
        if (stats == null) return null;
        var m = new Member
        {
            stats = stats, hero = stats.GetComponent<Hero>(), health = stats.GetComponent<Health>(),
            def = def, isPrimary = primary, startLevel = startLevel, startXp = startXp
        };
        if (primary) members.Insert(0, m); else members.Add(m);
        if (m.health != null) m.health.Died += () => OnMemberDied(m);
        return m;
    }

    public static Member Find(PlayerStats stats)
    {
        foreach (var m in members) if (m.stats == stats) return m;
        return null;
    }

    public static int AliveCount
    {
        get { int n = 0; foreach (var m in members) if (m.Alive) n++; return n; }
    }

    /// <summary>Sobrevivente vivo mais a direita (de frente para a horda).</summary>
    public static Member FrontMember()
    {
        Member best = null;
        foreach (var m in members)
        {
            if (!m.Alive || m.stats == null) continue;
            if (best == null || m.stats.transform.position.x > best.stats.transform.position.x) best = m;
        }
        return best;
    }

    public static Transform FrontTarget()
    {
        var m = FrontMember();
        return m != null ? m.stats.transform : null;
    }

    /// <summary>Vida somada do time (0..1). Quem caiu conta como 0.</summary>
    public static float TeamHealthRatio()
    {
        float cur = 0f, max = 0f;
        foreach (var m in members)
        {
            if (m.health == null) continue;
            cur += Mathf.Max(0f, m.health.Current);
            max += Mathf.Max(1f, m.health.max);
        }
        return max > 0f ? Mathf.Clamp01(cur / max) : 1f;
    }

    static void OnMemberDied(Member dead)
    {
        if (AliveCount == 0)
        {
            // Ultimo sobrevivente caiu: derrota (antes era o MissionController que fazia isso).
            GameEvents.MissionFailed?.Invoke();
            Time.timeScale = 0f;
            return;
        }

        var next = FrontMember();
        if (next == null || next.stats == null || dead.stats == null) return;

        // Vinheta/tremor de tela passam para quem ficou de pe (a do caido ficaria vermelha para sempre).
        var deadFx = dead.stats.GetComponent<HeroDamageFeedback>();
        if (deadFx != null)
        {
            UnityEngine.Object.Destroy(deadFx);
            if (next.stats.GetComponent<HeroDamageFeedback>() == null) next.stats.gameObject.AddComponent<HeroDamageFeedback>();
        }
        // Onda de choque [ESPACO] e foco por clique tambem.
        var deadSkills = dead.stats.GetComponent<ActiveSkills>();
        if (deadSkills != null)
        {
            UnityEngine.Object.Destroy(deadSkills);
            if (next.stats.GetComponent<ActiveSkills>() == null) next.stats.gameObject.AddComponent<ActiveSkills>();
        }
        AllyDown?.Invoke(dead);
    }
}
