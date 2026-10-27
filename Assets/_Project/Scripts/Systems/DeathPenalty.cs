using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Penalidade de morte. Vale para: morrer, abandonar a missao e fechar o jogo no meio dela.
///  - XP: nivel, XP e pontos de habilidade voltam ao inicio da missao (nada da run e gravado).
///  - Equipamento: 1 rolagem com a chance do nivel; se perder, tira 1 item EQUIPADO (a mochila fica segura).
///    No Pesadelo ha chance de um segundo. Seguro pago em ouro anula a perda.
///  - O item perdido vira drop garantido na proxima vitoria no mesmo local e nivel.
/// </summary>
public static class DeathPenalty
{
    public const float InsuranceGoldFraction = 0.4f;

    public static int InsuranceCost(LocationDef loc, int difficulty)
    {
        if (Difficulties.Get(difficulty).itemLossChance <= 0f) return 0;
        return Mathf.Max(40, Mathf.RoundToInt(loc.goldReward * InsuranceGoldFraction * (1f + 0.25f * (difficulty - 2))));
    }

    public sealed class Result
    {
        public readonly List<ItemInstance> lost = new List<ItemInstance>();
        public bool insuranceSaved;   // o seguro evitou uma perda que teria acontecido
        public bool insured;
        public float chance;
    }

    /// <summary>Decide quais itens equipados se perdem. NAO remove nada: quem chama remove e chama Register.</summary>
    public static Result Roll(List<ItemInstance> equipped, int difficulty, bool insured)
    {
        var d = Difficulties.Get(difficulty);
        var res = new Result { insured = insured, chance = d.itemLossChance };
        if (equipped == null || equipped.Count == 0 || d.itemLossChance <= 0f) return res;

        bool lose = Random.value < d.itemLossChance;
        if (!lose) return res;
        if (insured) { res.insuranceSaved = true; return res; }

        var pool = new List<ItemInstance>();
        foreach (var it in equipped) if (it != null) pool.Add(it);
        if (pool.Count == 0) return res;

        int i = Random.Range(0, pool.Count);
        res.lost.Add(pool[i]);
        pool.RemoveAt(i);

        if (pool.Count > 0 && d.secondLossChance > 0f && Random.value < d.secondLossChance)
            res.lost.Add(pool[Random.Range(0, pool.Count)]);
        return res;
    }

    /// <summary>Guarda os itens perdidos para devolver na proxima vitoria no mesmo local/nivel.</summary>
    public static void Register(SaveData save, string locationId, int difficulty, List<ItemInstance> lost)
    {
        if (save == null || lost == null) return;
        if (save.lostItems == null) save.lostItems = new List<LostItemRecord>();
        string key = Difficulties.Key(locationId, difficulty);
        foreach (var it in lost) save.lostItems.Add(new LostItemRecord { key = key, item = it });
    }

    /// <summary>Retira (e remove da lista) os itens perdidos que esse local/nivel deve devolver.</summary>
    public static List<ItemInstance> TakeRecoverable(SaveData save, string locationId, int difficulty)
    {
        var list = new List<ItemInstance>();
        if (save == null || save.lostItems == null) return list;
        string key = Difficulties.Key(locationId, difficulty);
        for (int i = save.lostItems.Count - 1; i >= 0; i--)
        {
            var r = save.lostItems[i];
            if (r == null || r.item == null) { save.lostItems.RemoveAt(i); continue; }
            if (r.key != key) continue;
            list.Insert(0, r.item);
            save.lostItems.RemoveAt(i);
        }
        return list;
    }

    public static int PendingFor(SaveData save, string locationId, int difficulty)
    {
        if (save == null || save.lostItems == null) return 0;
        string key = Difficulties.Key(locationId, difficulty);
        int n = 0;
        foreach (var r in save.lostItems) if (r != null && r.item != null && r.key == key) n++;
        return n;
    }

    public static string Describe(Result r, string header)
    {
        var sb = new StringBuilder(header);
        if (r.lost.Count > 0)
        {
            sb.Append("\nEquipamento perdido: ");
            for (int i = 0; i < r.lost.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(r.lost[i].name); }
            sb.Append(".\nEle volta como drop garantido ao vencer este mesmo local e nível.");
        }
        else if (r.insuranceSaved) sb.Append("\nO seguro protegeu seu equipamento desta vez.");
        else if (r.chance > 0f) sb.Append("\nSeu equipamento foi poupado.");
        return sb.ToString();
    }

    /// <summary>Junta o resultado de outro sobrevivente (modo dupla) em "into".</summary>
    public static void Merge(Result into, Result other)
    {
        if (into == null || other == null) return;
        into.lost.AddRange(other.lost);
        into.insuranceSaved |= other.insuranceSaved;
        if (other.chance > into.chance) into.chance = other.chance;
    }

    /// <summary>Fechou o jogo no meio da missao: aplica a penalidade direto no save (nada da run foi gravado).</summary>
    public static bool ResolveInterruptedRun(SaveData save)
    {
        if (save == null || save.activeRun == null || !save.activeRun.active) return false;
        var run = save.activeRun;
        var progress = save.GetProgress(string.IsNullOrEmpty(run.survivorId) ? save.selectedSurvivor : run.survivorId);

        var res = Roll(progress.equipped, run.difficulty, run.insured);
        foreach (var it in res.lost) progress.equipped.Remove(it);
        Register(save, run.locationId, run.difficulty, res.lost);

        // Modo dupla: o apoio tambem conta como derrotado (rolagem propria de equipamento).
        if (!string.IsNullOrEmpty(run.partnerId) && run.partnerId != progress.id)
        {
            var partner = save.GetProgress(run.partnerId);
            var pres = Roll(partner.equipped, run.difficulty, run.insured);
            foreach (var it in pres.lost) partner.equipped.Remove(it);
            Register(save, run.locationId, run.difficulty, pres.lost);
            Merge(res, pres);
        }
        // Modo trio: o terceiro tambem.
        if (!string.IsNullOrEmpty(run.thirdId) && run.thirdId != progress.id && run.thirdId != run.partnerId)
        {
            var third = save.GetProgress(run.thirdId);
            var tres = Roll(third.equipped, run.difficulty, run.insured);
            foreach (var it in tres.lost) third.equipped.Remove(it);
            Register(save, run.locationId, run.difficulty, tres.lost);
            Merge(res, tres);
        }

        var loc = GameDatabase.GetLocation(run.locationId);
        string place = loc != null ? loc.name : run.locationId;
        save.pendingNotice = Describe(res, $"O jogo foi fechado durante a missão em {place} ({Difficulties.Get(run.difficulty).name}): contou como derrota.");
        save.activeRun = new ActiveRun();
        return true;
    }
}
