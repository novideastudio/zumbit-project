using System;
using System.Collections.Generic;

[Serializable]
public class SurvivorProgress
{
    public string id;
    public int level = 1;
    public int xp;
    // Adicionado na versão 3; saves antigos começam com zero pontos.
    public int skillPoints;
    public List<string> unlockedSkills = new List<string>();
    public List<ItemInstance> equipped = new List<ItemInstance>();
}

/// <summary>Missao em andamento. Gravada ao iniciar e limpa ao terminar: se o jogo abrir com ela ainda ativa, o jogador fechou o jogo no meio (conta como morte).</summary>
[Serializable]
public class ActiveRun
{
    public bool active;
    public string locationId = "";
    public string survivorId = "";
    /// <summary>Segundo sobrevivente (modo dupla). Vazio = missao solo.</summary>
    public string partnerId = "";
    /// <summary>Terceiro sobrevivente (modo trio, so no Pesadelo). Vazio = sem trio.</summary>
    public string thirdId = "";
    public int difficulty = 2;
    public bool insured;
}

/// <summary>Item perdido por morte: volta como drop garantido na proxima vitoria no mesmo local e nivel (chave "hospital#3").</summary>
[Serializable]
public class LostItemRecord
{
    public string key;
    public ItemInstance item;
}

[Serializable]
public class SaveData
{
    public int version = 6;
    public int slot = 1;
    public string selectedSurvivor = "assaulter";
    /// <summary>Segundo slot do esquadrao (modo dupla). Vazio = jogar solo. Campo novo: saves antigos ficam solo.</summary>
    public string selectedPartner = "";
    /// <summary>Terceiro slot (modo trio). So entra em campo no Pesadelo; nos outros niveis fica guardado.</summary>
    public string selectedThird = "";
    // Nickname shown over the survivor during missions; added in version 4.
    public string playerNickname = "";
    public int gold;
    public long ticks;
    public string lastSaved = "";
    public bool starterGiven;
    public List<SurvivorProgress> progress = new List<SurvivorProgress>();
    public List<string> completed = new List<string>();
    public List<ItemInstance> inventory = new List<ItemInstance>();

    // ---- versao 5 ----
    // completed agora guarda "localId#nivel" (ex.: "hospital#2" = Hospital no Normal). Saves antigos contam como Normal.
    public ActiveRun activeRun = new ActiveRun();
    public List<LostItemRecord> lostItems = new List<LostItemRecord>();
    // Mensagem exibida no mapa (ex.: penalidade por ter fechado o jogo no meio de uma missao).
    public string pendingNotice = "";

    // ---- modo horda ----
    // Sucata: dropa dos zumbis, NUNCA se perde ao morrer. Gasta na oficina (inventario) para melhorar itens.
    // Campo aditivo: saves antigos comecam com 0.
    public int scrap;

    // Migração aditiva para saves anteriores à árvore de habilidades (versão 2 ou inferior).
    public void MigrateFromPreviousVersions()
    {
        if (progress == null) progress = new List<SurvivorProgress>();
        if (completed == null) completed = new List<string>();
        if (inventory == null) inventory = new List<ItemInstance>();

        bool legacySkills = version < 3;
        foreach (var p in progress)
        {
            if (p == null) continue;
            if (p.equipped == null) p.equipped = new List<ItemInstance>();
            if (p.unlockedSkills == null) p.unlockedSkills = new List<string>();
            if (legacySkills)
                p.skillPoints = Math.Max(p.skillPoints, Math.Max(0, p.level - 1));
        }
        if (version < 3) version = 3;

        // Existing saves did not contain a nickname. Default to the selected survivor name.
        if (string.IsNullOrWhiteSpace(playerNickname))
        {
            var survivor = GameDatabase.GetSurvivor(selectedSurvivor);
            playerNickname = survivor != null ? survivor.name : "Sobrevivente";
        }
        playerNickname = playerNickname.Trim();
        if (version < 4) version = 4;

        // v5: conclusao por nivel. Entradas antigas (sem '#') viram Normal.
        if (version < 5 || HasLegacyCompletedEntries())
        {
            var fixedList = new List<string>();
            foreach (var c in completed)
            {
                if (string.IsNullOrEmpty(c)) continue;
                string key = c.IndexOf('#') >= 0 ? c : c + "#" + (int)Difficulty.Normal;
                if (!fixedList.Contains(key)) fixedList.Add(key);
            }
            completed = fixedList;
        }
        if (activeRun == null) activeRun = new ActiveRun();
        if (lostItems == null) lostItems = new List<LostItemRecord>();
        if (pendingNotice == null) pendingNotice = "";
        if (version < 5) version = 5;

        // v6: rebalanceamento de progressao (nivel maximo 30, 1 ponto de habilidade a cada 3 niveis).
        // Nivel acima do teto vira o teto; pontos livres = (pontos que o nivel da) - (nos ja desbloqueados), nunca negativo.
        if (version < 6)
        {
            foreach (var p in progress)
            {
                if (p == null) continue;
                if (p.level > Balance.MaxLevel) { p.level = Balance.MaxLevel; p.xp = 0; }
                int spent = p.unlockedSkills != null ? p.unlockedSkills.Count : 0;
                p.skillPoints = Math.Max(0, Balance.SkillPointsTotal(p.level) - spent);
            }
            version = 6;
        }
        NormalizeSquad();
    }

    // ---- esquadrao (modo dupla) ----
    public bool HasPartner => !string.IsNullOrEmpty(selectedPartner);
    public bool HasThird => HasPartner && !string.IsNullOrEmpty(selectedThird);

    /// <summary>
    /// Apoio invalido ou repetido vira vazio. Se o 2o slot ficar vazio com o 3o ocupado,
    /// o 3o sobe para o 2o (o esquadrao nunca tem buraco).
    /// </summary>
    public void NormalizeSquad()
    {
        if (selectedPartner == null) selectedPartner = "";
        if (selectedThird == null) selectedThird = "";
        if (selectedPartner.Length > 0 && (selectedPartner == selectedSurvivor || GameDatabase.GetSurvivor(selectedPartner) == null))
            selectedPartner = "";
        if (selectedThird.Length > 0 && (selectedThird == selectedSurvivor || selectedThird == selectedPartner || GameDatabase.GetSurvivor(selectedThird) == null))
            selectedThird = "";
        if (selectedPartner.Length == 0 && selectedThird.Length > 0) { selectedPartner = selectedThird; selectedThird = ""; }
    }

    /// <summary>Troca o principal. Se ele estava num slot de apoio, troca de lugar com o principal antigo.</summary>
    public void SetPrimarySurvivor(string id)
    {
        if (string.IsNullOrEmpty(id) || id == selectedSurvivor) return;
        if (selectedPartner == id) selectedPartner = selectedSurvivor;
        else if (selectedThird == id) selectedThird = selectedSurvivor;
        selectedSurvivor = id;
        NormalizeSquad();
    }

    /// <summary>Define o apoio ("" = solo). Se ele estava no 3o slot, os dois apoios trocam de lugar.</summary>
    public void SetPartner(string id)
    {
        id = id ?? "";
        if (id.Length > 0 && id == selectedThird) selectedThird = selectedPartner;
        selectedPartner = id;
        NormalizeSquad();
    }

    /// <summary>Define o terceiro ("" = sem trio). Precisa de um apoio no 2o slot.</summary>
    public void SetThird(string id)
    {
        selectedThird = id ?? "";
        NormalizeSquad();
    }

    bool HasLegacyCompletedEntries()
    {
        foreach (var c in completed) if (!string.IsNullOrEmpty(c) && c.IndexOf('#') < 0) return true;
        return false;
    }

    // ---- conclusao por nivel ----
    public bool IsCompleted(string locationId, int difficulty) =>
        completed != null && completed.Contains(Difficulties.Key(locationId, difficulty));

    /// <summary>Maior nivel concluido naquele local (0 = nenhum).</summary>
    public int HighestCompleted(string locationId)
    {
        int best = 0;
        if (completed == null) return 0;
        string prefix = locationId + "#";
        foreach (var c in completed)
        {
            if (string.IsNullOrEmpty(c) || !c.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (int.TryParse(c.Substring(prefix.Length), out int d) && d > best) best = d;
        }
        return best;
    }

    /// <summary>Quantos niveis distintos foram concluidos naquele local (0..4).</summary>
    public int CompletedLevelCount(string locationId)
    {
        int n = 0;
        for (int d = 1; d <= 4; d++) if (IsCompleted(locationId, d)) n++;
        return n;
    }

    /// <summary>Marca o nivel como concluido. Retorna true se era a primeira vez.</summary>
    public bool MarkCompleted(string locationId, int difficulty)
    {
        if (completed == null) completed = new List<string>();
        string key = Difficulties.Key(locationId, difficulty);
        if (completed.Contains(key)) return false;
        completed.Add(key);
        return true;
    }

    /// <summary>Locais com o Normal (progressao principal) concluido.</summary>
    public int CompletedLocationCount()
    {
        int n = 0;
        foreach (var l in GameDatabase.Locations)
            if (HighestCompleted(l.id) >= (int)Difficulty.Normal) n++;
        return n;
    }

    public SurvivorProgress GetProgress(string id)
    {
        if (progress == null) progress = new List<SurvivorProgress>();
        foreach (var p in progress)
        {
            if (p == null || p.id != id) continue;
            if (p.equipped == null) p.equipped = new List<ItemInstance>();
            if (p.unlockedSkills == null) p.unlockedSkills = new List<string>();
            return p;
        }
        var n = new SurvivorProgress { id = id };
        progress.Add(n);
        return n;
    }
}
