using UnityEngine;

/// <summary>Sorteio de itens. As probabilidades vivem em Balance.cs (LootWeights): uma tabela por fonte e dificuldade.</summary>
public static class LootTable
{
    /// <param name="locationDifficulty">Dificuldade do LOCAL (1..5).</param>
    /// <param name="levelDifficulty">Nivel escolhido (1 Facil .. 4 Pesadelo).</param>
    /// <param name="source">Conclusao, elite abatida ou chefe (o chefe nunca dropa Comum/Incomum).</param>
    public static ItemInstance Roll(ItemData[] pool, int locationDifficulty, int levelDifficulty, Balance.LootSource source)
    {
        if (pool == null || pool.Length == 0) return null;

        float[] w = Balance.LootWeights(source, locationDifficulty, levelDifficulty);

        float total = 0f;
        foreach (var x in w) total += x;
        float r = Random.value * total;
        int idx = 0;
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] <= 0f) continue;
            idx = i;
            if (r < w[i]) break;
            r -= w[i];
        }

        var target = (Rarity)idx;
        bool nightmare = levelDifficulty >= (int)Difficulty.Pesadelo;
        var list = System.Array.FindAll(pool, p => p != null && p.rarity == target);

        // No Pesadelo, metade das vezes (se houver) o item vem da lista exclusiva daquela raridade.
        if (nightmare)
        {
            var ex = System.Array.FindAll(list, p => p.nightmareOnly);
            if (ex.Length > 0 && Random.value < 0.5f) list = ex;
        }

        if (list.Length == 0) list = System.Array.FindAll(pool, p => p != null);
        if (list.Length == 0) return null;
        return ItemFactory.Create(list[Random.Range(0, list.Length)], nightmare);
    }
}
