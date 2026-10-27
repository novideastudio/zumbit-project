using UnityEngine;

// Zumbis NAO dropam mais itens. Aqui ficam so os contadores da missao (ouro, XP, abates)
// e a lista de itens-base (pool) usada para sortear as recompensas finais.
public class LootDropper : MonoBehaviour
{
    public ItemData[] pool;
    public Inventory inventory;
    public int gold, xp, kills;
    /// <summary>Sucata da missao: fica com o jogador mesmo se ele morrer (ver MissionFlow).</summary>
    public int scrap;
    public float scrapMult = 1f;
    public float goldMult = 1f;
    /// <summary>Afixos de ouro dos itens equipados (somado ao goldMult).</summary>
    public float affixGoldPercent;
    /// <summary>Afixos de ouro do APOIO (modo dupla), somados aos do principal.</summary>
    public float partnerAffixGoldPercent;
    /// <summary>Afixos de ouro do terceiro sobrevivente (modo trio).</summary>
    public float thirdAffixGoldPercent;
    public float dropChanceMult = 1f;
    /// <summary>Elites e chefes abatidos: geram sorteios extras no FIM da missao (ver MissionFlow / Balance).</summary>
    public int elitesKilled, bossesKilled;

    // Valores por abate sao pequenos (ex.: 0,1 de ouro): acumula a fracao para nao arredondar para cima.
    float goldAcc, scrapAcc;

    void OnEnable() => GameEvents.EnemyKilled += OnKill;
    void OnDisable() => GameEvents.EnemyKilled -= OnKill;

    void OnKill(Enemy e)
    {
        kills++;
        if (e == null || e.data == null) return; // inimigo sem EnemyData ainda conta como abate
        if (e.data.tier == Tier.Elite) elitesKilled++;
        else if (e.data.tier == Tier.Boss) bossesKilled++;

        var loc = GameSession.Location;
        goldAcc += e.data.gold * Balance.GoldScale(loc) * goldMult * (1f + affixGoldPercent + partnerAffixGoldPercent + thirdAffixGoldPercent);
        int g = Mathf.FloorToInt(goldAcc); gold += g; goldAcc -= g;
        xp += Mathf.RoundToInt(e.data.xp * Balance.XpScale(loc));
        scrapAcc += ScrapRules.ForKillF(e, scrapMult);
        int sc = Mathf.FloorToInt(scrapAcc); scrap += sc; scrapAcc -= sc;
    }
}
