using System;
public static class GameEvents
{
    public static Action<Enemy> EnemyKilled;
    public static Action<int, int> WaveChanged;
    public static Action<int> LevelUp;
    public static Action MissionComplete, MissionFailed;

    /// <summary>Segundos ate a proxima onda comecar sozinha (pressao de tempo). Negativo = sem contagem.</summary>
    public static float WaveTimeLeft = -1f;

    // ---- Modo horda (HordeSpawner) ----
    public static bool HordeMode;
    /// <summary>Segundos ate o fim da horda (depois so falta eliminar todos / o chefe). Negativo = sem contagem.</summary>
    public static float HordeTimeLeft = -1f;
    /// <summary>A Furia comecou: zumbis ficam mais fortes e rapidos com o tempo.</summary>
    public static bool HordeFury;
}
