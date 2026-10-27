using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conteudo extra de itens definido em codigo (sem precisar criar .asset): amplia o pool base de 8 itens
/// e adiciona os itens exclusivos do Pesadelo. Os itens usam o icone generico do slot.
/// Para virar asset depois, basta criar ItemData com os mesmos valores e arrastar para o LootDropper.
/// </summary>
public static class ItemCatalog
{
    static List<ItemData> general, exclusive;

    static ItemData Make(string name, string desc, ItemSlot slot, Rarity r, float dmg, float hp, float def, int sell,
        bool nightmare = false, AffixType? sig = null, float sigValue = 0f)
    {
        var d = ScriptableObject.CreateInstance<ItemData>();
        d.hideFlags = HideFlags.HideAndDontSave;
        d.name = name;
        d.displayName = name; d.description = desc; d.slot = slot; d.rarity = r;
        d.bonusDamage = dmg; d.bonusHealth = hp; d.bonusDefense = def; d.sellValue = sell;
        d.nightmareOnly = nightmare;
        if (sig.HasValue) { d.hasSignature = true; d.signatureAffix = sig.Value; d.signatureValue = sigValue; }
        return d;
    }

    static void Build()
    {
        general = new List<ItemData>
        {
            Make("Mira Laser", "Ponteiro laser simples. Ajuda a firmar a pontaria.", ItemSlot.Arma, Rarity.Comum, 1.5f, 0, 0, 14),
            Make("Coronha Reforçada", "Reduz o coice e melhora o controle.", ItemSlot.Arma, Rarity.Incomum, 3f, 0, 0, 38),
            Make("Silenciador Tático", "Tiros mais discretos e letais.", ItemSlot.Arma, Rarity.Raro, 5f, 0, 0, 110),
            Make("Capacete de Kevlar", "Proteção leve contra mordidas e estilhaços.", ItemSlot.Capacete, Rarity.Incomum, 0, 18, 3, 42),
            Make("Visor Noturno", "Enxerga alvos que outros perdem.", ItemSlot.Capacete, Rarity.Raro, 2f, 20, 3, 115),
            Make("Capacete Balístico", "Placa de cerâmica sobre kevlar.", ItemSlot.Capacete, Rarity.Epico, 0, 35, 6, 230),
            Make("Colete Balístico", "Placas de cerâmica de uso militar.", ItemSlot.Colete, Rarity.Epico, 0, 90, 9, 260),
            Make("Colete do Veterano", "Remendado em dezenas de missões. Nunca falhou.", ItemSlot.Colete, Rarity.Lendario, 0, 120, 12, 700),
            Make("Calça Tática", "Bolsos e joelheiras reforçadas.", ItemSlot.Calca, Rarity.Incomum, 0, 16, 2, 36),
            Make("Calça de Kevlar", "Fibra balística nas coxas e canelas.", ItemSlot.Calca, Rarity.Raro, 0, 30, 4, 105),
            Make("Botas Gastas", "Já viram dias melhores, mas ainda servem.", ItemSlot.Botas, Rarity.Comum, 0, 8, 1, 12),
            Make("Botas Silenciosas", "Solado de borracha macia.", ItemSlot.Botas, Rarity.Raro, 0, 28, 3, 100),
            Make("Botas de Assalto", "Leves, firmes e rápidas de calçar.", ItemSlot.Botas, Rarity.Epico, 2f, 40, 4, 240),
            Make("Botas do Corredor", "Quem as calça nunca perde o ritmo.", ItemSlot.Botas, Rarity.Lendario, 4f, 70, 6, 680),
            Make("Mochila Tática", "Compartimentos modulares.", ItemSlot.Mochila, Rarity.Incomum, 0, 14, 1, 40),
            Make("Mochila de Paramédico", "Cheia de ataduras e analgésicos.", ItemSlot.Mochila, Rarity.Epico, 0, 60, 3, 235),
        };

        exclusive = new List<ItemData>
        {
            // Epicos exclusivos
            Make("Fuzil Corrompido", "Recuperado de um laboratório do Pesadelo. Ainda pulsa.", ItemSlot.Arma, Rarity.Epico, 14f, 0, 0, 420, true, AffixType.CritChance, 0.08f),
            Make("Colete Pútrido", "Costurado com algo que não devia estar vivo.", ItemSlot.Colete, Rarity.Epico, 0, 100, 10, 400, true, AffixType.Regen, 0.006f),
            Make("Visor Infectado", "Mostra o que está escondido na escuridão.", ItemSlot.Capacete, Rarity.Epico, 5f, 40, 6, 380, true, AffixType.Range, 0.12f),
            // Lendarios exclusivos
            Make("Ceifador Z-9", "Protótipo militar. Cada tiro procura o ponto fraco.", ItemSlot.Arma, Rarity.Lendario, 22f, 0, 0, 1400, true, AffixType.CritDamage, 0.35f),
            Make("Elmo do Último Sobrevivente", "Pertenceu a quem viu o fim e voltou.", ItemSlot.Capacete, Rarity.Lendario, 3f, 70, 9, 1200, true, AffixType.XpGain, 0.20f),
            Make("Carapaça Necrótica", "Tecido morto que cura quem a veste.", ItemSlot.Colete, Rarity.Lendario, 0, 150, 14, 1500, true, AffixType.Regen, 0.012f),
            Make("Calças de Kevlar Negro", "Fibra preta, impossível de rasgar.", ItemSlot.Calca, Rarity.Lendario, 0, 80, 10, 1100, true, AffixType.Damage, 0.10f),
            Make("Botas Fantasma", "Leves como uma sombra.", ItemSlot.Botas, Rarity.Lendario, 3f, 60, 6, 1100, true, AffixType.FireRate, 0.12f),
            Make("Mochila do Apocalipse", "Tudo o que o mundo perdeu cabe aqui.", ItemSlot.Mochila, Rarity.Lendario, 0, 100, 5, 1300, true, AffixType.GoldFind, 0.30f),
        };
    }

    static void Ensure()
    {
        if (general == null || general.Count == 0 || general[0] == null) Build();
    }

    public static IReadOnlyList<ItemData> Exclusives { get { Ensure(); return exclusive; } }

    /// <summary>Pool final da missao: itens da cena + conteudo extra (+ exclusivos no Pesadelo).</summary>
    public static ItemData[] BuildPool(ItemData[] scenePool, bool includeExclusives)
    {
        Ensure();
        var list = new List<ItemData>();
        if (scenePool != null) foreach (var p in scenePool) if (p != null) list.Add(p);
        list.AddRange(general);
        if (includeExclusives) list.AddRange(exclusive);
        return list.ToArray();
    }

    public static void Clear() { general = null; exclusive = null; }
}
