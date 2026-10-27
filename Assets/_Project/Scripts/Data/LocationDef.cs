public class LocationDef
{
    public string id, name, description;
    public string theme = "";
    public bool hasBoss;
    /// <summary>True so no estande de tiro (sem ondas, recompensas ou penalidades).</summary>
    public bool isRange;
    public int recommendedLevel = 1;
    public int difficulty = 1;
    public int waves = 3;
    public float countMult = 1f;
    public int goldReward = 100;
    public float goldMult = 1f;
    public float mapX = 0.5f, mapY = 0.5f;
    public string[] requires = new string[0];

    // Numeros em Balance.cs.
    public float EnemyHealthMult => 1f + Balance.HealthPerRecLevel * (recommendedLevel - 1);
    public float EnemyDamageMult => 1f + Balance.DamagePerRecLevel * (recommendedLevel - 1);
    // Chefe: maxHealth do EnemyData x BossHealthBase x (1 + 0,10 x (rec - 1)).
    public float BossHealthMult => Balance.BossHealthBase * (1f + Balance.BossHealthPerRecLevel * (recommendedLevel - 1));

    public static string DifficultyName(int d)
    {
        switch (d)
        {
            case 1: return "Fácil";
            case 2: return "Normal";
            case 3: return "Difícil";
            case 4: return "Muito difícil";
            default: return "Extrema";
        }
    }
}
