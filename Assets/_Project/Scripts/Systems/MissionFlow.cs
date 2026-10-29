using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class MissionFlow : MonoBehaviour
{
    public PlayerStats stats;
    /// <summary>Sobreviventes de apoio: vazio (solo), 1 (dupla) ou 2 (trio, so no Pesadelo).</summary>
    public List<PlayerStats> supports = new List<PlayerStats>();
    public LootDropper loot;
    /// <summary>Seguro pago para esta run: anula a perda de equipamento.</summary>
    public bool insured;

    bool done;
    float elapsed;
    int startLevel, startXp;

    sealed class SupportInfo
    {
        public PlayerStats stats;
        public SurvivorDef def;
        public int startLevel, startXp;
    }
    readonly List<SupportInfo> squad = new List<SupportInfo>();

    void Start()
    {
        // Nivel/XP de partida (so para mostrar o que foi perdido; o save nao e tocado ate o fim da missao).
        var s = GameSession.Survivor;
        var save = GameSession.Save;
        if (s != null && save != null)
        {
            var p = save.GetProgress(s.id);
            startLevel = p.level; startXp = p.xp;
        }
        squad.Clear();
        if (supports != null)
            foreach (var ps in supports)
            {
                var pm = ps != null ? HeroSquad.Find(ps) : null;
                if (pm == null || pm.def == null) continue;
                squad.Add(new SupportInfo { stats = ps, def = pm.def, startLevel = pm.startLevel, startXp = pm.startXp });
            }
    }

    bool IsAlive(PlayerStats ps)
    {
        if (ps == null) return false;
        var h = ps.GetComponent<Health>();
        return h == null || h.Current > 0f;
    }

    /// <summary>True after the mission ends or the player leaves it.</summary>
    public bool IsFinished => done;

    void Update()
    {
        if (!done) elapsed += Time.deltaTime;
    }

    void OnEnable()
    {
        GameEvents.MissionComplete += OnWin;
        GameEvents.MissionFailed += OnLose;
    }

    void OnDisable()
    {
        GameEvents.MissionComplete -= OnWin;
        GameEvents.MissionFailed -= OnLose;
    }

    /// <summary>
    /// Abandonar CONTA COMO MORTE (senao o jogador sairia antes de morrer): nivel/XP/pontos da missao sao perdidos,
    /// rola a chance de perder equipamento e fica metade do ouro recolhido. Nao conclui o local.
    /// </summary>
    public void AbandonToMap()
    {
        if (GameSession.IsRange) { ExitTrainingToMap(); return; }
        if (done) return;
        done = true;

        var save = GameSession.Save;
        var survivor = GameSession.Survivor;
        if (save != null && survivor != null && stats != null)
        {
            var inventory = FindFirstObjectByType<Inventory>();
            var result = ApplyDeathAll(save, survivor, inventory);

            if (!GameSession.DebugRun)
            {
                if (loot != null)
                {
                    save.gold += Mathf.Max(0, loot.gold / 2);
                    save.scrap += Mathf.Max(0, loot.scrap / 2);   // abandonar conta como derrota: metade da sucata
                }
                save.activeRun = new ActiveRun();
                save.pendingNotice = DeathPenalty.Describe(result, "Você abandonou a missão (conta como derrota).");
                try
                {
                    SaveSystem.Save(save);
                    Debug.Log($"Zombit: missão abandonada (contou como derrota); salvo no Slot {save.slot}.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Zombit: falha ao salvar ao sair para o mapa: " + e.Message);
                }
            }
        }

        GameSession.OpenMapOnLoad = !GameSession.DebugRun && GameSession.Save != null;
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameSession.MenuSceneIndex);
    }

    /// <summary>
    /// Penalidade de morte: nada do progresso da run e gravado (nivel/XP/pontos ficam como no inicio) e
    /// rola a perda de equipamento sobre o estado VIVO (PlayerStats), para o auto-save do inventario nao ressuscitar o item.
    /// </summary>
    DeathPenalty.Result ApplyDeath(SaveData save, SurvivorDef survivor, Inventory inventory)
        => ApplyDeathFor(save, stats, survivor, inventory, true);

    /// <summary>Penalidade de morte de UM sobrevivente. O principal grava via Inventory; o apoio direto no progresso.</summary>
    DeathPenalty.Result ApplyDeathFor(SaveData save, PlayerStats who, SurvivorDef survivor, Inventory inventory, bool primary)
    {
        var result = DeathPenalty.Roll(who.EquippedList(), GameSession.Difficulty, insured);
        foreach (var it in result.lost) who.Unequip(it.slot);
        if (!GameSession.DebugRun) DeathPenalty.Register(save, GameSession.Location.id, GameSession.Difficulty, result.lost);

        if (primary) { if (inventory != null) inventory.SaveTo(save, survivor.id, who); }
        else save.GetProgress(survivor.id).equipped = who.EquippedList();
        return result;
    }

    /// <summary>Derrota/abandono: todos os sobreviventes do esquadrao sofrem a penalidade.</summary>
    DeathPenalty.Result ApplyDeathAll(SaveData save, SurvivorDef survivor, Inventory inventory)
    {
        var result = ApplyDeath(save, survivor, inventory);
        foreach (var sp in squad)
            DeathPenalty.Merge(result, ApplyDeathFor(save, sp.stats, sp.def, inventory, false));
        return result;
    }

    /// <summary>Vitoria: grava nivel/XP/pontos ganhos por este sobrevivente.</summary>
    static void SaveProgress(SurvivorProgress p, PlayerStats who)
    {
        int before = p.level;
        p.level = who.Level;
        p.xp = who.Xp;
        p.skillPoints += Balance.SkillPointsBetween(before, who.Level);
    }

    void OnWin() => Finish(true);
    void OnLose() => Finish(false);

    static float ExtraDropChance(SurvivorDef s, SkillTreeBonuses bonuses) =>
        s == null ? 0f : Mathf.Clamp01(s.dropMult - 1f + (bonuses != null ? bonuses.dropPercent : 0f));

    /// <summary>
    /// Plano de sorteios da vitoria (tudo no fim da missao, sem duplicar drops durante a horda):
    /// conclusao (2, +1 Pesadelo, +1 Explorer), chefe (1, Raro ou superior) e elites abatidas (ate 3, Incomum ou superior).
    /// </summary>
    static List<Balance.LootSource> BuildLootPlan(LocationDef loc, int difficulty, float extraDropChance, int elitesKilled)
    {
        var plan = new List<Balance.LootSource>();
        int n = Balance.CompletionRolls(difficulty, extraDropChance);
        for (int i = 0; i < n; i++) plan.Add(Balance.LootSource.Completion);
        if (loc.hasBoss) plan.Add(Balance.LootSource.Boss);
        float p = Balance.EliteRollChance(difficulty);
        int ne = 0;
        for (int i = 0; i < elitesKilled && ne < Balance.EliteRollCap; i++)
            if (UnityEngine.Random.value < p) ne++;
        for (int i = 0; i < ne; i++) plan.Add(Balance.LootSource.Elite);
        return plan;
    }

    void Finish(bool win)
    {
        // Training events must never run campaign rewards or penalties.
        if (GameSession.IsRange) return;
        if (done) return;
        done = true;

        var save = GameSession.Save;
        var s = GameSession.Survivor;
        var loc = GameSession.Location;
        int level = GameSession.Difficulty;
        var diff = Difficulties.Get(level);

        // Avaliação por estrelas (só em vitória), calculada ANTES de qualquer painel:
        //   3 estrelas = terminou com >= 70% de vida
        //   2 estrelas = terminou com >= 40% de vida
        //   1 estrela  = vitória com menos de 40%
        //   derrota    = 0 estrelas (sem avaliação)
        // Modo dupla: vida SOMADA da equipe (quem caiu conta 0).
        float hpRatio = 1f;
        if (HeroSquad.Count > 0) hpRatio = HeroSquad.TeamHealthRatio();
        else
        {
            var heroHp = stats != null ? stats.GetComponent<Health>() : null;
            if (heroHp != null && heroHp.max > 0f) hpRatio = Mathf.Clamp01(heroHp.Current / heroHp.max);
        }
        bool primaryAlive = IsAlive(stats);
        int stars = !win ? 0 : hpRatio >= 0.70f ? 3 : hpRatio >= 0.40f ? 2 : 1;

        int missionGold = loot != null ? loot.gold : 0;
        int goldGain = win ? missionGold + Mathf.RoundToInt(loc.goldReward * Balance.RewardGoldMult(level)) : missionGold / 2;
        save.gold += goldGain;

        // Sucata: derrota guarda 100% (cada tentativa fortalece o heroi); vitoria rende +15%.
        int scrapGain = loot != null ? loot.scrap : 0;
        if (win) scrapGain = Mathf.RoundToInt(scrapGain * ScrapRules.WinBonus);
        save.scrap += scrapGain;

        var p = save.GetProgress(s.id);
        var inv = FindFirstObjectByType<Inventory>();

        var drops = new List<ItemInstance>();
        var recovered = new List<ItemInstance>();
        int soldGold = 0, soldCount = 0;
        string penaltyText = "";
        string unlockText = "";
        string fallenNote = "";
        bool newUnlock = false;

        if (win)
        {
            // Vitoria: o progresso da run e gravado para quem terminou DE PE.
            // Quem caiu no caminho (modo dupla) sofre a penalidade de morte (HeroSquad.FallenAllyPenalizedOnWin).
            bool penalize = HeroSquad.FallenAllyPenalizedOnWin;
            if (primaryAlive || !penalize) SaveProgress(p, stats);
            else fallenNote = FallenNote(s.name, ApplyDeathFor(save, stats, s, inv, true));
            foreach (var sp in squad)
            {
                var pp = save.GetProgress(sp.def.id);
                if (IsAlive(sp.stats) || !penalize)
                {
                    SaveProgress(pp, sp.stats);
                    pp.equipped = sp.stats.EquippedList();
                }
                else
                {
                    string note = FallenNote(sp.def.name, ApplyDeathFor(save, sp.stats, sp.def, inv, false));
                    fallenNote = fallenNote.Length > 0 ? fallenNote + "\n" + note : note;
                }
            }

            if (loot != null && loot.pool != null && loot.pool.Length > 0)
            {
                // Chance de drop extra: vale a melhor do esquadrao (Explorer de apoio tambem conta).
                float extra = ExtraDropChance(s, SkillTreeSystem.GetBonuses(s.id, p));
                foreach (var sp in squad)
                    extra = Mathf.Max(extra, ExtraDropChance(sp.def, SkillTreeSystem.GetBonuses(sp.def.id, save.GetProgress(sp.def.id))));
                var plan = BuildLootPlan(loc, level, extra, loot.elitesKilled);
                foreach (var source in plan)
                {
                    var it = LootTable.Roll(loot.pool, loc.difficulty, level, source);
                    if (it == null) continue;
                    if (Store(inv, save, it)) drops.Add(it);
                    else { save.gold += it.sellValue; soldGold += it.sellValue; soldCount++; }
                }
            }

            // Item perdido por morte neste mesmo local e nivel: drop garantido.
            foreach (var it in DeathPenalty.TakeRecoverable(save, loc.id, level))
            {
                if (Store(inv, save, it)) { drops.Add(it); recovered.Add(it); }
                else { save.gold += it.sellValue; soldGold += it.sellValue; soldCount++; }
            }
            if (inv != null) inv.SaveTo(save, s.id, stats);

            bool hadNormal = save.HighestCompleted(loc.id) >= (int)Difficulty.Normal;
            bool first = save.MarkCompleted(loc.id, level);
            newUnlock = first && level == (int)Difficulty.Normal && !hadNormal;
            if (first && level < (int)Difficulty.Pesadelo)
            {
                var next = Difficulties.Get(level + 1);
                unlockText = $"Nível {next.name} liberado neste local.";
            }
            if (newUnlock) unlockText = (unlockText.Length > 0 ? unlockText + "  " : "") + "Novas áreas podem ter sido desbloqueadas!";
        }
        else
        {
            // Derrota: nivel, XP e pontos de habilidade ficam como no inicio da missao (nao sao gravados).
            var result = ApplyDeathAll(save, s, inv);
            int lostLevels = Mathf.Max(0, stats.Level - startLevel);
            string header;
            if (squad.Count > 0)
            {
                var parts = new List<string> { $"{s.name} volta ao nível {startLevel}" };
                foreach (var sp in squad) parts.Add($"{sp.def.name} ao nível {sp.startLevel}");
                string who = squad.Count == 1 ? "Os dois caíram" : "A equipe inteira caiu";
                header = $"{who}: todo o XP da missão foi perdido. " + JoinPt(parts) + ".";
            }
            else header = lostLevels > 0
                ? $"Progresso da missão perdido: {lostLevels} nível(is) e todo o XP ganho. Você volta ao nível {startLevel}."
                : "Todo o XP ganho nesta missão foi perdido.";
            penaltyText = DeathPenalty.Describe(result, header);
        }

        save.activeRun = new ActiveRun();   // missao terminada: nao conta mais como interrompida

        bool saved = false;
        if (!GameSession.DebugRun)
        {
            try { SaveSystem.Save(save); saved = true; }
            catch (Exception e) { Debug.LogWarning("Zombit: falha ao salvar: " + e.Message); }
        }

        Time.timeScale = 0f;
        BuildPanel(win, goldGain, scrapGain, unlockText, drops, recovered.Count, soldCount, soldGold, saved, penaltyText, stars, hpRatio,
            fallenNote, primaryAlive);
    }

    /// <summary>"a, b e c".</summary>
    static string JoinPt(List<string> parts)
    {
        if (parts.Count <= 1) return parts.Count == 1 ? parts[0] : "";
        return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " e " + parts[parts.Count - 1];
    }

    static string FallenNote(string name, DeathPenalty.Result r)
    {
        string items = r.lost.Count > 0 ? $" e {r.lost.Count} item(ns) equipado(s) (voltam como drop garantido aqui)" : "";
        return $"{name} caiu em combate: perdeu o XP desta missão{items}.";
    }

    static bool Store(Inventory inv, SaveData save, ItemInstance it)
    {
        if (inv != null) return inv.Add(it);
        save.inventory.Add(it);
        return true;
    }

    static string Fmt(float v) => v.ToString("0.#");

    static string StatLines(ItemInstance it)
    {
        var sb = new StringBuilder();
        if (it.bonusDamage != 0) sb.AppendLine($"Dano +{Fmt(it.bonusDamage)}");
        if (it.bonusHealth != 0) sb.AppendLine($"Vida +{Fmt(it.bonusHealth)}");
        if (it.bonusDefense != 0) sb.AppendLine($"Defesa +{Fmt(it.bonusDefense)}");
        if (it.HasAffixes) foreach (var a in it.affixes) if (a != null) sb.AppendLine("<size=80%>" + a.Describe() + "</size>");
        return sb.ToString();
    }

    void StatBox(Transform parent, string label, string value, float x, float y)
    {
        var box = UIKit.Frame(parent, "Stat_" + label, new Color(0.028f, 0.047f, 0.059f, 0.98f),
            new Color(0.20f, 0.24f, 0.29f, 0.9f), 1.5f);
        UIFactory.Place(box, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(340, 120));
        var l = UIFactory.Txt(box, label, 24, TextAlignmentOptions.Center, UIFactory.Gray, "Label");
        UIFactory.Stretch(l.rectTransform, 0, 0.58f, 1, 0.96f);
        var v = UIFactory.Txt(box, value, 46, TextAlignmentOptions.Center, Color.white, "Value");
        UIFactory.Stretch(v.rectTransform, 0, 0.04f, 1, 0.62f);
    }

    void ItemCard(Transform parent, ItemInstance it, float x, float y)
    {
        var col = RarityColors.Get(it.rarity);
        var border = UIFactory.Img(parent, col, "Card");
        UIKit.ApplyChamfer(border, col);
        UIFactory.Place(border.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(210, 280));
        border.raycastTarget = false;

        var inner = UIFactory.Img(border.transform, new Color(0.09f, 0.09f, 0.12f, 1f), "Inner");
        UIKit.ApplyChamfer(inner, new Color(0.09f, 0.09f, 0.12f, 1f));
        UIFactory.Inset(UIFactory.Stretch(inner.rectTransform), 4, 4);

        var icon = UIFactory.Img(inner.transform, ItemVisual.Tint(it), "Icon");
        icon.sprite = ItemVisual.Icon(it);
        UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(90, 90));

        var name = UIFactory.Txt(inner.transform, it.name, 22, TextAlignmentOptions.Center, col, "Name");
        UIFactory.Stretch(name.rectTransform, 0.03f, 0.40f, 0.97f, 0.62f);

        var st = UIFactory.Txt(inner.transform, it.rarity + "\n" + StatLines(it), 20, TextAlignmentOptions.Top, Color.white, "Stats");
        UIFactory.Stretch(st.rectTransform, 0.05f, 0.03f, 0.95f, 0.40f);
    }

    // Três estrelas (avaliação da missão), preenchidas conforme a nota obtida.
    void StarRow(Transform parent, int earned)
    {
        var row = UIFactory.Node("StarRow", parent);
        UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(400f, 72f));
        for (int i = 0; i < 3; i++)
        {
            bool on = i < earned;
            var s = UIFactory.Img(row, on ? new Color(1f, 0.761f, 0.294f, 1f) : new Color(0.16f, 0.19f, 0.23f, 1f), "Star_" + i);
            s.sprite = UIKit.Star;
            s.type = Image.Type.Simple;
            s.rectTransform.anchorMin = s.rectTransform.anchorMax = s.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.rectTransform.anchoredPosition = new Vector2((i - 1) * 86f, 0f);
            s.rectTransform.sizeDelta = new Vector2(64f, 64f);
            s.raycastTarget = false;
        }
    }

    void BuildPanel(bool win, int goldGain, int scrapGain, string unlockText, List<ItemInstance> drops, int recoveredCount, int soldCount, int soldGold, bool saved, string penaltyText, int stars, float hpRatio,
        string fallenNote, bool primaryAlive)
    {
        var loc = GameSession.Location;
        var c = UIFactory.MakeCanvas("ResultCanvas", 100);
        var art = UIFactory.Img(c.transform, Color.white, "ResultBackdropArt");
        art.sprite = ArtLoader.Get(SceneBackgrounds.PathFor(loc));
        art.preserveAspect = false;
        art.color = new Color(0.58f, 0.65f, 0.70f, 0.60f);
        UIFactory.Stretch(art.rectTransform);
        art.raycastTarget = false;
        art.transform.SetAsFirstSibling();
        var dim = UIFactory.Img(c.transform, new Color(0.004f, 0.009f, 0.014f, 0.84f), "Dim");
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = false;
        var resultPanel = UIKit.Frame(c.transform, "ResultPanel", new Color(0.018f, 0.031f, 0.043f, 0.92f),
            new Color(0.20f, 0.24f, 0.29f, 0.95f), 2f);
        UIFactory.Place(resultPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1780f, 865f));
        var accent = UIFactory.Img(resultPanel, win ? new Color(1f, 0.761f, 0.294f, 1f) : new Color(0.847f, 0.141f, 0.169f, 1f), "ResultAccent");
        UIFactory.Place(accent.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(7f, 865f));
        accent.raycastTarget = false;
        var T = TextAlignmentOptions.Center;

        var title = UIFactory.Txt(c.transform, win ? "MISSÃO CONCLUÍDA" : "VOCÊ MORREU", 84, T, win ? UIFactory.Gold : UIFactory.Red, "ResultTitle");
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 1.1f;
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(1400, 130));

        // Avaliação por estrelas (regra: % de vida restante ao final da missão).
        StarRow(c.transform, stars);

        var diffDef = Difficulties.Get(GameSession.Difficulty);
        var place = UIFactory.Txt(c.transform, $"{loc.name}  ·  <color=#{ColorUtility.ToHtmlStringRGB(diffDef.color)}>{diffDef.name.ToUpperInvariant()}</color>", 34, T, UIFactory.Gray, "Place");
        UIFactory.Place(place.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -165), new Vector2(900, 44));

        int mins = (int)(elapsed / 60f), secs = (int)(elapsed % 60f);
        StatBox(c.transform, "TEMPO", $"{mins:00}:{secs:00}", -370, -220);
        StatBox(c.transform, "ZUMBIS DERROTADOS", (loot != null ? loot.kills : 0).ToString("N0"), 0, -220);
        StatBox(c.transform, "DANO TOTAL", Mathf.RoundToInt(Enemy.DamageDealt).ToString("N0"), 370, -220);
        // Em derrota o progresso da missao e perdido: mostra o nivel/XP de VOLTA (inicio da missao).
        bool penalize = HeroSquad.FallenAllyPenalizedOnWin;
        bool keepPrimary = win && (primaryAlive || !penalize);
        int shownLevel = keepPrimary ? stats.Level : startLevel;
        int shownXp = keepPrimary ? stats.Xp : startXp;
        string lineText;
        if (squad.Count > 0)
        {
            // Dupla/trio: nivel de cada um + vida somada da equipe.
            var lv = new StringBuilder($"{GameSession.Survivor.name} Nv {shownLevel}");
            foreach (var sp in squad)
            {
                bool keep = win && (IsAlive(sp.stats) || !penalize);
                lv.Append($"  ·  {sp.def.name} Nv {(keep ? sp.stats.Level : sp.startLevel)}");
            }
            lineText = $"Ouro: +{goldGain}      Sucata: +{scrapGain}      {lv}      Vida da equipe: {Mathf.RoundToInt(hpRatio * 100f)}%";
        }
        else lineText = $"Ouro: +{goldGain}      Sucata: +{scrapGain}      Nível {shownLevel}  (XP {shownXp}/{PlayerStats.XpForLevel(shownLevel)})      Vida restante: {Mathf.RoundToInt(hpRatio * 100f)}%";
        var line = UIFactory.Txt(c.transform, lineText, 32, T, UIFactory.Gold, "Line");
        UIFactory.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -358), new Vector2(1640, 44));
        // Trio: tres niveis na mesma linha; encolhe a fonte em vez de quebrar.
        line.enableAutoSizing = true; line.fontSizeMin = 22; line.fontSizeMax = 32;

        var rew = UIFactory.Txt(c.transform, "RECOMPENSAS", 38, T, Color.white, "RewardsTitle");
        UIFactory.Place(rew.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -420), new Vector2(800, 50));

        if (drops.Count > 0)
        {
            float spacing = Mathf.Min(230f, 1620f / Mathf.Max(1, drops.Count));
            for (int i = 0; i < drops.Count; i++)
            {
                float x = (i - (drops.Count - 1) * 0.5f) * spacing;
                ItemCard(c.transform, drops[i], x, -480);
            }
            if (recoveredCount > 0)
            {
                var rec = UIFactory.Txt(c.transform, $"{recoveredCount} item(ns) perdido(s) em derrota anterior foram recuperados.", 24, T, UIFactory.Gold, "Recovered");
                UIFactory.Place(rec.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -745), new Vector2(1400, 34));
            }
        }
        else
        {
            string m = win ? "Nenhum item desta vez."
                : (string.IsNullOrEmpty(penaltyText) ? "Sem itens ao ser derrotado." : penaltyText) + (scrapGain > 0 ? "\nA sucata foi mantida: use na oficina (inventário) para melhorar seus itens." : "")
                  + "\nVença a missão para receber itens.";
            var none = UIFactory.Txt(c.transform, m, 28, T, win ? UIFactory.Gray : new Color(1f, 0.78f, 0.62f), "NoDrops");
            UIFactory.Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -500), new Vector2(1500, 240));
        }

        if (soldCount > 0)
        {
            var ov = UIFactory.Txt(c.transform, $"Mochila cheia: {soldCount} item(ns) vendido(s) automaticamente (+{soldGold} de ouro).", 24, T, UIFactory.Gray, "Overflow");
            UIFactory.Place(ov.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -770), new Vector2(1400, 34));
        }

        string st = GameSession.DebugRun ? "MODO DE TESTE: o progresso NÃO foi salvo (inicie pelo MainMenu)."
                  : saved ? $"Progresso salvo (Slot {GameSession.Save.slot})."
                          : "ATENÇÃO: falha ao salvar o progresso (veja o Console).";
        if (win && !string.IsNullOrEmpty(unlockText) && !GameSession.DebugRun) st += "  " + unlockText;
        if (win && !string.IsNullOrEmpty(fallenNote))
        {
            // Ate 2 linhas (trio: dois podem cair numa vitoria).
            var fallen = UIFactory.Txt(c.transform, fallenNote, 22, T, new Color(1f, 0.78f, 0.62f), "FallenAlly");
            UIFactory.Place(fallen.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 176), new Vector2(1600, 60));
            fallen.alignment = TextAlignmentOptions.Bottom;
        }
        var status = UIFactory.Txt(c.transform, st, 24, T, (GameSession.DebugRun || !saved) ? UIFactory.Red : UIFactory.Gray, "Status");
        UIFactory.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1600, 34));

        var back = UIFactory.Btn(c.transform, "VOLTAR AO MAPA", 28, () =>
        {
            Time.timeScale = 1f;
            GameSession.OpenMapOnLoad = !GameSession.DebugRun;
            SceneManager.LoadScene(GameSession.MenuSceneIndex);
        }, new Color(0.106f, 0.133f, 0.169f, 1f));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(-230, 40), new Vector2(400, 78));
        var backOutline = back.gameObject.AddComponent<Outline>(); backOutline.effectColor = new Color(0.36f, 0.42f, 0.49f, 0.9f); backOutline.effectDistance = new Vector2(1f, -1f);
        TMP_Text backText = back.GetComponentInChildren<TMP_Text>(); if (backText) backText.fontStyle = FontStyles.Bold;

        var retry = UIFactory.Btn(c.transform, win ? "JOGAR NOVAMENTE" : "TENTAR DE NOVO", 28, () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }, new Color(0.588f, 0.075f, 0.09f, 1f));
        UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(230, 40), new Vector2(400, 78));
        var retryOutline = retry.gameObject.AddComponent<Outline>(); retryOutline.effectColor = new Color(0.847f, 0.141f, 0.169f, 0.85f); retryOutline.effectDistance = new Vector2(1f, -1f);
        TMP_Text retryText = retry.GetComponentInChildren<TMP_Text>(); if (retryText) retryText.fontStyle = FontStyles.Bold;

        // Vitoria: reinicia sozinho em alguns segundos (jogador pode deixar o jogo rodando na tela de resultado).
        if (win) SetupAutoReplay(c.transform, retry, retryText);
    }

    // ===================== Reinicio automatico apos vitoria =====================
    const float AutoReplaySeconds = 5f;
    const string AutoReplayPref = "zombit_autoreplay";   // 1 = ligado (padrao), 0 = desligado

    Coroutine autoReplayRoutine;
    Button autoReplayRetry;
    TMP_Text autoReplayRetryText, autoReplayToggleText;
    string autoReplayBaseLabel;

    static bool AutoReplayEnabled => PlayerPrefs.GetInt(AutoReplayPref, 1) == 1;

    void SetupAutoReplay(Transform canvas, Button retry, TMP_Text retryText)
    {
        autoReplayRetry = retry;
        autoReplayRetryText = retryText;
        autoReplayBaseLabel = retryText != null ? retryText.text : "JOGAR NOVAMENTE";

        var toggle = UIFactory.Btn(canvas, "", 22, ToggleAutoReplay, new Color(0.106f, 0.133f, 0.169f, 1f), "AutoReplayToggle");
        UIFactory.Place((RectTransform)toggle.transform, new Vector2(0.5f, 0f), new Vector2(690f, 40f), new Vector2(380f, 78f));
        var outline = toggle.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.36f, 0.42f, 0.49f, 0.9f); outline.effectDistance = new Vector2(1f, -1f);
        autoReplayToggleText = toggle.GetComponentInChildren<TMP_Text>();
        RefreshAutoReplayToggleLabel();

        if (AutoReplayEnabled) autoReplayRoutine = StartCoroutine(AutoReplayCountdown());
    }

    void ToggleAutoReplay()
    {
        bool now = !AutoReplayEnabled;
        PlayerPrefs.SetInt(AutoReplayPref, now ? 1 : 0);
        PlayerPrefs.Save();
        RefreshAutoReplayToggleLabel();

        if (autoReplayRoutine != null) { StopCoroutine(autoReplayRoutine); autoReplayRoutine = null; }
        if (autoReplayRetryText != null) autoReplayRetryText.text = autoReplayBaseLabel;
        if (now) autoReplayRoutine = StartCoroutine(AutoReplayCountdown());
    }

    void RefreshAutoReplayToggleLabel()
    {
        if (autoReplayToggleText != null)
            autoReplayToggleText.text = AutoReplayEnabled ? "REINÍCIO AUTO: LIGADO" : "REINÍCIO AUTO: DESLIGADO";
    }

    // Usa tempo real: a tela de resultado roda com Time.timeScale = 0.
    IEnumerator AutoReplayCountdown()
    {
        float t = AutoReplaySeconds;
        while (t > 0f)
        {
            if (autoReplayRetryText != null)
                autoReplayRetryText.text = autoReplayBaseLabel + " (" + Mathf.CeilToInt(t) + ")";
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        autoReplayRoutine = null;
        if (autoReplayRetry != null) autoReplayRetry.onClick.Invoke();   // mesmo caminho do clique manual
    }
}
