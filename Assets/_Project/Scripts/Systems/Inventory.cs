using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public int capacity = 60;
    public PlayerStats stats;

    LootDropper loot;
    readonly List<ItemInstance> items = new List<ItemInstance>();
    public IReadOnlyList<ItemInstance> Items => items;
    public event Action Changed;

    bool dirty;
    float nextSave;

    public int Gold => loot != null ? loot.gold : 0;

    void Awake()
    {
        loot = GetComponent<LootDropper>();
        if (loot == null) loot = FindFirstObjectByType<LootDropper>();
    }

    void Update()
    {
        if (dirty && Time.unscaledTime >= nextSave) Flush();
    }

    void OnDestroy() { if (dirty) Flush(); }
    void OnApplicationQuit() { if (dirty) Flush(); }
    void OnApplicationPause(bool paused) { if (paused && dirty) Flush(); }

    // marca alteracao do jogador e agenda o salvamento automatico
    void Touch()
    {
        Changed?.Invoke();
        dirty = true;
        nextSave = Time.unscaledTime + 1f;
    }

    void Flush()
    {
        dirty = false;
        if (GameSession.DebugRun || GameSession.Save == null || GameSession.Survivor == null || stats == null) return;
        try
        {
            SaveTo(GameSession.Save, GameSession.Survivor.id, stats);
            // Equipamento dos apoios (a mochila e unica): evita item sumir se a partida fechar no meio.
            // Depois do fim da missao o MissionFlow ja gravou tudo (com penalidades), entao nao mexe.
            var flow = FindFirstObjectByType<MissionFlow>();
            if (flow == null || !flow.IsFinished)
                foreach (var m in HeroSquad.Members)
                    if (m != null && !m.isPrimary && m.stats && m.def != null)
                        GameSession.Save.GetProgress(m.def.id).equipped = m.stats.EquippedList();
            SaveSystem.Save(GameSession.Save);
            Debug.Log($"Zombit: inventario salvo (slot {GameSession.Save.slot}): {items.Count} na mochila, {stats.EquippedList().Count} equipados.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("Zombit: falha ao salvar inventario: " + e.Message);
        }
    }

    public bool Contains(ItemInstance item) => items.Contains(item);

    /// <summary>Avisa que um item foi alterado no lugar (ex.: melhoria) para atualizar a UI e agendar o salvamento.</summary>
    public void MarkChanged() => Touch();

    public bool Add(ItemInstance item)
    {
        if (items.Count >= capacity) return false;
        items.Add(item);
        Touch();
        return true;
    }

    public void Equip(ItemInstance item) => Equip(item, stats);

    /// <summary>Equipa em qualquer sobrevivente do esquadrao (a mochila e compartilhada).</summary>
    public void Equip(ItemInstance item, PlayerStats target)
    {
        if (target == null || !items.Remove(item)) return;
        var old = target.Equip(item);
        if (old != null) items.Add(old);
        Touch();
    }

    public void Unequip(ItemSlot slot) => Unequip(slot, stats);

    public void Unequip(ItemSlot slot, PlayerStats target)
    {
        if (target == null || items.Count >= capacity) return;
        var old = target.Unequip(slot);
        if (old != null) items.Add(old);
        Touch();
    }

    public void Sell(ItemInstance item)
    {
        if (!items.Remove(item)) return;
        if (loot != null) loot.gold += item.sellValue;
        Touch();
    }

    // ---- persistencia ----
    public void LoadFrom(SaveData save, string survivorId, PlayerStats ps)
    {
        items.Clear();
        if (save.inventory != null) items.AddRange(save.inventory);
        ps.LoadEquipped(save.GetProgress(survivorId).equipped);
        Changed?.Invoke();
        Debug.Log($"Zombit: inventario carregado: {items.Count} na mochila, {ps.EquippedList().Count} equipados.");
    }

    public void SaveTo(SaveData save, string survivorId, PlayerStats ps)
    {
        save.inventory = new List<ItemInstance>(items);
        save.GetProgress(survivorId).equipped = ps.EquippedList();
    }
}
