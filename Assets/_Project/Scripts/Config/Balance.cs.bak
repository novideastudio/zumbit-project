using UnityEngine;

/// <summary>
/// LOCAL UNICO dos numeros de balanceamento: XP/nivel, pontos de habilidade, economia por zona, melhorias,
/// venda, tabelas de loot e ajustes de chefe/classes. Valores calibrados por simulacao (ver relatorio de balanceamento):
/// sao ponto de partida para playtest. Para ajustar o ritmo do jogo, mexa AQUI.
/// </summary>
public static class Balance
{
    // ===================== XP e nivel =====================
    public const int MaxLevel = 30;

    /// <summary>XP para subir do nivel L para L+1: 100 x L^2 (antes: 100 x L^1,35, sem teto).</summary>
    public static int XpForLevel(int level) { level = Mathf.Max(1, level); return 100 * level * level; }

    // ===================== Pontos de habilidade =====================
    /// <summary>Pontos acumulados ao atingir um nivel: 1 a cada 3 niveis (10 no nivel 30). A arvore tem 9 nos.</summary>
    public static int SkillPointsTotal(int level) => Mathf.Max(0, level) / 3;
    public static int SkillPointsBetween(int fromLevel, int toLevel) => Mathf.Max(0, SkillPointsTotal(toLevel) - SkillPointsTotal(fromLevel));
    /// <summary>Nivel minimo para cada tier da arvore (antes: 1 / 3 / 5).</summary>
    public static int SkillTierLevel(int tier) => tier <= 1 ? 1 : tier == 2 ? 5 : 10;

    // ===================== Economia por zona =====================
    // Fatores multiplicam os valores base do EnemyData (XP 25/100/500, ouro 2/10/100) e da sucata (1/4/25).
    // Calibrados para que uma missao Normal render ~ 280 x rec^0,85 XP, ~ 110 x rec^0,65 de ouro e ~ 90 x rec^0,6 de sucata.
    public struct Zone { public float xp, gold, scrap; public Zone(float x, float g, float s) { xp = x; gold = g; scrap = s; } }

    public static Zone ZoneOf(string locationId)
    {
        switch (locationId)
        {
            case "residencial": return new Zone(0.254f, 0.105f, 0.41f);
            case "suburbio":    return new Zone(0.488f, 0.150f, 0.58f);
            case "shopping":    return new Zone(0.518f, 0.155f, 0.56f);
            case "industrial":  return new Zone(0.582f, 0.165f, 0.57f);
            case "hospital":    return new Zone(0.494f, 0.125f, 0.45f);
            case "delegacia":   return new Zone(0.586f, 0.145f, 0.50f);
            case "metro":       return new Zone(0.502f, 0.120f, 0.41f);
            case "centro":      return new Zone(0.508f, 0.115f, 0.40f);
            case "base":        return new Zone(0.334f, 0.075f, 0.25f);
            default:            return new Zone(0.40f, 0.12f, 0.45f);
        }
    }

    public static float XpScale(LocationDef loc) => loc != null ? ZoneOf(loc.id).xp : 0.40f;
    public static float GoldScale(LocationDef loc) => loc != null ? ZoneOf(loc.id).gold : 0.12f;
    public static float ScrapScale(LocationDef loc) => loc != null ? ZoneOf(loc.id).scrap : 0.45f;

    /// <summary>Multiplicador da recompensa de conclusao do local por dificuldade (Facil..Pesadelo). Antes nao escalava.</summary>
    public static float RewardGoldMult(int difficulty)
    {
        switch (Mathf.Clamp(difficulty, 1, 4)) { case 1: return 0.6f; case 2: return 1f; case 3: return 1.5f; default: return 2.2f; }
    }

    // ===================== Inimigos por local =====================
    public const float HealthPerRecLevel = 0.12f, DamagePerRecLevel = 0.10f, BossHealthPerRecLevel = 0.10f;
    /// <summary>Vida-base do chefe = EnemyData.maxHealth (1200) x isto = 1500.</summary>
    public const float BossHealthBase = 1.25f;
    /// <summary>Dano-base do chefe = EnemyData.damage x isto (22 -> ~26).</summary>
    public const float BossDamageScale = 1.18f;
    /// <summary>Mecanicas de chefe: golpe pesado a cada N ataques (x mult) e reforcos aos 75% e 40% de vida.</summary>
    public const int BossHeavyEvery = 6;
    public const float BossHeavyMult = 2.2f;
    public const float BossAddsFirstAt = 0.75f, BossAddsSecondAt = 0.40f;
    public const int BossAddsFirst = 4, BossAddsSecond = 6;

    // ===================== Melhorias (oficina) =====================
    public const float UpgradeStatStep = 1.08f;      // atributos por nivel (antes 1,12)
    public const int UpgradeBaseCost = 20;           // sucata
    public const float UpgradeCostGrowth = 1.30f;    // custo do nivel n = Base x raridade x Growth^n
    public const float UpgradeGoldFraction = 0.5f;   // custo EXTRA em ouro = 50% do custo em sucata
    public static float UpgradeRarityCost(Rarity r)
    {
        switch (r) { case Rarity.Comum: return 1f; case Rarity.Incomum: return 1.5f; case Rarity.Raro: return 2.4f; case Rarity.Epico: return 3.8f; default: return 6f; }
    }
    /// <summary>Nivel maximo de melhoria por raridade (antes: +10 para todas).</summary>
    public static int UpgradeMaxLevel(Rarity r)
    {
        switch (r) { case Rarity.Comum: return 3; case Rarity.Incomum: return 5; case Rarity.Raro: return 7; case Rarity.Epico: return 9; default: return 10; }
    }

    // ===================== Itens: variacao e venda =====================
    public static void ItemRange(Rarity r, out float min, out float max, out float extraChance)
    {
        switch (r)
        {
            case Rarity.Comum:   min = 0.85f; max = 1.00f; extraChance = 0.00f; break;
            case Rarity.Incomum: min = 0.95f; max = 1.15f; extraChance = 0.15f; break;
            case Rarity.Raro:    min = 1.05f; max = 1.30f; extraChance = 0.30f; break;
            case Rarity.Epico:   min = 1.20f; max = 1.45f; extraChance = 0.50f; break;
            default:             min = 1.40f; max = 1.70f; extraChance = 0.75f; break;
        }
    }
    /// <summary>Fracao do valor de venda por raridade (antes 100%): vender lendarios deixa de pagar uma zona inteira.</summary>
    public static float SellMult(Rarity r)
    {
        switch (r) { case Rarity.Comum: return 0.8f; case Rarity.Incomum: return 0.7f; case Rarity.Raro: return 0.5f; case Rarity.Epico: return 0.35f; default: return 0.25f; }
    }

    // ===================== Loot (sorteios no FIM da missao; zumbis nao dropam itens) =====================
    public enum LootSource { Completion, Elite, Boss }

    /// <summary>Probabilidade (%) por raridade de um sorteio de CONCLUSAO, por dificuldade (Facil..Pesadelo), numa zona de dificuldade 1.</summary>
    static readonly float[][] CompletionTable =
    {
        new[] { 68f, 25f,  6f,  0.9f, 0.1f },   // Facil
        new[] { 55f, 29f, 13f,  2.7f, 0.3f },   // Normal
        new[] { 40f, 33f, 20f,  6.2f, 0.8f },   // Dificil
        new[] { 26f, 33f, 29f, 10.5f, 1.5f },   // Pesadelo
    };
    /// <summary>Cada nivel de dificuldade da zona (1..5) soma +8% ao peso de Epico e Lendario.</summary>
    public const float ZoneEpicBonus = 0.08f;

    /// <summary>Sorteios de conclusao: 2 (+1 no Pesadelo, +1 com a chance extra do Explorer), maximo 4.</summary>
    public static int CompletionRolls(int difficulty, float extraChance)
    {
        int n = 2 + (difficulty >= 4 ? 1 : 0) + (Random.value < extraChance ? 1 : 0);
        return Mathf.Min(n, 4);
    }

    /// <summary>Chance de CADA elite abatida gerar 1 sorteio (raridade minima Incomum), ate EliteRollCap por missao.</summary>
    public static float EliteRollChance(int difficulty)
    {
        switch (Mathf.Clamp(difficulty, 1, 4)) { case 1: return 0.03f; case 2: return 0.05f; case 3: return 0.06f; default: return 0.07f; }
    }
    public const int EliteRollCap = 3;

    public static float[] LootWeights(LootSource source, int locationDifficulty, int levelDifficulty)
    {
        var b = CompletionTable[Mathf.Clamp(levelDifficulty, 1, 4) - 1];
        float k = 1f + ZoneEpicBonus * Mathf.Max(0, locationDifficulty - 1);
        float[] w = { b[0], b[1], b[2], b[3] * k, b[4] * k };
        w[0] = Mathf.Max(5f, w[0] - (b[3] + b[4]) * (k - 1f));
        switch (source)
        {
            case LootSource.Elite: return new[] { 0f, w[0] * 0.55f + w[1], w[2] * 1.4f, w[3] * 1.8f, w[4] * 2f };
            case LootSource.Boss:  return new[] { 0f, 0f, w[2] + w[1] * 0.5f, w[3] * 2.5f, w[4] * 3f };
            default: return w;
        }
    }

    // ===================== Ajustes de classe =====================
    /// <summary>Torreta do Engineer: fracao do bonus fixo de dano do heroi (nivel/itens) somada ao dano de cada tiro.</summary>
    public const float TurretBonusShare = 0.6f;
}
