using System.Text;
using UnityEngine;

/// <summary>
/// Progressao do modo horda: SUCATA (dropa dos zumbis, nunca se perde), UPGRADE de itens na oficina
/// e PODER (numero unico para comparar o heroi com o que o local/nivel pede).
/// Todos os numeros estao no topo de cada classe: ajuste com playtest.
/// </summary>
public static class ScrapRules
{
    /// <summary>Sucata base por abate (comum/elite/chefe). O valor final e multiplicado pela zona (Balance.ZoneOf) e pela dificuldade.</summary>
    public const float CommonScrap = 1f, EliteScrap = 4f, BossScrap = 25f;
    /// <summary>Bonus de sucata ao VENCER (derrota guarda 100%, vitoria rende 15% a mais).</summary>
    public const float WinBonus = 1.15f;

    public static float MissionMult(DifficultyDef d, LocationDef loc)
        => (d != null ? d.scrapMult : 1f) * Balance.ScrapScale(loc);

    /// <summary>Sucata FRACIONARIA do abate (o LootDropper acumula a fracao, senao os valores pequenos arredondariam para cima).</summary>
    public static float ForKillF(Enemy e, float mult)
    {
        float b = CommonScrap;
        if (e != null && e.data != null)
            b = e.data.tier == Tier.Boss ? BossScrap : e.data.tier == Tier.Elite ? EliteScrap : CommonScrap;
        return b * Mathf.Max(0f, mult) * Random.Range(0.8f, 1.4f);
    }

    public static int ForKill(Enemy e, float mult) => Mathf.Max(0, Mathf.RoundToInt(ForKillF(e, mult)));
}

/// <summary>
/// Oficina: cada nivel de upgrade aumenta Dano/Vida/Defesa do item em ~8%. O nivel maximo depende da raridade
/// (Comum +3 ... Lendario +10) e cada melhoria custa SUCATA + OURO. Numeros em Balance.cs.
/// </summary>
public static class ItemUpgrade
{
    public const int MaxLevel = 10;                      // teto absoluto (Lendario)
    public const float AffixMilestone = 1.10f;           // afixos ganham +10% nos niveis 5 e 10

    public static int MaxFor(ItemInstance it) => it == null ? 0 : Balance.UpgradeMaxLevel(it.rarity);

    public static int Cost(ItemInstance it)
    {
        if (it == null) return 0;
        return Mathf.RoundToInt(Balance.UpgradeBaseCost * Balance.UpgradeRarityCost(it.rarity)
            * Mathf.Pow(Balance.UpgradeCostGrowth, Mathf.Max(0, it.upgradeLevel)));
    }

    public static int GoldCost(ItemInstance it) => Mathf.RoundToInt(Cost(it) * Balance.UpgradeGoldFraction);

    static float GrowDec(float v)
    {
        if (v <= 0.0001f) return v;
        return Mathf.Max(v + 0.1f, Mathf.Round(v * Balance.UpgradeStatStep * 10f) / 10f);
    }

    static float GrowInt(float v)
    {
        if (v <= 0.0001f) return v;
        return Mathf.Max(v + 1f, Mathf.Round(v * Balance.UpgradeStatStep));
    }

    /// <summary>Gasta sucata + ouro e melhora o item NO LUGAR (mochila ou equipado). Quem chama salva o jogo.</summary>
    public static bool TryUpgrade(SaveData save, ItemInstance it, out string message)
    {
        message = "";
        if (save == null || it == null) return false;
        if (it.upgradeLevel >= MaxFor(it)) { message = "Este item já está no nível máximo para a sua raridade."; return false; }
        int cost = Cost(it), gold = GoldCost(it);
        if (save.scrap < cost) { message = $"Sucata insuficiente: precisa de {cost}, você tem {save.scrap}."; return false; }
        if (save.gold < gold) { message = $"Ouro insuficiente: precisa de {gold}, você tem {save.gold}."; return false; }

        save.scrap -= cost;
        save.gold -= gold;
        it.upgradeLevel++;
        it.bonusDamage = GrowDec(it.bonusDamage);
        it.bonusDefense = GrowDec(it.bonusDefense);
        it.bonusHealth = GrowInt(it.bonusHealth);
        it.sellValue = Mathf.Max(it.sellValue, Mathf.RoundToInt(it.sellValue * 1.1f));
        if ((it.upgradeLevel == 5 || it.upgradeLevel == 10) && it.HasAffixes)
            foreach (var a in it.affixes) if (a != null) a.value *= AffixMilestone;

        message = $"{it.name} melhorado para +{it.upgradeLevel} (-{cost} sucata, -{gold} ouro).";
        return true;
    }
}

/// <summary>
/// PODER = um numero unico para comparar o heroi com o local/nivel (aproximado: ignora arvore de habilidades e arma).
/// Serve para o jogador saber que precisa FARMAR antes de tentar o proximo nivel.
/// </summary>
public static class PowerRating
{
    public const float PerLevel = 12f, PerDamage = 10f, PerHealth = 0.8f, PerDefense = 6f, PerAffix = 250f, PerRegen = 3000f;
    // Poder recomendado = (Base + PerRecLevel x (nivel recomendado - 1)) x multiplicador do nivel de dificuldade.
    public const float RecBase = 110f, RecPerLevel = 22f;
    static readonly float[] TierMult = { 0.7f, 1f, 1.7f, 2.8f };   // Facil, Normal, Dificil, Pesadelo

    public static int Of(SaveData save, string survivorId)
    {
        if (save == null) return 0;
        var p = save.GetProgress(string.IsNullOrEmpty(survivorId) ? save.selectedSurvivor : survivorId);
        float power = 40f + PerLevel * (Mathf.Max(1, p.level) - 1);
        if (p.equipped != null)
            foreach (var it in p.equipped)
            {
                if (it == null) continue;
                power += it.bonusDamage * PerDamage + it.bonusHealth * PerHealth + it.bonusDefense * PerDefense;
                if (it.affixes != null)
                    foreach (var a in it.affixes)
                        if (a != null) power += a.value * (a.type == AffixType.Regen ? PerRegen : PerAffix);
            }
        return Mathf.RoundToInt(power);
    }

    public static int Recommended(LocationDef loc, int difficulty)
    {
        int rl = loc != null ? Mathf.Max(1, loc.recommendedLevel) : 1;
        float tier = TierMult[Mathf.Clamp(difficulty, 1, 4) - 1];
        return Mathf.RoundToInt((RecBase + RecPerLevel * (rl - 1)) * tier);
    }

    /// <summary>Linha de texto (rich text) para o painel de dificuldade do mapa.</summary>
    public static string Describe(SaveData save, string survivorId, LocationDef loc, int difficulty)
    {
        int have = Of(save, survivorId), need = Recommended(loc, difficulty);
        float r = need > 0 ? (float)have / need : 1f;
        string col = r >= 1f ? "#6CFF8A" : r >= 0.7f ? "#FFD84A" : "#FF6B6B";
        string hint = r >= 1f ? "" : r >= 0.7f ? "  (arriscado: melhore itens na oficina)" : "  (fraco demais: farme sucata em níveis mais fáceis)";
        var sb = new StringBuilder("\n");
        sb.Append($"Poder <color={col}><b>{have}</b></color>  ·  recomendado <b>{need}</b>{hint}");
        return sb.ToString();
    }
}
