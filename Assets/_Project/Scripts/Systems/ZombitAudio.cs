using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Audio do Zombit Survival.
///  - Efeitos e musica vem de arquivos em Resources/Audio (tiros por arma, impacto em carne, tiro que erra,
///    morte de zumbi/chefe, explosao, onda de choque, level up, trovao, chuva e a trilha sombria).
///  - Cada categoria tem VARIACOES (nome_1, nome_2, ...): o codigo sorteia sem repetir a anterior.
///  - Para trocar por gravacoes reais basta substituir os arquivos de mesmo nome (WAV ou OGG).
///  - Se um arquivo nao existir, cai num som sintetizado simples (o jogo nunca fica mudo nem quebra).
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class ZombitAudio : MonoBehaviour
{
    static ZombitAudio instance;
    AudioSource sfxSource, musicSource, rainSource;
    readonly List<AudioSource> gunVoices = new List<AudioSource>(12);
    readonly List<AudioSource> generalVoices = new List<AudioSource>(12);
    readonly List<AudioSource> priorityVoices = new List<AudioSource>(4);
    readonly Dictionary<string, AudioClip[]> banks = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
    AudioClip fallbackGun, fallbackImpact, fallbackDeath, fallbackExplosion, fallbackUi, fallbackAmbience;

    const string MusicKey = "zombit_music";
    const string SfxKey = "zombit_sfx";
    const string MusicClipPath = "Audio/music_dark";
    const string RainClipPath = "Audio/rain_loop";

    // O volume mestre ("zombit_vol") e aplicado UMA vez, via AudioListener.volume (menu de opcoes).
    static float sfxVolume = -1f, musicVolume = -1f;
    public static float SfxVolume => sfxVolume >= 0f ? sfxVolume : (sfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.85f));
    public static float MusicVolume => musicVolume >= 0f ? musicVolume : (musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.40f));

    // Mixagem relativa por categoria (os arquivos sao normalizados no mesmo pico).
    const float GunGain = 0.62f, HitGain = 0.55f, CritHitGain = 0.7f, MissGain = 0.32f,
                DeathGain = 0.78f, BossDeathGain = 0.95f, ExplosionGain = 1.0f, ShockGain = 0.9f,
                LevelUpGain = 0.85f, UiGain = 0.6f, ThunderGain = 0.9f, RainGain = 0.55f;

    float rainTarget, rainCurrent;
    float lastMissAt = -10f, lastThunderAt = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("ZombitAudio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ZombitAudio>();
        AudioListener.volume = PlayerPrefs.GetFloat("zombit_vol", 1f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; sfxVolume = -1f; musicVolume = -1f; }

    // ------------------------------------------------------------------ inicializacao
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = NewSource(false);
        musicSource = NewSource(true);
        rainSource = NewSource(true);

        for (int i = 0; i < 12; i++) gunVoices.Add(NewSource(false));
        for (int i = 0; i < 12; i++) generalVoices.Add(NewSource(false));
        for (int i = 0; i < 4; i++) priorityVoices.Add(NewSource(false));

        // Bancos de variacoes (carregados uma vez).
        foreach (var n in new[] { "gun_rifle", "gun_dmr", "gun_lmg", "gun_smg", "gun_sil", "hit_flesh", "hit_crit",
                                  "miss_whiz", "zombie_death", "boss_death", "explosion", "shockwave", "levelup",
                                  "ui_click", "thunder" })
            LoadBank(n);

        // Trilha: a mesma do menu toca durante a missao (a fonte persiste entre cenas).
        var music = Resources.Load<AudioClip>(MusicClipPath);
        if (music == null) { fallbackAmbience = MakeAmbience(); music = fallbackAmbience; }
        musicSource.clip = music;
        musicSource.volume = MusicVolume;
        musicSource.Play();

        var rain = Resources.Load<AudioClip>(RainClipPath);
        if (rain != null) { rainSource.clip = rain; rainSource.volume = 0f; }

        if (!HasAny("gun_rifle")) fallbackGun = MakeGunshotClip();
        if (!HasAny("hit_flesh")) fallbackImpact = MakeImpactClip();
        if (!HasAny("zombie_death")) fallbackDeath = MakeNoiseTone("ZombieDeath", 0.32f, 95f, 0.48f, 0.95f, true);
        if (!HasAny("explosion")) fallbackExplosion = MakeNoiseTone("Explosion", 0.48f, 72f, 0.8f, 0.92f, true);
        if (!HasAny("ui_click")) fallbackUi = MakeNoiseTone("UI", 0.045f, 760f, 0.16f, 0.2f, false);
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.loop = loop;
        return s;
    }

    void LoadBank(string prefix)
    {
        var list = new List<AudioClip>();
        for (int i = 1; i <= 8; i++)
        {
            var c = Resources.Load<AudioClip>("Audio/" + prefix + "_" + i);
            if (c == null) break;
            list.Add(c);
        }
        banks[prefix] = list.ToArray();
    }

    bool HasAny(string prefix) => banks.TryGetValue(prefix, out var b) && b.Length > 0;

    AudioClip Pick(string prefix, AudioClip fallback = null)
    {
        if (!banks.TryGetValue(prefix, out var b) || b.Length == 0) return fallback;
        if (b.Length == 1) return b[0];
        lastPick.TryGetValue(prefix, out int last);
        int idx = Random.Range(0, b.Length);
        if (idx == last) idx = (idx + 1 + Random.Range(0, b.Length - 1)) % b.Length;
        lastPick[prefix] = idx;
        return b[idx];
    }

    void Update()
    {
        if (musicSource != null) musicSource.volume = Mathf.Clamp01(MusicVolume);

        // Chuva: o volume segue a intensidade do clima suavemente.
        if (rainSource != null && rainSource.clip != null)
        {
            rainCurrent = Mathf.MoveTowards(rainCurrent, rainTarget, Time.unscaledDeltaTime * 0.35f);
            float v = rainCurrent * RainGain * Mathf.Clamp01(SfxVolume);
            rainSource.volume = v;
            if (v > 0.002f && !rainSource.isPlaying) rainSource.Play();
            else if (v <= 0.002f && rainSource.isPlaying) rainSource.Pause();
        }
    }

    // ------------------------------------------------------------------ reproducao
    // Cada efeito usa "clip = ...; Play()" numa fonte propria (PlayOneShot nao permite pitch por som).
    readonly Dictionary<AudioSource, float> startedAt = new Dictionary<AudioSource, float>();

    void PlayOn(List<AudioSource> pool, AudioClip clip, float gain, float pitchMin, float pitchMax)
    {
        if (clip == null || pool.Count == 0) return;
        AudioSource src = null;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && !pool[i].isPlaying) { src = pool[i]; break; }
        if (src == null)
        {
            // Todas ocupadas: rouba a mais antiga (em vez de cortar um som aleatorio).
            float oldest = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null) continue;
                startedAt.TryGetValue(pool[i], out float st);
                if (st < oldest) { oldest = st; src = pool[i]; }
            }
            if (src == null) return;
        }
        src.Stop();
        src.clip = clip;
        src.loop = false;
        src.pitch = Random.Range(pitchMin, pitchMax);
        src.volume = Mathf.Clamp01(SfxVolume * gain);
        src.Play();
        startedAt[src] = Time.unscaledTime;
    }

    static ZombitAudio GetOrCreateInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<ZombitAudio>();
        if (instance != null) return instance;
        Bootstrap();
        return instance;
    }

    // ------------------------------------------------------------------ API publica
    /// <summary>Familia de som da arma: SCAR = fuzil pesado, MP5-SD = silenciada, MAC-10 = SMG, M249 = metralhadora.</summary>
    static string GunBank(WeaponData w)
    {
        string n = w != null && w.displayName != null ? w.displayName.ToUpperInvariant() : "";
        if (n.Contains("SCAR") || n.Contains("AWP") || n.Contains("SNIPER")) return "gun_dmr";
        if (n.Contains("MP5") || n.Contains("SD")) return "gun_sil";
        if (n.Contains("MAC") || n.Contains("UZI") || n.Contains("SMG")) return "gun_smg";
        if (n.Contains("M249") || n.Contains("LMG")) return "gun_lmg";
        return "gun_rifle";
    }

    public static void PlayGunshot(Vector3 pos, WeaponData weapon)
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.gunVoices, a.Pick(GunBank(weapon), a.fallbackGun), GunGain, 0.96f, 1.04f);
    }

    /// <summary>Compatibilidade com o chamador antigo (sem a arma).</summary>
    public static void PlayGunshot(Vector3 pos, float rate = 1f, float pitchScale = 1f)
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.gunVoices, a.Pick("gun_rifle", a.fallbackGun), GunGain,
            Mathf.Clamp(0.96f * pitchScale, 0.6f, 1.6f), Mathf.Clamp(1.04f * pitchScale, 0.6f, 1.6f));
    }

    public static void PlayImpact(Vector3 pos, bool critical = false)
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.generalVoices, critical ? a.Pick("hit_crit", a.fallbackImpact) : a.Pick("hit_flesh", a.fallbackImpact),
            critical ? CritHitGain : HitGain, 0.92f, 1.08f);
    }

    /// <summary>Tiro que passou direto (assobio + ricochete). Limitado para nao poluir rajadas rapidas.</summary>
    public static void PlayMiss(Vector3 pos)
    {
        var a = GetOrCreateInstance();
        if (a == null || Time.unscaledTime - a.lastMissAt < 0.12f) return;
        a.lastMissAt = Time.unscaledTime;
        a.PlayOn(a.generalVoices, a.Pick("miss_whiz"), MissGain, 0.9f, 1.15f);
    }

    public static void PlayZombieDeath(Vector3 pos, bool boss = false)
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        if (boss) a.PlayOn(a.priorityVoices, a.Pick("boss_death", a.fallbackDeath), BossDeathGain, 0.95f, 1.03f);
        else a.PlayOn(a.generalVoices, a.Pick("zombie_death", a.fallbackDeath), DeathGain, 0.9f, 1.12f);
    }

    public static void PlayExplosion(Vector3 pos)
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.priorityVoices, a.Pick("explosion", a.fallbackExplosion), ExplosionGain, 0.96f, 1.04f);
    }

    public static void PlayShockwave(Vector3 pos)
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.priorityVoices, a.Pick("shockwave", a.fallbackExplosion), ShockGain, 0.96f, 1.04f);
    }

    public static void PlayLevelUp()
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.priorityVoices, a.Pick("levelup"), LevelUpGain, 1f, 1f);
    }

    public static void PlayThunder()
    {
        var a = GetOrCreateInstance();
        if (a == null || Time.unscaledTime - a.lastThunderAt < 4f) return;
        a.lastThunderAt = Time.unscaledTime;
        a.PlayOn(a.priorityVoices, a.Pick("thunder"), ThunderGain, 0.92f, 1.06f);
    }

    public static void PlayUI()
    {
        var a = GetOrCreateInstance();
        if (a == null) return;
        a.PlayOn(a.generalVoices, a.Pick("ui_click", a.fallbackUi), UiGain, 0.98f, 1.03f);
    }

    /// <summary>Intensidade da chuva (0 = nenhuma, 1 = forte). Quem controla o clima chama a cada quadro.</summary>
    public static void SetRain(float intensity01)
    {
        var a = GetOrCreateInstance();
        if (a != null) a.rainTarget = Mathf.Clamp01(intensity01);
    }

    public static void SetMusicVolume(float value) { musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, musicVolume); PlayerPrefs.Save(); }
    public static void SetSfxVolume(float value) { sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, sfxVolume); PlayerPrefs.Save(); }

    void OnDestroy() { if (instance == this) instance = null; }

    // ------------------------------------------------------------------ fallback procedural (so se faltar o arquivo)
    static AudioClip MakeGunshotClip()
    {
        const int rate = 22050;
        const float seconds = 0.14f;
        int count = Mathf.RoundToInt(rate * seconds);
        var data = new float[count];
        float phase = 0f, previousNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Exp(-t * 31f);
            phase += 2f * Mathf.PI * Mathf.Lerp(112f, 52f, t / seconds) / rate;
            float noise = Random.Range(-1f, 1f);
            float crack = noise - previousNoise * 0.78f;
            previousNoise = noise;
            float transient = t < 0.018f ? Mathf.Exp(-t * 175f) : 0f;
            data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.34f + crack * (0.5f + transient * 0.65f)) * envelope * 0.72f, -1f, 1f);
        }
        var clip = AudioClip.Create("Zombit_Gunshot_Fallback", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeImpactClip()
    {
        const int rate = 22050;
        const float seconds = 0.085f;
        int count = Mathf.RoundToInt(rate * seconds);
        var data = new float[count];
        float phase = 0f, previousNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            phase += 2f * Mathf.PI * Mathf.Lerp(880f, 310f, t / seconds) / rate;
            float noise = Random.Range(-1f, 1f);
            float tick = noise - previousNoise * 0.62f;
            previousNoise = noise;
            data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.43f + tick * 0.48f) * Mathf.Exp(-t * 58f) * 0.42f, -1f, 1f);
        }
        var clip = AudioClip.Create("Zombit_Hit_Fallback", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeNoiseTone(string name, float seconds, float frequency, float amplitude, float noiseMix, bool lowpass)
    {
        const int rate = 22050;
        int count = Mathf.Max(256, Mathf.RoundToInt(rate * seconds));
        float[] data = new float[count];
        float phase = Random.value * 6.28318f, lastNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float env = Mathf.Pow(1f - t, name == "Explosion" ? 1.8f : 3.5f);
            float noise = Random.Range(-1f, 1f);
            if (lowpass) { lastNoise = Mathf.Lerp(lastNoise, noise, 0.22f); noise = lastNoise; }
            float tone = Mathf.Sin(phase + 6.28318f * frequency * i / rate);
            data[i] = Mathf.Clamp((tone * (1f - noiseMix) + noise * noiseMix) * amplitude * env, -1f, 1f);
        }
        var clip = AudioClip.Create("Zombit_" + name + "_Fallback", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeAmbience()
    {
        const int rate = 22050;
        const int seconds = 12;
        int count = rate * seconds;
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float fade = Mathf.Min(1f, Mathf.Min(t / 0.5f, (seconds - t) / 0.5f));
            float pulse = 0.72f + 0.28f * Mathf.Sin(6.28318f * t / 7.5f);
            float a = Mathf.Sin(6.28318f * 55f * t) * 0.55f;
            float b = Mathf.Sin(6.28318f * 82.41f * t + 0.35f) * 0.28f;
            float c = Mathf.Sin(6.28318f * 110f * t + 1.1f) * 0.12f;
            data[i] = (a + b + c) * pulse * fade * 0.12f;
        }
        var clip = AudioClip.Create("Zombit_Ambient_Fallback", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
