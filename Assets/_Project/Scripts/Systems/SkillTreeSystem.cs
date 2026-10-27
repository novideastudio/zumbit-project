using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>Estatisticas que uma habilidade ou especializacao pode alterar.</summary>
public enum SkillStat
{
    Damage, FireRate, Health, Range, CritChance, CritDamage, Accuracy, Defense, Xp, Gold, Drop,
    AbilityPower, TurretFireRate, Cooldown, Healing, GrenadeRadius
}

/// <summary>Passive gameplay modifiers aggregated from a survivor's skills and specialization.</summary>
public sealed class SkillTreeBonuses
{
    // Values expressed as fractions: 0.10f means +10%.
    public float damagePercent;
    public float fireRatePercent;
    public float healthPercent;
    public float rangePercent;
    public float critChance;
    public float critDamagePercent;
    public float accuracyBonus;
    public float defenseFlat;
    public float xpPercent;
    public float goldPercent;
    public float dropPercent;
    public float abilityPowerPercent;
    public float turretFireRatePercent;
    public float abilityCooldownReduction;
    public float healingPercent;
    public float grenadeRadiusPercent;

    public void Add(SkillTreeBonuses other)
    {
        if (other == null) return;
        damagePercent += other.damagePercent;
        fireRatePercent += other.fireRatePercent;
        healthPercent += other.healthPercent;
        rangePercent += other.rangePercent;
        critChance += other.critChance;
        critDamagePercent += other.critDamagePercent;
        accuracyBonus += other.accuracyBonus;
        defenseFlat += other.defenseFlat;
        xpPercent += other.xpPercent;
        goldPercent += other.goldPercent;
        dropPercent += other.dropPercent;
        abilityPowerPercent += other.abilityPowerPercent;
        turretFireRatePercent += other.turretFireRatePercent;
        abilityCooldownReduction += other.abilityCooldownReduction;
        healingPercent += other.healingPercent;
        grenadeRadiusPercent += other.grenadeRadiusPercent;
    }

    public float Get(SkillStat s)
    {
        switch (s)
        {
            case SkillStat.Damage: return damagePercent;
            case SkillStat.FireRate: return fireRatePercent;
            case SkillStat.Health: return healthPercent;
            case SkillStat.Range: return rangePercent;
            case SkillStat.CritChance: return critChance;
            case SkillStat.CritDamage: return critDamagePercent;
            case SkillStat.Accuracy: return accuracyBonus;
            case SkillStat.Defense: return defenseFlat;
            case SkillStat.Xp: return xpPercent;
            case SkillStat.Gold: return goldPercent;
            case SkillStat.Drop: return dropPercent;
            case SkillStat.AbilityPower: return abilityPowerPercent;
            case SkillStat.TurretFireRate: return turretFireRatePercent;
            case SkillStat.Cooldown: return abilityCooldownReduction;
            case SkillStat.Healing: return healingPercent;
            default: return grenadeRadiusPercent;
        }
    }

    public void Set(SkillStat s, float v)
    {
        switch (s)
        {
            case SkillStat.Damage: damagePercent = v; break;
            case SkillStat.FireRate: fireRatePercent = v; break;
            case SkillStat.Health: healthPercent = v; break;
            case SkillStat.Range: rangePercent = v; break;
            case SkillStat.CritChance: critChance = v; break;
            case SkillStat.CritDamage: critDamagePercent = v; break;
            case SkillStat.Accuracy: accuracyBonus = v; break;
            case SkillStat.Defense: defenseFlat = v; break;
            case SkillStat.Xp: xpPercent = v; break;
            case SkillStat.Gold: goldPercent = v; break;
            case SkillStat.Drop: dropPercent = v; break;
            case SkillStat.AbilityPower: abilityPowerPercent = v; break;
            case SkillStat.TurretFireRate: turretFireRatePercent = v; break;
            case SkillStat.Cooldown: abilityCooldownReduction = v; break;
            case SkillStat.Healing: healingPercent = v; break;
            default: grenadeRadiusPercent = v; break;
        }
    }

    public SkillTreeBonuses Scaled(float k)
    {
        var r = new SkillTreeBonuses();
        r.Add(this);
        foreach (SkillStat s in System.Enum.GetValues(typeof(SkillStat))) r.Set(s, Get(s) * k);
        return r;
    }
}

public sealed class SkillTreeNode
{
    public string id;
    public string branchId;
    public string branchName;
    public string displayName;
    public string description;
    public string prerequisiteId;
    public int branchIndex;
    /// <summary>Posicao da habilidade no ramo (1..6).</summary>
    public int tier;
    /// <summary>Nivel do personagem para o PRIMEIRO grau (os seguintes pedem +2 niveis cada).</summary>
    public int requiredLevel;
    public int pointCost;
    public SkillStat stat;
    /// <summary>Bonus de UM grau. O bonus total e este valor x grau.</summary>
    public SkillTreeBonuses bonuses;
}

public sealed class SpecializationDef
{
    public string id, survivorId, name, description;
    public SkillTreeBonuses bonuses;
}

/// <summary>
/// Arvores de habilidades: 3 ramos x 6 habilidades por sobrevivente; cada habilidade evolui por 6 graus
/// (Novato ... Lendario) gastando 1 ponto por grau. No nivel 15 o sobrevivente escolhe 1 de 2 especializacoes.
/// Os ids das 3 primeiras habilidades de cada ramo sao os mesmos de antes (saves antigos contam como grau 1).
/// </summary>
public static class SkillTreeSystem
{
    public const int MaxRank = 6;
    public const int SpecializationLevel = 15;
    static readonly string[] RankNames = { "Sem grau", "Novato", "Aprendiz", "Experiente", "Profissional", "Elite", "Lendário" };
    // Nivel do personagem para o grau 1 da habilidade n do ramo (n = 1..6). Cada grau seguinte pede +2 niveis.
    static readonly int[] UnlockLevels = { 1, 6, 12, 20, 28, 36 };

    public static string RankName(int rank) => RankNames[Mathf.Clamp(rank, 0, MaxRank)];
    public static int RequiredLevelFor(SkillTreeNode node, int rank) => node.requiredLevel + (Mathf.Max(1, rank) - 1) * 2;

    // ------------------------------------------------------------------ dados
    sealed class Def
    {
        public string name; public SkillStat stat;
        public Def(string n, SkillStat s) { name = n; stat = s; }
    }
    static Def D(string n, SkillStat s) => new Def(n, s);

    /// <summary>Bonus de um grau de cada estatistica (6 graus = 6x). Ajuste aqui para balancear.</summary>
    static float Step(SkillStat s)
    {
        switch (s)
        {
            case SkillStat.Damage: return 0.025f;
            case SkillStat.FireRate: return 0.025f;
            case SkillStat.Health: return 0.03f;
            case SkillStat.Range: return 0.02f;
            case SkillStat.CritChance: return 0.01f;
            case SkillStat.CritDamage: return 0.05f;
            case SkillStat.Accuracy: return 0.01f;
            case SkillStat.Defense: return 1f;
            case SkillStat.Xp: return 0.02f;
            case SkillStat.Gold: return 0.03f;
            case SkillStat.Drop: return 0.02f;
            case SkillStat.AbilityPower: return 0.04f;
            case SkillStat.TurretFireRate: return 0.04f;
            case SkillStat.Cooldown: return 0.015f;
            case SkillStat.Healing: return 0.04f;
            default: return 0.03f;   // GrenadeRadius
        }
    }

    static readonly Dictionary<string, List<SkillTreeNode>> Trees = BuildTrees();
    static readonly Dictionary<string, List<SpecializationDef>> Specs = BuildSpecs();

    static Dictionary<string, List<SkillTreeNode>> BuildTrees()
    {
        var t = new Dictionary<string, List<SkillTreeNode>>();
        const SkillStat Dmg = SkillStat.Damage, Rate = SkillStat.FireRate, Hp = SkillStat.Health, Rng = SkillStat.Range,
            CC = SkillStat.CritChance, CD = SkillStat.CritDamage, Acc = SkillStat.Accuracy, Def_ = SkillStat.Defense,
            Xp = SkillStat.Xp, Gold = SkillStat.Gold, Drop = SkillStat.Drop, AP = SkillStat.AbilityPower,
            Tur = SkillStat.TurretFireRate, Cd = SkillStat.Cooldown, Heal = SkillStat.Healing, Rad = SkillStat.GrenadeRadius;

        AddTree(t, "assaulter", new[] { "Poder de fogo", "Especialista em granadas", "Resistência" }, new[]
        {
            new[] { D("Dano treinado", Dmg), D("Ritmo de combate", Rate), D("Atirador de assalto", Dmg), D("Alcance tático", Rng), D("Ponto fraco", CC), D("Golpe fatal", CD) },
            new[] { D("Granada reforçada", AP), D("Recarga rápida", Cd), D("Explosão ampliada", Rad), D("Carga explosiva", AP), D("Logística de campo", Cd), D("Onda de impacto", Rad) },
            new[] { D("Condicionamento", Hp), D("Placas reforçadas", Def_), D("Sobrevivente nato", Hp), D("Colete balístico", Def_), D("Fôlego de ferro", Hp), D("Veterano de guerra", Xp) },
        });
        AddTree(t, "sniper", new[] { "Precisão", "Atirador de elite", "Sobrevivência" }, new[]
        {
            new[] { D("Mira firme", Acc), D("Ponto vital", CC), D("Disparo devastador", CD), D("Respiração controlada", Acc), D("Olho de águia", CC), D("Tiro mortal", CD) },
            new[] { D("Calibre melhorado", Dmg), D("Alcance estendido", Rng), D("Eliminação precisa", Dmg), D("Cano longo", Rng), D("Recarga ágil", Rate), D("Disparo de mestre", Dmg) },
            new[] { D("Passo calculado", Hp), D("Colete tático", Def_), D("Aprendizado de campo", Xp), D("Camuflagem", Hp), D("Postura firme", Def_), D("Instinto de sobrevivência", Hp) },
        });
        AddTree(t, "medic", new[] { "Primeiros socorros", "Apoio de campo", "Resiliência" }, new[]
        {
            new[] { D("Mãos treinadas", Heal), D("Tratamento eficiente", Heal), D("Protocolo avançado", Heal), D("Sutura rápida", Heal), D("Kit completo", Hp), D("Medicina de guerra", Heal) },
            new[] { D("Kit de emergência", Heal), D("Pontaria de campo", Dmg), D("Experiência médica", Xp), D("Tiro de cobertura", Rate), D("Estoque de suprimentos", Drop), D("Líder de esquadrão", Dmg) },
            new[] { D("Fôlego extra", Hp), D("Proteção reforçada", Def_), D("Última linha", Hp), D("Adrenalina", Hp), D("Colete médico", Def_), D("Teimosia", Hp) },
        });
        AddTree(t, "engineer", new[] { "Tecnologia", "Arsenal", "Blindagem" }, new[]
        {
            new[] { D("Circuito otimizado", AP), D("Manutenção rápida", Tur), D("Torreta aprimorada", AP), D("Núcleo sobrecarregado", Tur), D("Algoritmo de mira", AP), D("Protocolo sentinela", Tur) },
            new[] { D("Mecanismo estável", Rate), D("Munição calibrada", Dmg), D("Disparo contínuo", Rate), D("Cano reforçado", Dmg), D("Gatilho leve", Rate), D("Mira estabilizada", Acc) },
            new[] { D("Colete de trabalho", Hp), D("Placas de proteção", Def_), D("Blindagem modular", Hp), D("Armadura composta", Def_), D("Reforço estrutural", Hp), D("Fortaleza móvel", Def_) },
        });
        AddTree(t, "explorer", new[] { "Saqueador", "Caçador", "Sobrevivência" }, new[]
        {
            new[] { D("Olho para recursos", Gold), D("Busca metódica", Drop), D("Saque valioso", Gold), D("Faro para tesouros", Drop), D("Contrabandista", Gold), D("Caçador de relíquias", Drop) },
            new[] { D("Caçador experiente", Dmg), D("Pontos vulneráveis", CC), D("Perseguidor", Rng), D("Tiro rápido", Rate), D("Golpe certeiro", CD), D("Predador", Dmg) },
            new[] { D("Conhecimento prático", Xp), D("Improviso defensivo", Hp), D("Resistência de campo", Def_), D("Instinto de fuga", Hp), D("Aprendiz rápido", Xp), D("Calejado", Def_) },
        });
        return t;
    }

    static void AddTree(Dictionary<string, List<SkillTreeNode>> trees, string survivorId, string[] branchNames, Def[][] branches)
    {
        var list = new List<SkillTreeNode>();
        for (int branch = 0; branch < branches.Length; branch++)
        {
            string branchId = BranchId(branch);
            string previousId = null;
            for (int i = 0; i < branches[branch].Length; i++)
            {
                var d = branches[branch][i];
                float step = Step(d.stat);
                var perRank = new SkillTreeBonuses();
                perRank.Set(d.stat, step);
                string nodeId = survivorId + "_" + branchId + "_" + (i + 1);
                list.Add(new SkillTreeNode
                {
                    id = nodeId,
                    branchId = branchId,
                    branchName = branchNames[branch],
                    displayName = d.name,
                    description = LineFor(d.stat, step) + " por grau (máx. " + LineFor(d.stat, step * MaxRank) + ").",
                    prerequisiteId = previousId,
                    branchIndex = branch,
                    tier = i + 1,
                    requiredLevel = UnlockLevels[i],
                    pointCost = 1,
                    stat = d.stat,
                    bonuses = perRank
                });
                previousId = nodeId;
            }
        }
        trees[survivorId] = list;
    }

    static string BranchId(int index) => index == 0 ? "alpha" : index == 1 ? "bravo" : "charlie";

    static SkillTreeBonuses B(params (SkillStat s, float v)[] values)
    {
        var b = new SkillTreeBonuses();
        foreach (var x in values) b.Set(x.s, x.v);
        return b;
    }

    static Dictionary<string, List<SpecializationDef>> BuildSpecs()
    {
        var d = new Dictionary<string, List<SpecializationDef>>();
        void Add(string sid, string id, string name, string desc, SkillTreeBonuses b)
        {
            if (!d.TryGetValue(sid, out var list)) d[sid] = list = new List<SpecializationDef>();
            list.Add(new SpecializationDef { id = sid + "_" + id, survivorId = sid, name = name, description = desc, bonuses = b });
        }
        Add("assaulter", "demolidor", "Demolidor", "Vive de explosões: granadas maiores, mais fortes e mais frequentes, mas dispara um pouco pior.",
            B((SkillStat.AbilityPower, 0.25f), (SkillStat.GrenadeRadius, 0.30f), (SkillStat.Cooldown, 0.20f), (SkillStat.Damage, -0.05f)));
        Add("assaulter", "fuzileiro", "Fuzileiro", "Especialista em tiro: mais dano, cadência e crítico, com granadas mais fracas.",
            B((SkillStat.Damage, 0.15f), (SkillStat.FireRate, 0.12f), (SkillStat.CritChance, 0.03f), (SkillStat.AbilityPower, -0.15f)));
        Add("sniper", "franco", "Franco-atirador", "Alcance e críticos devastadores, ao custo de cadência e vida.",
            B((SkillStat.Range, 0.20f), (SkillStat.CritChance, 0.08f), (SkillStat.CritDamage, 0.40f), (SkillStat.FireRate, -0.10f), (SkillStat.Health, -0.05f)));
        Add("sniper", "rastreador", "Rastreador", "Mais dano e resistência para aguentar a linha de frente, com menos alcance.",
            B((SkillStat.Damage, 0.14f), (SkillStat.FireRate, 0.08f), (SkillStat.Health, 0.12f), (SkillStat.Range, -0.08f)));
        Add("medic", "combate", "Médico de combate", "Regeneração muito maior e mais vida para o grupo, mas fere menos.",
            B((SkillStat.Healing, 0.50f), (SkillStat.Health, 0.10f), (SkillStat.Damage, -0.05f)));
        Add("medic", "resgate", "Atirador de resgate", "Combina cura moderada com mais dano e cadência.",
            B((SkillStat.Damage, 0.12f), (SkillStat.FireRate, 0.10f), (SkillStat.Healing, 0.15f), (SkillStat.Health, -0.05f)));
        Add("engineer", "torretas", "Mestre de torretas", "A torreta vira a arma principal: muito mais poder e cadência, mas o engenheiro atira menos.",
            B((SkillStat.AbilityPower, 0.35f), (SkillStat.TurretFireRate, 0.30f), (SkillStat.Damage, -0.08f)));
        Add("engineer", "apoio", "Soldado de apoio", "Luta junto da torreta: mais dano, cadência e vida, com a torreta mais fraca.",
            B((SkillStat.Damage, 0.14f), (SkillStat.FireRate, 0.10f), (SkillStat.Health, 0.10f), (SkillStat.AbilityPower, -0.10f)));
        Add("explorer", "saqueador", "Saqueador", "Foco em recompensas: mais ouro, itens e experiência, com menos dano.",
            B((SkillStat.Gold, 0.30f), (SkillStat.Drop, 0.25f), (SkillStat.Xp, 0.10f), (SkillStat.Damage, -0.08f)));
        Add("explorer", "cacador", "Caçador de recompensas", "Mais agressivo: dano, crítico e alcance, com ganhos de ouro moderados.",
            B((SkillStat.Damage, 0.15f), (SkillStat.CritChance, 0.04f), (SkillStat.Range, 0.10f), (SkillStat.Gold, 0.10f)));
        return d;
    }

    // ------------------------------------------------------------------ texto
    static string Num(float v) => v.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>"Dano causado +2,5%" / "Vida máxima −5%".</summary>
    public static string LineFor(SkillStat stat, float v)
    {
        string label;
        switch (stat)
        {
            case SkillStat.Damage: label = "Dano causado"; break;
            case SkillStat.FireRate: label = "Cadência de tiro"; break;
            case SkillStat.Health: label = "Vida máxima"; break;
            case SkillStat.Range: label = "Alcance de tiro"; break;
            case SkillStat.CritChance: label = "Chance de crítico"; break;
            case SkillStat.CritDamage: label = "Dano crítico"; break;
            case SkillStat.Accuracy: label = "Precisão"; break;
            case SkillStat.Defense: label = "Defesa"; break;
            case SkillStat.Xp: label = "Experiência recebida"; break;
            case SkillStat.Gold: label = "Ouro recebido"; break;
            case SkillStat.Drop: label = "Chance de recompensa extra"; break;
            case SkillStat.AbilityPower: label = "Poder da habilidade"; break;
            case SkillStat.TurretFireRate: label = "Cadência da torreta"; break;
            case SkillStat.Cooldown: label = "Recarga da habilidade"; v = -v; break;
            case SkillStat.Healing: label = "Regeneração de vida"; break;
            default: label = "Raio da granada"; break;
        }
        string sign = v < 0f ? "−" : "+";
        float a = Mathf.Abs(v);
        if (stat == SkillStat.Defense) return label + " " + sign + Num(a);
        string pct = Num(a * 100f) + "%";
        if (stat == SkillStat.CritChance || stat == SkillStat.Accuracy) pct = Num(a * 100f) + " p.p.";
        return label + " " + sign + pct;
    }

    /// <summary>Todos os bonus nao nulos, um por linha.</summary>
    public static string Describe(SkillTreeBonuses b, string separator = "\n")
    {
        if (b == null) return "";
        var sb = new StringBuilder();
        foreach (SkillStat s in System.Enum.GetValues(typeof(SkillStat)))
        {
            float v = b.Get(s);
            if (Mathf.Abs(v) < 0.0001f) continue;
            if (sb.Length > 0) sb.Append(separator);
            sb.Append(LineFor(s, v));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ consultas
    public static IReadOnlyList<SkillTreeNode> GetNodes(string survivorId)
    {
        if (!string.IsNullOrEmpty(survivorId) && Trees.TryGetValue(survivorId, out var list)) return list;
        return new List<SkillTreeNode>();
    }

    public static SkillTreeNode GetNode(string survivorId, string nodeId)
    {
        var nodes = GetNodes(survivorId);
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].id == nodeId) return nodes[i];
        return null;
    }

    public static IReadOnlyList<SpecializationDef> GetSpecs(string survivorId)
    {
        if (!string.IsNullOrEmpty(survivorId) && Specs.TryGetValue(survivorId, out var list)) return list;
        return new List<SpecializationDef>();
    }

    public static SpecializationDef GetSpec(SurvivorProgress progress)
    {
        if (progress == null || string.IsNullOrEmpty(progress.specialization)) return null;
        foreach (var s in GetSpecs(progress.id))
            if (s.id == progress.specialization) return s;
        return null;
    }

    public static int GetRank(SurvivorProgress progress, string nodeId)
    {
        if (progress == null) return 0;
        if (progress.skillRanks != null)
            for (int i = 0; i < progress.skillRanks.Count; i++)
                if (progress.skillRanks[i] != null && progress.skillRanks[i].id == nodeId)
                    return Mathf.Clamp(progress.skillRanks[i].rank, 0, MaxRank);
        // Save antigo: habilidade desbloqueada sem grau registrado = grau 1.
        return progress.unlockedSkills != null && progress.unlockedSkills.Contains(nodeId) ? 1 : 0;
    }

    static void SetRank(SurvivorProgress progress, string nodeId, int rank)
    {
        if (progress.skillRanks == null) progress.skillRanks = new List<SkillRankEntry>();
        if (progress.unlockedSkills == null) progress.unlockedSkills = new List<string>();
        for (int i = 0; i < progress.skillRanks.Count; i++)
            if (progress.skillRanks[i] != null && progress.skillRanks[i].id == nodeId) { progress.skillRanks[i].rank = rank; goto ranked; }
        progress.skillRanks.Add(new SkillRankEntry { id = nodeId, rank = rank });
    ranked:
        if (rank > 0 && !progress.unlockedSkills.Contains(nodeId)) progress.unlockedSkills.Add(nodeId);
    }

    public static bool IsUnlocked(SurvivorProgress progress, string nodeId) => GetRank(progress, nodeId) >= 1;

    /// <summary>Pode comprar o PROXIMO grau desta habilidade?</summary>
    public static bool CanUnlock(SurvivorProgress progress, SkillTreeNode node, out string reason)
    {
        reason = "";
        if (progress == null || node == null) { reason = "Habilidade indisponível."; return false; }
        int rank = GetRank(progress, node.id);
        if (rank >= MaxRank) { reason = "Grau máximo atingido."; return false; }
        int need = RequiredLevelFor(node, rank + 1);
        if (progress.level < need) { reason = "Requer nível " + need + "."; return false; }
        if (!string.IsNullOrEmpty(node.prerequisiteId) && !IsUnlocked(progress, node.prerequisiteId))
        {
            var previous = GetNode(progress.id, node.prerequisiteId);
            reason = previous != null ? "Requer: " + previous.displayName + "." : "Desbloqueie a habilidade anterior.";
            return false;
        }
        if (progress.skillPoints < node.pointCost) { reason = "Pontos insuficientes."; return false; }
        return true;
    }

    /// <summary>Sobe um grau. Quem chama grava o save.</summary>
    public static bool TryUnlock(SaveData save, string survivorId, string nodeId, out string message)
    {
        message = "";
        if (save == null) { message = "Nenhum save carregado."; return false; }
        var node = GetNode(survivorId, nodeId);
        if (node == null) { message = "Habilidade não encontrada."; return false; }
        var progress = save.GetProgress(survivorId);
        if (!CanUnlock(progress, node, out message)) return false;
        int newRank = GetRank(progress, node.id) + 1;
        progress.skillPoints -= node.pointCost;
        SetRank(progress, node.id, newRank);
        message = node.displayName + ": grau " + RankName(newRank) + ".";
        return true;
    }

    // ------------------------------------------------------------------ especializacao
    public static bool CanChooseSpecialization(SurvivorProgress progress, out string reason)
    {
        reason = "";
        if (progress == null) { reason = "Sobrevivente indisponível."; return false; }
        if (!string.IsNullOrEmpty(progress.specialization)) { reason = "Especialização já escolhida."; return false; }
        if (progress.level < SpecializationLevel) { reason = "Requer nível " + SpecializationLevel + "."; return false; }
        return true;
    }

    public static bool TryChooseSpecialization(SaveData save, string survivorId, string specId, out string message)
    {
        message = "";
        if (save == null) { message = "Nenhum save carregado."; return false; }
        var progress = save.GetProgress(survivorId);
        if (!CanChooseSpecialization(progress, out message)) return false;
        foreach (var s in GetSpecs(survivorId))
            if (s.id == specId)
            {
                progress.specialization = specId;
                message = "Especialização escolhida: " + s.name + ".";
                return true;
            }
        message = "Especialização não encontrada.";
        return false;
    }

    // ------------------------------------------------------------------ redistribuir
    /// <summary>Custo em ouro. A primeira redistribuicao de cada sobrevivente e gratis.</summary>
    public static int ResetCost(SurvivorProgress progress)
    {
        if (progress == null || progress.skillResets <= 0) return 0;
        return Mathf.Min(progress.skillResets, 4) * 12 * Mathf.Max(1, progress.level);
    }

    /// <summary>Devolve todos os pontos, apaga os graus e a especializacao. Quem chama grava o save.</summary>
    public static bool TryReset(SaveData save, string survivorId, out string message)
    {
        message = "";
        if (save == null) { message = "Nenhum save carregado."; return false; }
        var progress = save.GetProgress(survivorId);
        int cost = ResetCost(progress);
        if (save.gold < cost) { message = "Ouro insuficiente: precisa de " + cost + "."; return false; }
        save.gold -= cost;
        progress.skillPoints = Balance.SkillPointsTotal(progress.level);
        if (progress.skillRanks != null) progress.skillRanks.Clear();
        if (progress.unlockedSkills != null) progress.unlockedSkills.Clear();
        progress.specialization = "";
        progress.skillResets++;
        message = cost > 0 ? "Pontos redistribuídos (-" + cost + " ouro)." : "Pontos redistribuídos (grátis desta vez).";
        return true;
    }

    // ------------------------------------------------------------------ bonus finais
    public static SkillTreeBonuses GetBonuses(string survivorId, SurvivorProgress progress)
    {
        var result = new SkillTreeBonuses();
        if (progress == null) return result;
        var nodes = GetNodes(survivorId);
        for (int i = 0; i < nodes.Count; i++)
        {
            int rank = GetRank(progress, nodes[i].id);
            if (rank > 0) result.Add(nodes[i].bonuses.Scaled(rank));
        }
        var spec = GetSpec(progress);
        if (spec != null) result.Add(spec.bonuses);
        return result;
    }
}
