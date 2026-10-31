using System.Collections.Generic;
using UnityEngine;

public class MissionSetup : MonoBehaviour
{
    readonly List<WeaponData> runtimeWeapons = new List<WeaponData>();

    void OnDestroy()
    {
        foreach (var w in runtimeWeapons) if (w) Destroy(w);
        runtimeWeapons.Clear();
    }

    void Awake()
    {
        GameSession.EnsureDefaults();
        var s = GameSession.Survivor;
        var loc = GameSession.Location;
        var save = GameSession.Save;
        if (save != null) save.MigrateFromPreviousVersions();

        var stats = FindFirstObjectByType<PlayerStats>();
        if (stats == null)
        {
            Debug.LogError("Zombit: PlayerStats não foi encontrado na cena da missão.");
            return;
        }
        var hero = stats.GetComponent<Hero>();
        if (hero == null)
        {
            Debug.LogError("Zombit: Hero não foi encontrado no objeto PlayerStats.");
            return;
        }

        HeroSquad.Clear();

        // Dupla/trio: cada APOIO e um clone do heroi da cena, feito ANTES de configurar o principal
        // (assim herda o estado original: arma-molde da cena, Health/PlayerStats limpos).
        // O 3o sobrevivente so entra no Pesadelo (HeroSquad.TrioDifficulty).
        var supportDefs = new List<SurvivorDef>();
        var partnerDef = GameSession.Partner;
        if (partnerDef != null && partnerDef.id != s.id) supportDefs.Add(partnerDef);
        var thirdDef = GameSession.Third;
        if (supportDefs.Count == 1 && thirdDef != null && (GameSession.Difficulty >= HeroSquad.TrioDifficulty || GameSession.IsRange)
            && thirdDef.id != s.id && thirdDef.id != partnerDef.id) supportDefs.Add(thirdDef);

        var supportStats = new List<PlayerStats>();
        for (int i = 0; i < supportDefs.Count; i++)
        {
            var go = Instantiate(stats.gameObject, stats.transform.parent);
            go.name = i == 0 ? "Hero_Partner" : "Hero_Third";
            supportStats.Add(go.GetComponent<PlayerStats>());
        }
        int squadSize = 1 + supportStats.Count;

        // Nivel de dificuldade escolhido no mapa (1 Facil .. 4 Pesadelo).
        var diff = Difficulties.Get(GameSession.Difficulty);
        var tuning = GameTuning.Current;
        float roadY = tuning.HeroFeetY(0);   // pes do primeiro; os apoios descem em cascata (cascadeStepY)

        // ---- principal (frente) ----
        var prog = save.GetProgress(s.id);
        var skillBonuses = SkillTreeSystem.GetBonuses(s.id, prog);
        float frontX = HeroSquad.FrontXFor(squadSize), stepX = HeroSquad.OffsetXFor(squadSize);
        ConfigureHero(stats, s, prog, skillBonuses, diff, new Vector2(frontX, roadY), 0, squadSize);
        HeroSquad.Register(stats, s, true, prog.level, prog.xp);

        // Carrega mochila (compartilhada) + equipamento do principal antes de PlayerStats.Start.
        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory != null)
        {
            inventory.stats = stats;
            inventory.LoadFrom(save, s.id, stats);
        }

        // ---- apoios (atras, em fila: meio e fundo) ----
        var supportBonuses = new List<SkillTreeBonuses>();
        for (int i = 0; i < supportStats.Count; i++)
        {
            var d = supportDefs[i];
            var sp = save.GetProgress(d.id);
            var sb = SkillTreeSystem.GetBonuses(d.id, sp);
            supportBonuses.Add(sb);
            var pos = new Vector2(frontX - stepX * (i + 1), tuning.HeroFeetY(i + 1));
            ConfigureHero(supportStats[i], d, sp, sb, diff, pos, i + 1, squadSize);
            supportStats[i].LoadEquipped(sp.equipped);   // equipamento proprio; a mochila e a mesma
            HeroSquad.Register(supportStats[i], d, false, sp.level, sp.xp);
        }

        var loot = FindFirstObjectByType<LootDropper>();
        if (loot != null)
        {
            // Bonus de loot: vale o MELHOR do esquadrao (o Explorer de apoio tambem rende).
            float gold = s.goldMult * (1f + skillBonuses.goldPercent);
            float drop = s.dropMult + skillBonuses.dropPercent;
            for (int i = 0; i < supportDefs.Count; i++)
            {
                gold = Mathf.Max(gold, supportDefs[i].goldMult * (1f + supportBonuses[i].goldPercent));
                drop = Mathf.Max(drop, supportDefs[i].dropMult + supportBonuses[i].dropPercent);
            }
            loot.goldMult = gold;
            loot.dropChanceMult = drop;
            loot.partnerAffixGoldPercent = 0f;
            loot.thirdAffixGoldPercent = 0f;
            loot.scrapMult = ScrapRules.MissionMult(diff, loc);   // sucata por abate (fica mesmo se morrer)
        }

        // Trio: zumbis e chefes com mais vida (so aumentar a horda nao segura 3 armas).
        float squadHp = HeroSquad.ZombieHealthMultFor(squadSize);
        Enemy.HealthMult = loc.EnemyHealthMult * diff.zombieHealth * squadHp;
        Enemy.BossHealthMult = loc.BossHealthMult * diff.zombieHealth * squadHp;   // baseBoss x (1 + 0,12 x (rec-1))
        Enemy.DamageMult = loc.EnemyDamageMult * diff.zombieDamage;
        Enemy.SpeedMult = diff.speedMult;
        Enemy.EliteEnrage = diff.eliteEnrage;
        Enemy.MaxAttackersPerSide = Mathf.Max(1, diff.attackersPerSide);   // quantos zumbis batem juntos
        Enemy.FuryDamage = 1f;
        Enemy.FurySpeed = 1f;
        Enemy.DamageDealt = 0f;
        // Vinheta vermelha + leve tremor de tela: so no principal (dois tremeriam a camera em dobro).
        // Se ele cair, o HeroSquad passa o efeito para o apoio.
        if (hero.GetComponent<HeroDamageFeedback>() == null) hero.gameObject.AddComponent<HeroDamageFeedback>();

        // Rotulo sobre cada sobrevivente: classe + nivel + barra de vida. O nick fica no painel de wave do HUD.
        PlayerNicknameDisplay.Attach(hero.transform, s);
        for (int i = 0; i < supportStats.Count; i++)
            PlayerNicknameDisplay.Attach(supportStats[i].transform, supportDefs[i]);

        new GameObject("WaveBackground").AddComponent<WaveBackground>();

        if (GameSession.IsRange)
        {
            // Estande de tiro: sem spawners; o ShootingRange cria os bonecos e o painel de DPS.
            var rangeSpawner = FindFirstObjectByType<WaveSpawner>();
            if (rangeSpawner != null) rangeSpawner.enabled = false;
            new GameObject("ShootingRange").AddComponent<ShootingRange>().Init(hero.transform, squadSize);
        }
        else
        {
        var ws = FindFirstObjectByType<WaveSpawner>();
        if (ws == null)
        {
            Debug.LogError("Zombit: WaveSpawner não foi encontrado na cena da missão.");
            return;
        }
        // Always wire the real hero into the spawner. A missing Inspector reference
        // otherwise creates enemies with no target, making them stand still.
        // Os zumbis nascem mirando o principal (que esta na frente); se ele cair, o Enemy troca de alvo.
        ws.hero = hero.transform;
        if (ws.spawnPoints != null)
        {
            for (int i = 0; i < ws.spawnPoints.Length; i++)
            {
                if (ws.spawnPoints[i] == null) continue;
                var p = ws.spawnPoints[i].position;
                // Todos os zumbis na mesma pista, centrada na cascata do grupo (GameTuning.ZombieFeetY).
                ws.spawnPoints[i].position = new Vector3(9f, tuning.ZombieFeetY(squadSize), p.z);
            }
        }

        // Mais atiradores = mais DPS: hordas maiores na dupla (x1,5) e no trio (x2).
        float squadMult = HeroSquad.HordeMultFor(squadSize);
        var roster = EnemyRoster.Get(loc.theme);
        if (roster.HasArt)
        {
            ws.enabled = false;
            if (tuning.hordeMode)
            {
                // Horda continua: sem pausas entre waves (veja HordeSpawner e GameTuning).
                var hs = ws.gameObject.AddComponent<HordeSpawner>();
                hs.hero = hero.transform;
                hs.spawnPoints = ws.spawnPoints;
                hs.theme = loc.theme;
                hs.phases = Mathf.Max(1, loc.waves);
                hs.phaseSeconds = tuning.hordePhaseSeconds;
                hs.baseRate = tuning.hordeBaseRate;
                hs.ramp = tuning.hordeRampPerSecond;
                hs.maxAlive = tuning.hordeMaxAlive;
                hs.countMultiplier = loc.countMult * squadMult;   // o nivel entra por hordeRate (abaixo)
                hs.hordeRate = diff.hordeRate;
                hs.hasBoss = loc.hasBoss;
                hs.eliteMods = diff.eliteMods;
                hs.eliteShare = diff.eliteShare;
                hs.spitterShare = diff.spitterShare;
                hs.furyStart = diff.furyStart;
            }
            else
            {
                var ts = ws.gameObject.AddComponent<ThemedSpawner>();
                ts.hero = hero.transform;
                ts.spawnPoints = ws.spawnPoints;
                ts.theme = loc.theme;
                ts.waveCount = loc.waves;
                ts.countMultiplier = loc.countMult * diff.countMult * squadMult;
                ts.hasBoss = loc.hasBoss;
                ts.flankChance = 0f;   // hordas so pela direita
                ts.eliteMods = diff.eliteMods;
                ts.waveTimeLimit = diff.waveTimeLimit;
            }
        }
        else
        {
            ws.waveCount = loc.waves;
            ws.countMultiplier = loc.countMult * diff.countMult * squadMult;
            ws.flankChance = 0f;   // hordas so pela direita
            ws.waveTimeLimit = diff.waveTimeLimit;
        }
        }   // fim do else (nao e estande)

        // Cada sobrevivente usa a PROPRIA habilidade (granada, cura, torreta, precisao, saque).
        stats.gameObject.AddComponent<SurvivorAbilities>().Init(s, skillBonuses);
        for (int i = 0; i < supportStats.Count; i++)
            supportStats[i].gameObject.AddComponent<SurvivorAbilities>().Init(supportDefs[i], supportBonuses[i]);
        // Onda de choque [ESPACO] + foco por clique: um controle so (no principal; passa ao apoio se ele cair).
        if (stats.GetComponent<ActiveSkills>() == null) stats.gameObject.AddComponent<ActiveSkills>();

        // Pool de recompensas: itens da cena + conteudo extra (+ exclusivos no Pesadelo).
        if (loot != null) loot.pool = ItemCatalog.BuildPool(loot.pool, GameSession.Difficulty >= (int)Difficulty.Pesadelo);

        // Registra a run no save. Se o jogo for fechado antes do fim, ao reabrir conta como derrota.
        bool insured = false;
        if (!GameSession.DebugRun && !GameSession.IsRange && save != null)
        {
            insured = GameSession.InsuranceBought;
            save.activeRun = new ActiveRun
            {
                active = true, locationId = loc.id, survivorId = s.id,
                partnerId = supportDefs.Count > 0 ? supportDefs[0].id : "",
                thirdId = supportDefs.Count > 1 ? supportDefs[1].id : "",
                difficulty = GameSession.Difficulty, insured = insured
            };
            try { SaveSystem.Save(save); }
            catch (System.Exception e) { Debug.LogWarning("Zombit: falha ao registrar a missão ativa: " + e.Message); }
        }
        GameSession.InsuranceBought = false;

        var flow = new GameObject("MissionFlow").AddComponent<MissionFlow>();
        flow.stats = stats;
        flow.supports = supportStats;
        flow.loot = loot;
        flow.insured = insured;

        var pause = new GameObject("MissionPauseMenu").AddComponent<MissionPauseMenu>();
        pause.flow = flow;

        // Offensive posture is opt-in and never installed in DPS training.
        if (!GameSession.IsRange && GetComponent<SquadCombatPosture>() == null)
            gameObject.AddComponent<SquadCombatPosture>();
        if (!GameSession.IsRange && squadSize > 1 && GetComponent<SquadFormationSwap>() == null)
            gameObject.AddComponent<SquadFormationSwap>();
    }

    /// <summary>Arma, atributos, progresso, sprite e posicao de UM sobrevivente.</summary>
    void ConfigureHero(PlayerStats stats, SurvivorDef s, SurvivorProgress prog, SkillTreeBonuses skillBonuses,
        DifficultyDef diff, Vector2 position, int supportSlot, int squadSize)
    {
        var hero = stats.GetComponent<Hero>();
        hero.muzzleUV = s.muzzleUV;
        hero.muzzleSearchBandUV = 0.12f;
        // Arma exclusiva do sobrevivente (M4A1, SCAR-20, MP5-SD, M249 ou MAC-10). Copia o template
        // da cena para manter o prefab do projetil. Precisa vir ANTES do SurvivorAbilities (a torreta
        // do Engineer clona hero.weapon) e roda no Awake, antes do Hero.Start encher o pente.
        var weapon = WeaponCatalog.CreateRuntime(s, hero.weapon);
        runtimeWeapons.Add(weapon);
        hero.weapon = weapon;
        hero.damageMult = s.damageMult * (1f + skillBonuses.damagePercent);
        hero.fireRateMult = s.fireRateMult * (1f + skillBonuses.fireRatePercent);
        hero.rangeMult = s.rangeMult * (1f + skillBonuses.rangePercent);
        hero.accuracyBonus = s.accuracyBonus + skillBonuses.accuracyBonus;
        hero.critChance = Mathf.Clamp01(s.critChance + skillBonuses.critChance);
        hero.critMult = s.critMult * (1f + skillBonuses.critDamagePercent);

        stats.isPartner = supportSlot > 0;
        stats.supportSlot = supportSlot;
        stats.xpShare = HeroSquad.XpShareFor(squadSize);   // 50/50 na dupla, 40% cada no trio
        stats.ApplySurvivor(s);
        stats.ApplySkillBonuses(skillBonuses);
        stats.ApplyDifficulty(diff);   // XP do nivel + cura de level-up (0 no Dificil/Pesadelo)
        stats.SetProgress(prog.level, prog.xp);

        var characterMotion = hero.GetComponent<PixelCharacterMotion>();
        if (characterMotion == null) characterMotion = hero.gameObject.AddComponent<PixelCharacterMotion>();
        // A animacao ja movimenta o corpo; desliga o bob procedural apenas no Assaulter.
        if (s.id == "assaulter") characterMotion.bobAmount = 0f;

        var sr = hero.GetComponent<SpriteRenderer>();
        var heroSprite = s.fullBody != null ? s.fullBody : ArtLoader.Get("Art/Characters/" + s.id + "_full");
        if (sr != null)
        {
            if (heroSprite != null)
            {
                sr.sprite = heroSprite;
                sr.color = Color.white;
                // Altura VISIVEL do heroi (sem a margem transparente do PNG), mesma regra dos zumbis.
                float k = Mathf.Max(0.1f, s.heroHeight) / SpriteMetrics.WorldHeight(heroSprite);
                hero.transform.localScale = new Vector3(k, k, 1f);
            }
            else sr.color = s.color;
        }
        if (hero.GetComponent<YSort>() == null) hero.gameObject.AddComponent<YSort>();
        // GroundY is the road baseline. Move the sprite so its actual bottom (feet),
        // rather than its pivot/center, sits on the road.
        hero.transform.position = new Vector3(position.x, position.y, 0f);
        if (sr != null && sr.sprite != null)
        {
            float feet = sr.bounds.min.y + SpriteMetrics.Get(sr.sprite).bottomPx / sr.sprite.pixelsPerUnit * Mathf.Abs(hero.transform.localScale.y);
            hero.transform.position += Vector3.up * (position.y - feet);
        }

        // O Assaulter usa sprites de caminhada proprios; a direcao e lida do movimento
        // horizontal real aplicado por SquadCombatPosture (avanco para direita, recuo para esquerda).
        if (!GameSession.IsRange && s.id == "assaulter" && hero.GetComponent<AssaulterMovementAnimation>() == null)
            hero.gameObject.AddComponent<AssaulterMovementAnimation>();
    }
}
