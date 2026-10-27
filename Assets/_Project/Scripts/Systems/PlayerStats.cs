using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(Hero))]
public class PlayerStats : MonoBehaviour
{
    [Header("Base (nivel 1)")]
    public float baseHealth = 100f;
    public float baseDamage = 0f;
    public float baseDefense = 0f;

    [Header("Por nivel")]
    public float healthPerLevel = 20f;
    public float damagePerLevel = 2f;
    public float defensePerLevel = 1f;

    [Header("Level-up")]
    [Range(0f, 1f), Tooltip("Fracao da vida maxima curada ao subir de nivel. Antes era cura TOTAL (1.0): com o heroi parado " +
        "segurando hordas isso virava cura infinita. 0 = nao cura; 1 = cura total (comportamento antigo).")]
    public float levelUpHealFraction = 0.25f;

    [Header("Multiplicadores (sobrevivente)")]
    public float healthMult = 1f;
    public float xpMult = 1f;

    [Header("Dificuldade (definido pelo MissionSetup)")]
    [Tooltip("XP do nivel de dificuldade (Facil 0.6 ... Pesadelo 2).")] public float difficultyXpMult = 1f;
    [Tooltip("Multiplica levelUpHealFraction. 0 no Dificil e no Pesadelo (sem cura ao subir de nivel).")] public float levelUpHealMultiplier = 1f;

    [Header("Esquadrao (definido pelo MissionSetup)")]
    [Tooltip("Fracao da XP de cada abate que este sobrevivente recebe. 0,5 na dupla (50/50), 0,4 no trio.")]
    public float xpShare = 1f;
    [Tooltip("Sobrevivente de apoio (modo dupla): seus afixos de ouro vao para um campo separado do LootDropper.")]
    public bool isPartner;
    [Tooltip("Posicao no esquadrao: 0 = frente (principal), 1 = apoio do meio, 2 = apoio do fundo (trio).")]
    public int supportSlot;
    /// <summary>Sobrevivente que este PlayerStats representa (definido em ApplySurvivor).</summary>
    public SurvivorDef Survivor { get; private set; }
    float xpRemainder;   // fracao de XP acumulada (com 50/50, um zumbi de 5 XP rende 2,5)

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    int missionStartLevel = 1;
    /// <summary>Niveis ganhos nesta missao. Os pontos de habilidade sao creditados junto com o nivel, ao fim da missao.</summary>
    public int LevelsGainedThisMission => Mathf.Max(0, Level - missionStartLevel);
    public int XpToNext => XpForLevel(Level);
    public static int XpForLevel(int level) => Balance.XpForLevel(level);

    public float TotalDamage { get; private set; }
    public float TotalHealth { get; private set; }
    public float TotalDefense { get; private set; }

    public event Action Changed;

    readonly Dictionary<ItemSlot, ItemInstance> equipped = new Dictionary<ItemSlot, ItemInstance>();
    Health health;
    Hero hero;
    float survivorXpMult = 1f;
    SkillTreeBonuses skillBonuses = new SkillTreeBonuses();
    LootDropper loot;

    /// <summary>Soma dos afixos dos itens equipados.</summary>
    public AffixBonuses Affix { get; private set; }

    void Awake()
    {
        health = GetComponent<Health>();
        hero = GetComponent<Hero>();
    }

    void OnEnable()  => GameEvents.EnemyKilled += OnKill;
    void OnDisable() => GameEvents.EnemyKilled -= OnKill;

    void Start()
    {
        loot = FindFirstObjectByType<LootDropper>();
        Recalculate(true);
    }

    void Update()
    {
        // Afixo de regeneracao: fracao da vida maxima por segundo.
        if (Affix.regen > 0f && health != null && health.Current > 0f)
            health.Heal(health.max * Affix.regen * Time.deltaTime);
    }

    public void ApplyDifficulty(DifficultyDef d)
    {
        if (d == null) return;
        difficultyXpMult = d.xpMult;
        levelUpHealMultiplier = d.levelUpHealMult;
        RefreshXpMultiplier();
    }

    void OnKill(Enemy e)
    {
        if (health != null && health.Current <= 0f) return;   // quem caiu nao ganha XP
        if (e != null && e.data != null) AddXpScaled(e.data.xp * Balance.XpScale(GameSession.Location) * Mathf.Max(0f, xpShare));
    }

    public void SetProgress(int level, int xp)
    {
        Level = Mathf.Clamp(level, 1, Balance.MaxLevel);
        Xp = Level >= Balance.MaxLevel ? 0 : Mathf.Max(0, xp);
        missionStartLevel = Level;
    }

    public void ApplySurvivor(SurvivorDef s)
    {
        if (s == null) return;
        Survivor = s;
        healthMult = s.healthMult;
        survivorXpMult = s.xpMult;
        RefreshXpMultiplier();
    }

    public void ApplySkillBonuses(SkillTreeBonuses bonuses)
    {
        skillBonuses = bonuses ?? new SkillTreeBonuses();
        RefreshXpMultiplier();
    }

    void RefreshXpMultiplier()
    {
        xpMult = survivorXpMult * (1f + skillBonuses.xpPercent + Affix.xp) * difficultyXpMult;
    }

    public void AddXp(int amount) => AddXpScaled(amount);

    void AddXpScaled(float raw)
    {
        if (Level >= Balance.MaxLevel) return;   // nivel maximo: o XP deixa de acumular
        float v = raw * xpMult + xpRemainder;
        int whole = Mathf.FloorToInt(v);
        xpRemainder = v - whole;
        Xp += whole;
        while (Level < Balance.MaxLevel && Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            Recalculate(false, levelUpHealFraction * Mathf.Max(0f, levelUpHealMultiplier));
            GameEvents.LevelUp?.Invoke(Level);
            LevelUpEffect.Play(this, Level);   // efeito visual + som (por sobrevivente que subiu)
        }
        if (Level >= Balance.MaxLevel) Xp = 0;
        Changed?.Invoke();
    }

    // ---- equipamento ----
    public ItemInstance Equip(ItemInstance item)
    {
        if (item == null) return null;
        equipped.TryGetValue(item.slot, out var old);
        equipped[item.slot] = item;
        Recalculate(false);
        return old;
    }

    public ItemInstance Unequip(ItemSlot slot)
    {
        if (!equipped.TryGetValue(slot, out var old)) return null;
        equipped.Remove(slot);
        Recalculate(false);
        return old;
    }

    public ItemInstance GetEquipped(ItemSlot slot)
    {
        equipped.TryGetValue(slot, out var i);
        return i;
    }

    public List<ItemInstance> EquippedList() => new List<ItemInstance>(equipped.Values);

    // Carrega do save sem recalcular; Start faz o cálculo inicial da missão.
    public void LoadEquipped(List<ItemInstance> list)
    {
        equipped.Clear();
        if (list == null) return;
        foreach (var it in list) if (it != null) equipped[it.slot] = it;
    }

    /// <param name="fullHeal">Enche a vida (usado so no inicio da missao).</param>
    /// <param name="bonusHealFraction">Cura extra, como fracao da vida maxima (usado no level-up).</param>
    void Recalculate(bool fullHeal, float bonusHealFraction = 0f)
    {
        float hp = baseHealth + healthPerLevel * (Level - 1);
        float dmg = baseDamage + damagePerLevel * (Level - 1);
        float def = baseDefense + defensePerLevel * (Level - 1);

        foreach (var item in equipped.Values)
        {
            if (item == null) continue;
            hp += item.bonusHealth;
            dmg += item.bonusDamage;
            def += item.bonusDefense;
        }

        hp *= healthMult * Mathf.Max(0.1f, 1f + skillBonuses.healthPercent);
        def += skillBonuses.defenseFlat;

        TotalHealth = hp;
        TotalDamage = dmg;
        TotalDefense = def;

        // Mantem a PROPORCAO de vida atual (equipar/subir de nivel nao cura nem fere de graca).
        // Um heroi morto continua morto.
        float ratio = health.max > 0f ? health.Current / health.max : 1f;
        float newCurrent = fullHeal
            ? hp
            : (health.Current <= 0f ? 0f : Mathf.Clamp(hp * (ratio + Mathf.Max(0f, bonusHealFraction)), 1f, hp));
        health.defense = def;
        health.SetValues(hp, newCurrent);
        hero.damageBonus = dmg;

        // Afixos dos itens equipados -> heroi, XP e ouro.
        Affix = AffixBonuses.Sum(equipped.Values);
        hero.affixDamage = Affix.damage;
        hero.affixCritChance = Affix.critChance;
        hero.affixCritDamage = Affix.critDamage;
        hero.affixFireRate = Affix.fireRate;
        hero.affixRange = Affix.range;
        hero.affixAccuracy = Affix.accuracy;
        RefreshXpMultiplier();
        if (loot != null)
        {
            if (isPartner && supportSlot >= 2) loot.thirdAffixGoldPercent = Affix.gold;
            else if (isPartner) loot.partnerAffixGoldPercent = Affix.gold;
            else loot.affixGoldPercent = Affix.gold;
        }

        Changed?.Invoke();
    }
}
