using System.Collections.Generic;

/// <summary>Passive gameplay modifiers aggregated from a survivor's unlocked skills.</summary>
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
    public int tier;
    public int requiredLevel;
    public int pointCost;
    public SkillTreeBonuses bonuses;
}

/// <summary>
/// Defines and persists the skill trees for Assaulter, Sniper, Medic, Engineer and Explorer.
/// Skill identifiers are stable because they are generated from survivor/branch/tier IDs.
/// </summary>
public static class SkillTreeSystem
{
    sealed class SkillSpec
    {
        public string name;
        public string description;
        public SkillTreeBonuses bonuses;

        public SkillSpec(string name, string description, SkillTreeBonuses bonuses)
        {
            this.name = name;
            this.description = description;
            this.bonuses = bonuses;
        }
    }

    static readonly Dictionary<string, List<SkillTreeNode>> Trees = BuildTrees();

    static Dictionary<string, List<SkillTreeNode>> BuildTrees()
    {
        var trees = new Dictionary<string, List<SkillTreeNode>>();

        AddTree(trees, "assaulter",
            new string[] { "Poder de fogo", "Especialista em granadas", "Resistência" },
            new SkillSpec[][]
            {
                new SkillSpec[]
                {
                    new SkillSpec("Dano treinado", "Dano causado +8%.", new SkillTreeBonuses { damagePercent = 0.08f }),
                    new SkillSpec("Ritmo de combate", "Cadência de tiro +10%.", new SkillTreeBonuses { fireRatePercent = 0.10f }),
                    new SkillSpec("Atirador de assalto", "Dano causado +15%.", new SkillTreeBonuses { damagePercent = 0.15f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Granada reforçada", "Poder da habilidade +15%.", new SkillTreeBonuses { abilityPowerPercent = 0.15f }),
                    new SkillSpec("Recarga rápida", "Recarga da habilidade reduzida em 10%.", new SkillTreeBonuses { abilityCooldownReduction = 0.10f }),
                    new SkillSpec("Explosão ampliada", "Poder da habilidade +20% e raio da granada +12%.", new SkillTreeBonuses { abilityPowerPercent = 0.20f, grenadeRadiusPercent = 0.12f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Condicionamento", "Vida máxima +8%.", new SkillTreeBonuses { healthPercent = 0.08f }),
                    new SkillSpec("Placas reforçadas", "Defesa +2.", new SkillTreeBonuses { defenseFlat = 2f }),
                    new SkillSpec("Sobrevivente nato", "Vida máxima +12%.", new SkillTreeBonuses { healthPercent = 0.12f })
                }
            });

        AddTree(trees, "sniper",
            new string[] { "Precisão", "Atirador de elite", "Sobrevivência" },
            new SkillSpec[][]
            {
                new SkillSpec[]
                {
                    new SkillSpec("Mira firme", "Precisão adicional +4 pontos percentuais.", new SkillTreeBonuses { accuracyBonus = 0.04f }),
                    new SkillSpec("Ponto vital", "Chance de crítico +5 pontos percentuais.", new SkillTreeBonuses { critChance = 0.05f }),
                    new SkillSpec("Disparo devastador", "Dano crítico +25%.", new SkillTreeBonuses { critDamagePercent = 0.25f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Calibre melhorado", "Dano causado +8%.", new SkillTreeBonuses { damagePercent = 0.08f }),
                    new SkillSpec("Alcance estendido", "Alcance de tiro +10%.", new SkillTreeBonuses { rangePercent = 0.10f }),
                    new SkillSpec("Eliminação precisa", "Dano causado +14%.", new SkillTreeBonuses { damagePercent = 0.14f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Passo calculado", "Vida máxima +8%.", new SkillTreeBonuses { healthPercent = 0.08f }),
                    new SkillSpec("Colete tático", "Defesa +2.", new SkillTreeBonuses { defenseFlat = 2f }),
                    new SkillSpec("Aprendizado de campo", "Experiência recebida +10%.", new SkillTreeBonuses { xpPercent = 0.10f })
                }
            });

        AddTree(trees, "medic",
            new string[] { "Primeiros socorros", "Apoio de campo", "Resiliência" },
            new SkillSpec[][]
            {
                new SkillSpec[]
                {
                    new SkillSpec("Mãos treinadas", "Regeneração de vida +15%.", new SkillTreeBonuses { healingPercent = 0.15f }),
                    new SkillSpec("Tratamento eficiente", "Regeneração de vida +20%.", new SkillTreeBonuses { healingPercent = 0.20f }),
                    new SkillSpec("Protocolo avançado", "Regeneração de vida +25%.", new SkillTreeBonuses { healingPercent = 0.25f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Kit de emergência", "Regeneração de vida +10%.", new SkillTreeBonuses { healingPercent = 0.10f }),
                    new SkillSpec("Resposta rápida", "Regeneração de vida +15%.", new SkillTreeBonuses { healingPercent = 0.15f }),
                    new SkillSpec("Experiência médica", "Experiência recebida +10%.", new SkillTreeBonuses { xpPercent = 0.10f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Fôlego extra", "Vida máxima +8%.", new SkillTreeBonuses { healthPercent = 0.08f }),
                    new SkillSpec("Proteção reforçada", "Defesa +2.", new SkillTreeBonuses { defenseFlat = 2f }),
                    new SkillSpec("Última linha", "Vida máxima +12%.", new SkillTreeBonuses { healthPercent = 0.12f })
                }
            });

        AddTree(trees, "engineer",
            new string[] { "Tecnologia", "Arsenal", "Blindagem" },
            new SkillSpec[][]
            {
                new SkillSpec[]
                {
                    new SkillSpec("Circuito otimizado", "Poder da habilidade +15%.", new SkillTreeBonuses { abilityPowerPercent = 0.15f }),
                    new SkillSpec("Manutenção rápida", "Velocidade de disparo da torreta +12%.", new SkillTreeBonuses { turretFireRatePercent = 0.12f }),
                    new SkillSpec("Torreta aprimorada", "Poder da habilidade +20%.", new SkillTreeBonuses { abilityPowerPercent = 0.20f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Mecanismo estável", "Cadência de tiro +8%.", new SkillTreeBonuses { fireRatePercent = 0.08f }),
                    new SkillSpec("Munição calibrada", "Dano causado +8%.", new SkillTreeBonuses { damagePercent = 0.08f }),
                    new SkillSpec("Disparo contínuo", "Cadência de tiro +12%.", new SkillTreeBonuses { fireRatePercent = 0.12f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Colete de trabalho", "Vida máxima +8%.", new SkillTreeBonuses { healthPercent = 0.08f }),
                    new SkillSpec("Placas de proteção", "Defesa +2.", new SkillTreeBonuses { defenseFlat = 2f }),
                    new SkillSpec("Blindagem modular", "Vida máxima +12%.", new SkillTreeBonuses { healthPercent = 0.12f })
                }
            });

        AddTree(trees, "explorer",
            new string[] { "Saqueador", "Caçador", "Sobrevivência" },
            new SkillSpec[][]
            {
                new SkillSpec[]
                {
                    new SkillSpec("Olho para recursos", "Ouro recebido +10%.", new SkillTreeBonuses { goldPercent = 0.10f }),
                    new SkillSpec("Busca metódica", "Chance de recompensa extra aumentada.", new SkillTreeBonuses { dropPercent = 0.10f }),
                    new SkillSpec("Saque valioso", "Ouro recebido +15%.", new SkillTreeBonuses { goldPercent = 0.15f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Caçador experiente", "Dano causado +6%.", new SkillTreeBonuses { damagePercent = 0.06f }),
                    new SkillSpec("Pontos vulneráveis", "Chance de crítico +4 pontos percentuais.", new SkillTreeBonuses { critChance = 0.04f }),
                    new SkillSpec("Perseguidor", "Alcance de tiro +8%.", new SkillTreeBonuses { rangePercent = 0.08f })
                },
                new SkillSpec[]
                {
                    new SkillSpec("Conhecimento prático", "Experiência recebida +10%.", new SkillTreeBonuses { xpPercent = 0.10f }),
                    new SkillSpec("Improviso defensivo", "Vida máxima +8%.", new SkillTreeBonuses { healthPercent = 0.08f }),
                    new SkillSpec("Resistência de campo", "Defesa +2.", new SkillTreeBonuses { defenseFlat = 2f })
                }
            });

        return trees;
    }

    static void AddTree(Dictionary<string, List<SkillTreeNode>> trees, string survivorId,
        string[] branchNames, SkillSpec[][] branches)
    {
        var list = new List<SkillTreeNode>();
        for (int branch = 0; branch < branches.Length; branch++)
        {
            string branchId = BranchId(branch);
            string previousId = null;
            for (int tierIndex = 0; tierIndex < branches[branch].Length; tierIndex++)
            {
                var spec = branches[branch][tierIndex];
                int tier = tierIndex + 1;
                string nodeId = survivorId + "_" + branchId + "_" + tier;
                list.Add(new SkillTreeNode
                {
                    id = nodeId,
                    branchId = branchId,
                    branchName = branchNames[branch],
                    displayName = spec.name,
                    description = spec.description,
                    prerequisiteId = previousId,
                    branchIndex = branch,
                    tier = tier,
                    requiredLevel = Balance.SkillTierLevel(tier),
                    pointCost = 1,
                    bonuses = spec.bonuses
                });
                previousId = nodeId;
            }
        }
        trees[survivorId] = list;
    }

    static string BranchId(int index)
    {
        if (index == 0) return "alpha";
        if (index == 1) return "bravo";
        return "charlie";
    }

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

    public static bool IsUnlocked(SurvivorProgress progress, string nodeId)
    {
        if (progress == null || progress.unlockedSkills == null) return false;
        return progress.unlockedSkills.Contains(nodeId);
    }

    public static bool CanUnlock(SurvivorProgress progress, SkillTreeNode node, out string reason)
    {
        reason = "";
        if (progress == null || node == null)
        {
            reason = "Habilidade indisponível.";
            return false;
        }
        if (IsUnlocked(progress, node.id))
        {
            reason = "Habilidade já desbloqueada.";
            return false;
        }
        if (progress.level < node.requiredLevel)
        {
            reason = "Requer nível " + node.requiredLevel + ".";
            return false;
        }
        if (!string.IsNullOrEmpty(node.prerequisiteId) && !IsUnlocked(progress, node.prerequisiteId))
        {
            var previous = GetNode(progress.id, node.prerequisiteId);
            reason = previous != null ? "Requer: " + previous.displayName + "." : "Desbloqueie a habilidade anterior.";
            return false;
        }
        if (progress.skillPoints < node.pointCost)
        {
            reason = "Pontos insuficientes.";
            return false;
        }
        return true;
    }

    public static bool TryUnlock(SaveData save, string survivorId, string nodeId, out string message)
    {
        message = "";
        if (save == null)
        {
            message = "Nenhum save carregado.";
            return false;
        }
        var node = GetNode(survivorId, nodeId);
        if (node == null)
        {
            message = "Habilidade não encontrada.";
            return false;
        }
        var progress = save.GetProgress(survivorId);
        if (!CanUnlock(progress, node, out message)) return false;
        if (progress.unlockedSkills == null) progress.unlockedSkills = new List<string>();
        progress.skillPoints -= node.pointCost;
        progress.unlockedSkills.Add(node.id);
        message = "Habilidade desbloqueada: " + node.displayName + ".";
        return true;
    }

    public static SkillTreeBonuses GetBonuses(string survivorId, SurvivorProgress progress)
    {
        var result = new SkillTreeBonuses();
        if (progress == null) return result;
        var nodes = GetNodes(survivorId);
        for (int i = 0; i < nodes.Count; i++)
            if (IsUnlocked(progress, nodes[i].id)) result.Add(nodes[i].bonuses);
        return result;
    }
}
