using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    public const int SlotCount = 3;

    // Com conta online, cada conta tem seus proprios arquivos locais (varias contas no mesmo navegador/PC nao se misturam).
    // Sem conta (cena de teste aberta direto), cai no nome antigo.
    static string FilePrefix => string.IsNullOrEmpty(ZombitCloud.Uid) ? "zombit_slot" : "zombit_" + ZombitCloud.Uid + "_slot";
    static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, FilePrefix + slot + ".json");
    static string LegacyPathFor(int slot) => Path.Combine(Application.persistentDataPath, $"zombit_slot{slot}.json");
    static string BackupPathFor(int slot) => PathFor(slot) + ".bak";
    static string TempPathFor(int slot) => PathFor(slot) + ".tmp";

    public static bool Exists(int slot) => File.Exists(PathFor(slot)) || File.Exists(BackupPathFor(slot));

    /// <summary>
    /// Gravação segura: escreve num arquivo temporário e só então substitui o save, mantendo a
    /// versão anterior em .bak. Se o jogo fechar/travar no meio da escrita, o save antigo continua intacto.
    /// </summary>
    public static void Save(SaveData d)
    {
        // UTC: os "ticks" sao comparados entre aparelhos para decidir qual save (local ou nuvem) e o mais novo.
        d.ticks = DateTime.UtcNow.Ticks;
        d.lastSaved = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        string json = JsonUtility.ToJson(d, true);
        WriteAtomic(d.slot, json);
        ZombitCloud.QueueSave(d.slot, json, d.ticks);   // enviado em segundo plano (com atraso) se houver conta
    }

    static void WriteAtomic(int slot, string json)
    {
        string path = PathFor(slot);
        string tmp = TempPathFor(slot);
        string bak = BackupPathFor(slot);

        File.WriteAllText(tmp, json);
        if (!File.Exists(path)) { File.Move(tmp, path); return; }
        try { File.Replace(tmp, path, bak); }
        catch (Exception)
        {
            // File.Replace nao existe em todas as plataformas (ex.: WebGL). Alternativa equivalente.
            try { File.Copy(path, bak, true); } catch (Exception) { }
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
    }

    // ---- usado pela sincronizacao com a nuvem (ZombitCloud) ----
    public static string ReadRawJson(int slot)
    {
        try { string p = PathFor(slot); return File.Exists(p) ? File.ReadAllText(p) : null; }
        catch (Exception) { return null; }
    }

    [Serializable] class TicksProbe { public long ticks; }
    public static long TicksOf(string json)
    {
        try { return JsonUtility.FromJson<TicksProbe>(json).ticks; }
        catch (Exception) { return 0L; }
    }

    /// <summary>Grava no disco um save vindo da nuvem (nao reenvia para a nuvem).</summary>
    public static void ImportFromCloud(int slot, string json) => WriteAtomic(slot, json);

    public static void DeleteLocalOnly(int slot)
    {
        foreach (var p in new[] { PathFor(slot), BackupPathFor(slot), TempPathFor(slot) })
            if (File.Exists(p)) File.Delete(p);
    }

    /// <summary>
    /// Saves criados antes de existirem contas ficam em "zombit_slotN.json". A PRIMEIRA conta que entrar neste
    /// aparelho herda esses arquivos (copia); depois disso eles nao sao mais oferecidos a nenhuma outra conta.
    /// </summary>
    public static void AdoptLegacyFiles()
    {
        if (string.IsNullOrEmpty(ZombitCloud.Uid) || PlayerPrefs.GetInt("zombit_legacy_adopted", 0) == 1) return;
        try
        {
            for (int i = 1; i <= SlotCount; i++)
            {
                string legacy = LegacyPathFor(i);
                if (!File.Exists(legacy) || File.Exists(PathFor(i))) continue;
                File.Copy(legacy, PathFor(i), false);
            }
        }
        catch (Exception e) { Debug.LogWarning("Zombit: falha ao migrar saves antigos para a conta: " + e.Message); }
        PlayerPrefs.SetInt("zombit_legacy_adopted", 1);
        PlayerPrefs.Save();
    }

    static SaveData TryRead(string path, int slot)
    {
        if (!File.Exists(path)) return null;
        var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        if (d == null) return null;
        d.slot = slot;
        d.MigrateFromPreviousVersions();

        // Jogo fechado no meio de uma missao: aplica a penalidade de morte e grava na hora (idempotente).
        if (DeathPenalty.ResolveInterruptedRun(d))
        {
            try { Save(d); }
            catch (Exception e) { Debug.LogWarning("Zombit: falha ao gravar a penalidade de missao interrompida: " + e.Message); }
        }
        return d;
    }

    public static SaveData Load(int slot)
    {
        try
        {
            var d = TryRead(PathFor(slot), slot);
            if (d != null) return d;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Falha ao carregar slot " + slot + ": " + e.Message + ". Tentando o backup.");
        }

        try
        {
            var d = TryRead(BackupPathFor(slot), slot);
            if (d != null) Debug.LogWarning("Slot " + slot + " restaurado a partir do backup (.bak).");
            return d;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Falha ao carregar o backup do slot " + slot + ": " + e.Message);
            return null;
        }
    }

    public static void Delete(int slot)
    {
        DeleteLocalOnly(slot);
        ZombitCloud.QueueDelete(slot);
    }

    public static int FirstEmptySlot()
    {
        for (int i = 1; i <= SlotCount; i++) if (!Exists(i)) return i;
        return -1;
    }

    public static SaveData LoadMostRecent()
    {
        SaveData best = null;
        for (int i = 1; i <= SlotCount; i++)
        {
            var d = Load(i);
            if (d != null && (best == null || d.ticks > best.ticks)) best = d;
        }
        return best;
    }
}
