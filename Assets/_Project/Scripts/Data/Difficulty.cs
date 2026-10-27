using UnityEngine;

/// <summary>
/// Os 4 níveis de dificuldade de cada local. Valores iniciais: AJUSTE COM PLAYTEST.
/// O valor numérico (1..4) é o que vai no save ("hospital#2" = Hospital no Normal).
///
/// Filosofia (modo horda): o jogador NÃO passa de nível só jogando melhor; precisa de PODER
/// (itens + upgrades com sucata). Por isso a vida dos zumbis sobe muito entre os níveis
/// (x1 -> x1,6 -> x2,5, mais elites, cuspidores e furia) e a taxa da horda cresce. Veja PowerRating (Progression.cs).
/// </summary>
public enum Difficulty { Facil = 1, Normal = 2, Dificil = 3, Pesadelo = 4 }

public sealed class DifficultyDef
{
    public Difficulty id;
    public string name;
    public Color color;

    [Header("Zumbis")]
    public float zombieHealth = 1f, zombieDamage = 1f;
    [Tooltip("So usado pelo modo antigo (waves). No modo horda vale hordeRate.")]
    public float countMult = 1f;
    public float speedMult = 1f;
    [Tooltip("Chance de cada zumbi nascer pelo flanco esquerdo (0..1).")]
    public float flankChance;
    public bool eliteMods;      // modificadores de elite (blindado, regenerante, explosivo)
    public bool eliteEnrage;    // elites tambem ficam furiosos aos 50% (chefes sempre ficam)

    [Header("Horda continua")]
    [Tooltip("Multiplica a taxa de nascimento da horda (1 = padrao do GameTuning).")]
    public float hordeRate = 1f;
    [Tooltip("Quantos zumbis de cada lado batem AO MESMO TEMPO (a fila ate la espera).")]
    public int attackersPerSide = 3;
    [Tooltip("Fracao dos nascimentos que sao elites (a partir de ~25 s de missao).")]
    public float eliteShare = 0.07f;
    [Tooltip("Fracao dos comuns que nascem como cuspidores (atacam de longe, ignoram a fila).")]
    public float spitterShare = 0.08f;
    [Tooltip("Segundos ate a FURIA (zumbis +dano/+velocidade a cada 30 s). 0 = sem furia.")]
    public float furyStart;
    [Tooltip("Multiplica a sucata ganha por abate.")]
    public float scrapMult = 1f;

    [Header("Recompensa / risco")]
    public float xpMult = 1f;
    [Tooltip("Chance de perder 1 item equipado ao morrer/abandonar.")]
    public float itemLossChance;
    [Tooltip("Chance de um SEGUNDO item ser perdido (so apos o primeiro).")]
    public float secondLossChance;

    [Header("Pressao")]
    [Tooltip("Cura de level-up multiplicada por este valor (0 = sem cura).")]
    public float levelUpHealMult = 1f;
    [Tooltip("So usado pelo modo antigo (waves): a proxima onda comeca apos N segundos. 0 = espera limpar.")]
    public float waveTimeLimit;

    public int Tier => (int)id - 1; // 0..3, usado na formula de drops
}

public static class Difficulties
{
    public const float DropTierStep = 2.5f;

    // Valores calibrados em simulacao (ver Balance.cs). Dificil/Pesadelo ganham dificuldade por elites, cuspidores,
    // velocidade e furia, e nao so por vida bruta (antes x2,5 / x6).
    public static readonly DifficultyDef[] All =
    {
        new DifficultyDef { id = Difficulty.Facil, name = "Fácil", color = new Color(0.42f, 0.86f, 0.52f),
            zombieHealth = 0.85f, zombieDamage = 0.75f, countMult = 0.8f, speedMult = 0.95f,
            hordeRate = 0.75f, attackersPerSide = 2, eliteShare = 0.04f, spitterShare = 0.03f, furyStart = 0f, scrapMult = 0.7f,
            flankChance = 0f, xpMult = 0.5f, itemLossChance = 0f, secondLossChance = 0f,
            levelUpHealMult = 1f, waveTimeLimit = 0f },

        new DifficultyDef { id = Difficulty.Normal, name = "Normal", color = new Color(1f, 0.84f, 0.29f),
            hordeRate = 1f, attackersPerSide = 3, eliteShare = 0.08f, spitterShare = 0.08f, furyStart = 240f, scrapMult = 1f,
            flankChance = 0f, xpMult = 1f, itemLossChance = 0.10f,
            levelUpHealMult = 1f, waveTimeLimit = 90f },

        new DifficultyDef { id = Difficulty.Dificil, name = "Difícil", color = new Color(1f, 0.52f, 0.16f),
            zombieHealth = 1.6f, zombieDamage = 1.35f, countMult = 1.3f, speedMult = 1.08f,
            hordeRate = 1.25f, attackersPerSide = 4, eliteShare = 0.11f, spitterShare = 0.12f, furyStart = 200f, scrapMult = 1.35f,
            flankChance = 0f, xpMult = 1.15f, itemLossChance = 0.25f, eliteMods = true, eliteEnrage = true,
            levelUpHealMult = 0f, waveTimeLimit = 60f },

        new DifficultyDef { id = Difficulty.Pesadelo, name = "Pesadelo", color = new Color(0.95f, 0.18f, 0.22f),
            zombieHealth = 2.5f, zombieDamage = 1.8f, countMult = 1.6f, speedMult = 1.15f,
            hordeRate = 1.5f, attackersPerSide = 5, eliteShare = 0.14f, spitterShare = 0.16f, furyStart = 150f, scrapMult = 1.8f,
            flankChance = 0f, xpMult = 1.3f, itemLossChance = 0.45f, secondLossChance = 0.15f,
            eliteMods = true, eliteEnrage = true, levelUpHealMult = 0f, waveTimeLimit = 45f },
    };

    public static DifficultyDef Get(int value) => All[Mathf.Clamp(value, 1, 4) - 1];
    public static DifficultyDef Get(Difficulty d) => Get((int)d);

    public static string Key(string locationId, int difficulty) => locationId + "#" + Mathf.Clamp(difficulty, 1, 4);

    /// <summary>Peso usado na LootTable: d = (dificuldade_local - 1) + 2,5 x tier.</summary>
    public static float DropWeightShift(int locationDifficulty, int difficulty)
        => Mathf.Max(0, locationDifficulty - 1) + DropTierStep * Get(difficulty).Tier;

    public static string Describe(DifficultyDef d)
    {
        string loss = d.itemLossChance <= 0f ? "sem risco de perder equipamento"
            : $"{Mathf.RoundToInt(d.itemLossChance * 100)}% de perder 1 item equipado" +
              (d.secondLossChance > 0f ? $" (+{Mathf.RoundToInt(d.secondLossChance * 100)}% de um 2º)" : "");
        string heal = d.levelUpHealMult <= 0f ? "sem cura ao subir de nível" : "cura normal ao subir de nível";
        bool horde = GameTuning.Current.hordeMode;
        string timer = horde
            ? $"horda contínua (ritmo x{d.hordeRate:0.##}, {d.attackersPerSide} batem juntos)"
            : (d.waveTimeLimit > 0f ? $"nova onda a cada {Mathf.RoundToInt(d.waveTimeLimit)}s" : "ondas esperam você limpar");
        string extra = d.eliteMods ? "\nElites com modificadores: blindado, regenerante, explosivo." : "";
        if (horde && d.furyStart > 0f)
            extra += $"\nFúria após {Mathf.FloorToInt(d.furyStart / 60f)}:{Mathf.FloorToInt(d.furyStart % 60f):00}: zumbis ficam mais fortes e rápidos.";
        return
            $"Zumbis: vida x{d.zombieHealth:0.##} · dano x{d.zombieDamage:0.##} · vel x{d.speedMult:0.##}\n" +
            $"Hordas pela direita    XP x{d.xpMult:0.##}    Sucata x{d.scrapMult:0.##}\n" +
            $"Ao morrer: perde nível/XP da missão (a sucata fica); {loss}.\n" +
            $"{char.ToUpper(heal[0])}{heal.Substring(1)}; {timer}." + extra;
    }
}
