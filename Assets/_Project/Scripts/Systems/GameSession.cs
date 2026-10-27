public static class GameSession
{
    public const int MenuSceneIndex = 0;
    public const int GameSceneIndex = 1;

    public static SaveData Save;
    public static SurvivorDef Survivor;
    /// <summary>Segundo sobrevivente da missao (modo dupla) ou null (solo).</summary>
    public static SurvivorDef Partner;
    /// <summary>Terceiro sobrevivente (modo trio, so no Pesadelo) ou null.</summary>
    public static SurvivorDef Third;
    public static LocationDef Location;
    public static bool OpenMapOnLoad;
    public static bool DebugRun;
    /// <summary>True no estande de tiro (sem ondas, recompensas ou penalidades).</summary>
    public static bool IsRange => Location != null && Location.id == "range";

    /// <summary>Nivel escolhido no mapa: 1 Facil, 2 Normal, 3 Dificil, 4 Pesadelo.</summary>
    public static int Difficulty = 2;
    /// <summary>Seguro pago no mapa para a PROXIMA run (consumido pelo MissionSetup).</summary>
    public static bool InsuranceBought;

    public static void EnsureDefaults()
    {
        if (Save == null) { Save = new SaveData(); DebugRun = true; }
        if (Survivor == null) Survivor = GameDatabase.GetSurvivor(Save.selectedSurvivor);
        if (Partner != null && Survivor != null && Partner.id == Survivor.id) Partner = null;
        if (Third != null && (Partner == null || Third.id == Partner.id || (Survivor != null && Third.id == Survivor.id))) Third = null;
        if (Location == null) Location = GameDatabase.Locations[0];
    }

    public static void Reset()
    {
        Save = null; Survivor = null; Partner = null; Third = null; Location = null;
        OpenMapOnLoad = false; DebugRun = false;
        Difficulty = 2; InsuranceBought = false;
    }
}
