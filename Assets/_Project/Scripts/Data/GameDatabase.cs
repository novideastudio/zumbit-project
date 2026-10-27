using System.Collections.Generic;
using UnityEngine;

public static class GameDatabase
{
    static List<SurvivorDef> survivors;

    public static List<SurvivorDef> Survivors
    {
        get
        {
            if (survivors == null) Build();
            return survivors;
        }
    }

    public static void Reload() => survivors = null;

    static void Build()
    {
        survivors = new List<SurvivorDef>();
        var assets = Resources.LoadAll<SurvivorAsset>("Config/Survivors");
        if (assets.Length > 0)
        {
            System.Array.Sort(assets, (a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.id, b.id));
            foreach (var a in assets) survivors.Add(a.ToDef());
        }
        else survivors.AddRange(DefaultSurvivors());
    }

    public static List<SurvivorDef> DefaultSurvivors()
    {
        return new List<SurvivorDef>
        {
            new SurvivorDef {
                id = "assaulter", name = "Assaulter", role = "Equilibrado",
                description = "Versátil em qualquer situação. Bom dano, boa resistência e capaz de lidar com diferentes tipos de inimigos.",
                utilityName = "Granada", utilityDesc = "A cada 8 segundos lança uma granada que causa dano em área.",
                color = new Color(0.35f, 0.5f, 0.9f), utility = UtilityType.Grenade, weaponId = WeaponCatalog.M4A1
            },
            new SurvivorDef {
                id = "sniper", name = "Sniper", role = "Dano e alcance",
                description = "Especialista em eliminações à longa distância. Com a SCAR-20, cada tiro é devastador, alcança muito longe e atravessa até três zumbis em fila, mas a cadência é baixa.",
                utilityName = "Tiro de precisão", utilityDesc = "25% de chance de acerto crítico (dano x2,5) e mira mais precisa.",
                color = new Color(0.75f, 0.3f, 0.3f), utility = UtilityType.Precision,
                healthMult = 0.8f, weaponId = WeaponCatalog.Scar20,
                accuracyBonus = 0.15f, critChance = 0.25f, critMult = 2.5f
            },
            new SurvivorDef {
                id = "medic", name = "Medic", role = "Sustentação",
                description = "Mantém a equipe viva. Resistente e sempre se curando. A MP5-SD dispara muito rápido, com dano baixo por tiro.",
                utilityName = "Primeiros socorros", utilityDesc = "Regenera 1,5% da vida máxima por segundo (a dele e a do parceiro no modo dupla).",
                color = new Color(0.3f, 0.8f, 0.45f), utility = UtilityType.FieldMedic,
                healthMult = 1.1f, weaponId = WeaponCatalog.Mp5sd
            },
            new SurvivorDef {
                id = "engineer", name = "Engineer", role = "Suporte tático",
                description = "Constrói, repara e fornece recursos. Usa torretas e a M249: pente enorme, pouca precisão.",
                utilityName = "Torreta", utilityDesc = "Instala uma torreta automática que atira junto com ele.",
                color = new Color(0.95f, 0.6f, 0.15f), utility = UtilityType.Turret,
                weaponId = WeaponCatalog.M249
            },
            new SurvivorDef {
                id = "explorer", name = "Explorer", role = "Coleta e recompensa",
                description = "Explora áreas, encontra recursos e itens raros. A MAC-10 é a arma mais rápida, com pente de 40, e ele rende muito mais.",
                utilityName = "Saqueador", utilityDesc = "+50% de ouro, +50% de chance de itens e +20% de XP.",
                color = new Color(0.8f, 0.7f, 0.45f), utility = UtilityType.Scavenger,
                healthMult = 0.9f, weaponId = WeaponCatalog.Mac10, xpMult = 1.2f, goldMult = 1.5f, dropMult = 1.5f
            },
        };
    }

    // goldReward = recompensa de conclusao no NORMAL (x Balance.RewardGoldMult por dificuldade).
    // mapX / mapY = posicao na imagem do mapa (0..1, origem no canto inferior esquerdo)
    public static readonly List<LocationDef> Locations = new List<LocationDef>
    {
        new LocationDef { id = "residencial", name = "Bairro Residencial", theme = "escola", recommendedLevel = 1, difficulty = 1, waves = 3,
            countMult = 0.8f, goldReward = 60, mapX = 0.29f, mapY = 0.79f,
            description = "Ruas silenciosas tomadas por poucos infectados. Um bom lugar para começar." },
        new LocationDef { id = "suburbio", name = "Subúrbio", theme = "escola", hasBoss = true, recommendedLevel = 3, difficulty = 1, waves = 3,
            countMult = 1.0f, goldReward = 120, mapX = 0.15f, mapY = 0.25f, requires = new[] { "residencial" },
            description = "Casas e fazendas abandonadas. Um mutante enorme guarda a escola local." },
        new LocationDef { id = "shopping", name = "Shopping", theme = "centro", recommendedLevel = 5, difficulty = 2, waves = 4,
            countMult = 1.0f, goldReward = 170, mapX = 0.59f, mapY = 0.38f, requires = new[] { "residencial" },
            description = "Estacionamentos e lojas saqueadas. Hordas médias." },
        new LocationDef { id = "industrial", name = "Zona Industrial", theme = "centro", recommendedLevel = 7, difficulty = 2, waves = 4,
            countMult = 1.2f, goldReward = 210, mapX = 0.81f, mapY = 0.32f, requires = new[] { "suburbio" },
            description = "Porto, guindastes e galpões. Muitos zumbis em espaço aberto." },
        new LocationDef { id = "hospital", name = "Hospital", theme = "hospital", hasBoss = true, recommendedLevel = 9, difficulty = 3, waves = 5,
            countMult = 1.2f, goldReward = 250, mapX = 0.82f, mapY = 0.82f, requires = new[] { "shopping" },
            description = "Onde tudo começou. O Paciente Zero espera lá dentro." },
        new LocationDef { id = "delegacia", name = "Delegacia", theme = "delegacia", hasBoss = true, recommendedLevel = 12, difficulty = 3, waves = 5,
            countMult = 1.3f, goldReward = 300, mapX = 0.21f, mapY = 0.61f, requires = new[] { "industrial" },
            description = "Um bunker cercado por infectados fardados. Cuidado com o comandante." },
        new LocationDef { id = "metro", name = "Estação de Metrô", theme = "centro", recommendedLevel = 15, difficulty = 4, waves = 6,
            countMult = 1.4f, goldReward = 350, mapX = 0.65f, mapY = 0.57f, requires = new[] { "hospital", "delegacia" },
            description = "Galpões e trilhos tomados por infectados. Ondas incessantes." },
        new LocationDef { id = "centro", name = "Centro", theme = "centro", hasBoss = true, recommendedLevel = 18, difficulty = 4, waves = 6,
            countMult = 1.6f, goldReward = 400, mapX = 0.47f, mapY = 0.54f, requires = new[] { "metro" },
            description = "O coração da cidade. O Titã da Infecção domina as ruas." },
        new LocationDef { id = "base", name = "Base Militar", theme = "base", hasBoss = true, recommendedLevel = 22, difficulty = 5, waves = 8,
            countMult = 1.8f, goldReward = 450, mapX = 0.91f, mapY = 0.64f, requires = new[] { "centro" },
            description = "A última esperança. O Experimento Alfa guarda o hangar." },
    };

    /// <summary>Local especial do estande de tiro. Fica FORA de Locations (nao aparece no mapa nem entra na progressao).</summary>
    public static readonly LocationDef RangeLocation = new LocationDef
    {
        id = "range", name = "Estande de Tiro", theme = "centro", isRange = true,
        recommendedLevel = 1, difficulty = 1, waves = 0, countMult = 0f, goldReward = 0,
        description = "Teste armas e equipamentos em bonecos de treino. Sem ondas, XP, loot ou penalidade."
    };

    public static SurvivorDef GetSurvivor(string id)
    {
        foreach (var s in Survivors) if (s.id == id) return s;
        return Survivors[0];
    }

    public static LocationDef GetLocation(string id)
    {
        foreach (var l in Locations) if (l.id == id) return l;
        return null;
    }

    // A progressao principal (abrir o proximo local) segue o NORMAL. Facil/Dificil/Pesadelo nao abrem locais.
    // Continua sendo OU entre os requisitos, como o texto "ou" da UI.
    public static bool IsUnlocked(LocationDef loc, SaveData save)
    {
        if (loc.requires == null || loc.requires.Length == 0) return true;
        foreach (var r in loc.requires) if (save.HighestCompleted(r) >= (int)Difficulty.Normal) return true;
        return false;
    }

    /// <summary>Facil: sempre que o local esteja liberado. Demais: precisa vencer o nivel anterior (ou um acima).</summary>
    public static bool IsDifficultyUnlocked(LocationDef loc, SaveData save, int difficulty)
    {
        if (!IsUnlocked(loc, save)) return false;
        if (difficulty <= (int)Difficulty.Facil) return true;
        return save.HighestCompleted(loc.id) >= difficulty - 1;
    }
}
