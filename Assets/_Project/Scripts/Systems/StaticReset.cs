using UnityEngine;

public static class StaticReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Enemy.Alive.Clear();
        Enemy.HealthMult = 1f;
        Enemy.DamageMult = 1f;
        Enemy.DamageDealt = 0f;
        Enemy.BossHealthMult = 1f;
        Enemy.SpeedMult = 1f;
        Enemy.EliteEnrage = false;
        Enemy.MaxAttackersPerSide = 3;
        Enemy.FuryDamage = 1f;
        Enemy.FurySpeed = 1f;
        GameEvents.WaveTimeLeft = -1f;
        GameEvents.HordeMode = false;
        GameEvents.HordeTimeLeft = -1f;
        GameEvents.HordeFury = false;
        ItemCatalog.Clear();
        HeroSquad.Clear();

        GameEvents.EnemyKilled = null;
        GameEvents.WaveChanged = null;
        GameEvents.LevelUp = null;
        GameEvents.MissionComplete = null;
        GameEvents.MissionFailed = null;

        GameSession.Reset();
        GameDatabase.Reload();
        UITheme.Reload();
        GameTuning.Reload();
        ArtLoader.Clear();
        UIFactory.ResetCache();
        Time.timeScale = 1f;
    }
}
