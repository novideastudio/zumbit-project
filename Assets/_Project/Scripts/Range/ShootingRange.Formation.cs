using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Only instantiated by ShootingRange. No wave/spawner changes.
public partial class ShootingRange
{
    readonly Dictionary<string, PlayerStats> trainingActors = new Dictionary<string, PlayerStats>();
    readonly List<string> trainingOrder = new List<string>();
    readonly List<GameObject> originalActors = new List<GameObject>();
    readonly List<WeaponData> trainingWeapons = new List<WeaponData>();
    readonly Dictionary<string, Button> trainingChoices = new Dictionary<string, Button>();
    
    Canvas trainingCanvas;
    TMP_Text trainingMessage;
    bool trainingBusy;
    bool trainingInitialized;

    IEnumerator InstallTrainingFormation()
    {
        if (!GameSession.IsRange || GameSession.Save == null) yield break;
        yield return new WaitForSeconds(0.5f);
        var oldMembers = new List<HeroSquad.Member>(HeroSquad.Members);
        var template = hero != null ? hero.GetComponent<Hero>() : null;
        if (template == null || template.weapon == null) yield break;
        trainingBusy = true;
        foreach (var m in oldMembers)
        {
            if (m == null || m.def == null || m.stats == null) continue;
            trainingOrder.Add(m.def.id);
            originalActors.Add(m.stats.gameObject);
        }
        if (trainingOrder.Count == 0) trainingOrder.Add(GameSession.Survivor.id);
        foreach (var obj in originalActors) obj.SetActive(false);
        // Existing turrets are standalone objects, not squad members.
        foreach (var h in FindObjectsByType<Hero>(FindObjectsSortMode.None))
            if (h.GetComponent<PlayerStats>() == null) { h.gameObject.SetActive(false); Destroy(h.gameObject); }
        foreach (var def in GameDatabase.Survivors)
        {
            var go = new GameObject("Training_" + def.id);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Health>();
            var h = go.AddComponent<Hero>();
            var ps = go.AddComponent<PlayerStats>();
            var w = WeaponCatalog.CreateRuntime(def, template.weapon);
            trainingWeapons.Add(w);
            h.weapon = w;
            h.muzzleUV = def.muzzleUV;
            h.muzzleSearchBandUV = 0.12f;
            var p = GameSession.Save.GetProgress(def.id);
            var b = SkillTreeSystem.GetBonuses(def.id, p);
            h.damageMult = def.damageMult * (1f + b.damagePercent);
            h.fireRateMult = def.fireRateMult * (1f + b.fireRatePercent);
            h.rangeMult = def.rangeMult * (1f + b.rangePercent);
            h.accuracyBonus = def.accuracyBonus + b.accuracyBonus;
            h.critChance = Mathf.Clamp01(def.critChance + b.critChance);
            h.critMult = def.critMult * (1f + b.critDamagePercent);
            ps.ApplySurvivor(def);
            ps.ApplySkillBonuses(b);
            ps.ApplyDifficulty(Difficulties.Get(GameSession.Difficulty));
            ps.SetProgress(p.level, p.xp);
            ps.LoadEquipped(p.equipped);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = def.fullBody != null ? def.fullBody : ArtLoader.Get("Art/Characters/" + def.id + "_full");
            if (sr.sprite != null)
            {
                float scale = Mathf.Max(0.1f, def.heroHeight) / SpriteMetrics.WorldHeight(sr.sprite);
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }
            sr.color = Color.white;
            go.AddComponent<PixelCharacterMotion>();
            go.AddComponent<YSort>();
            go.AddComponent<ActiveSkills>().enabled = false;
            h.enabled = false;
            trainingActors[def.id] = ps;
            go.SetActive(true);
        }
        // Allow Awake/Start to initialize Health, ammunition and item affixes.
        yield return null;
        foreach (var ps in trainingActors.Values) ps.gameObject.SetActive(false);
        BuildTrainingFormationUi();
        trainingInitialized = true;
        trainingBusy = false;
        yield return ApplyTrainingFormation();
    }

    void BuildTrainingFormationUi()
    {
        trainingCanvas = UIFactory.MakeCanvas("TrainingFormationCanvas", 45);
        var strip = UIKit.Frame(trainingCanvas.transform, "TrainingFormation", new Color(0.01f,0.025f,0.035f,0.97f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(strip,0.005f,0.005f,0.995f,0.19f);
        var title = UIFactory.Txt(strip,"SOBREVIVENTES: TOQUE PARA ADICIONAR OU REMOVER (1–3)",18,TextAlignmentOptions.Center,UIKit.Col.Gold,"Title");
        UIFactory.Stretch(title.rectTransform,0.01f,0.78f,0.99f,0.98f);
        int n = GameDatabase.Survivors.Count;
        for(int i=0;i<n;i++)
        {
            var def=GameDatabase.Survivors[i];
            string id=def.id;
            var b=UIFactory.Btn(strip,"",18,()=>ToggleTrainingActor(id),null,"Choose_"+id);
            float x=0.01f+i*(0.98f/n);
            UIFactory.Stretch((RectTransform)b.transform,x,0.18f,x+0.98f/n-0.006f,0.75f);
            var icon=UIFactory.Img(b.transform,Color.white,"Portrait");
            icon.sprite=def.portrait != null ? def.portrait : ArtLoader.Get("Art/Characters/"+id+"_portrait");
            icon.preserveAspect=true; icon.raycastTarget=false;
            UIFactory.Stretch(icon.rectTransform,0.02f,0.10f,0.34f,0.90f);
            var text=b.GetComponentInChildren<TMP_Text>();
            text.text=def.name.ToUpperInvariant()+"\nNV. "+GameSession.Save.GetProgress(id).level+" | P "+PowerRating.Of(GameSession.Save,id);
            text.fontSize=16; text.enableAutoSizing=true; text.fontSizeMin=10; text.fontSizeMax=16;
            text.raycastTarget=false;
            UIFactory.Stretch(text.rectTransform,0.35f,0.05f,0.99f,0.95f);
            trainingChoices[id]=b;
        }
        trainingMessage=UIFactory.Txt(strip,"",15,TextAlignmentOptions.Center,UIKit.Col.TextDim,"Hint");
        UIFactory.Stretch(trainingMessage.rectTransform,0.01f,0.01f,0.99f,0.17f);
        RefreshTrainingFormationUi();
        ApplyPcRangeFormation();
    }
    void RefreshTrainingFormationUi()
    {
        foreach(var kv in trainingChoices)
            UIFactory.SetButtonColor(kv.Value,trainingOrder.Contains(kv.Key)?new Color(0.15f,0.35f,0.25f):new Color(0.06f,0.09f,0.12f));

        if(trainingMessage) trainingMessage.text="Selecione de 1 a 3 sobreviventes. Cada alteração zera o DPS. Não altera a formação da campanha.";
        RefreshPcRangeFormation();
    }
    void ToggleTrainingActor(string id)
    {
        if(trainingBusy) return;
        if(trainingOrder.Contains(id))
        {
            if(trainingOrder.Count==1) { trainingMessage.text="Mantenha pelo menos um sobrevivente."; return; }
            trainingOrder.Remove(id);
        }
        else
        {
            if(trainingOrder.Count>=3) { trainingMessage.text="Máximo de três. Remova um antes de adicionar outro."; return; }
            trainingOrder.Add(id);
        }
        StartCoroutine(ApplyTrainingFormation());
    }
    public bool CanDragTrainingSlot(int index) => !trainingBusy && index>=0 && index<trainingOrder.Count;
    public void MoveTrainingSlot(int from,int to)
    {
        if(trainingBusy || from<0 || from>=trainingOrder.Count || to<0 || to>2 || from==to) return;
        if(to>=trainingOrder.Count) to=trainingOrder.Count-1;
        string id=trainingOrder[from];
        trainingOrder[from]=trainingOrder[to]; trainingOrder[to]=id;
        StartCoroutine(ApplyTrainingFormation());
    }
    IEnumerator ApplyTrainingFormation()
    {
        trainingBusy=true;
        // Preserve any equipment changes made inside the range before changing inventory owner.
        var inventory=FindFirstObjectByType<Inventory>();
        if(inventory != null && inventory.stats != null && GameSession.Survivor != null)
            inventory.SaveTo(GameSession.Save,GameSession.Survivor.id,inventory.stats);
        foreach(var m in HeroSquad.Members)
            if(m != null && m.stats != null && m.def != null)
                GameSession.Save.GetProgress(m.def.id).equipped=m.stats.EquippedList();
        foreach(var ps in trainingActors.Values)
        {
            ps.GetComponent<Hero>().enabled=false;
            var ab=ps.GetComponent<SurvivorAbilities>();
            if(ab != null) { ab.CleanupTrainingObjects(); ab.enabled=false; Destroy(ab); }
            ps.gameObject.SetActive(false);
        }
        foreach(var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) { p.gameObject.SetActive(false); Destroy(p.gameObject); }
        HeroSquad.Clear();
        yield return null;
        int count=trainingOrder.Count;
        for(int i=0;i<count;i++)
        {
            var ps=trainingActors[trainingOrder[i]];
            var def=GameDatabase.GetSurvivor(trainingOrder[i]);
            var p=GameSession.Save.GetProgress(def.id);
            ps.isPartner=i>0;ps.supportSlot=i;
            ps.transform.position=new Vector3(HeroSquad.FrontXFor(count)-HeroSquad.OffsetXFor(count)*i,GameTuning.Current.HeroFeetY(i),0f);
            var sr=ps.GetComponent<SpriteRenderer>();
            if(sr != null && sr.sprite != null)
            {
                float feet=sr.bounds.min.y+SpriteMetrics.Get(sr.sprite).bottomPx/sr.sprite.pixelsPerUnit*Mathf.Abs(ps.transform.localScale.y);
                ps.transform.position+=Vector3.up*(GameTuning.Current.HeroFeetY(i)-feet);
            }
            var motion=ps.GetComponent<PixelCharacterMotion>();
            // Training actors retain their procedural motion state between selections.
            ps.gameObject.SetActive(true);
            ps.GetComponent<ActiveSkills>().enabled=i==0;
            HeroSquad.Register(ps,def,i==0,p.level,p.xp);
        }
        GameSession.Survivor=GameDatabase.GetSurvivor(trainingOrder[0]);
        GameSession.Partner=count>1?GameDatabase.GetSurvivor(trainingOrder[1]):null;
        GameSession.Third=count>2?GameDatabase.GetSurvivor(trainingOrder[2]):null;
        hero=trainingActors[trainingOrder[0]].transform; squadSize=count;
        if(inventory != null) { inventory.stats=trainingActors[trainingOrder[0]]; inventory.LoadFrom(GameSession.Save,trainingOrder[0],inventory.stats); }
        var flow=FindFirstObjectByType<MissionFlow>();
        if(flow != null)
        {
            flow.stats=trainingActors[trainingOrder[0]];flow.supports.Clear();
            for(int i=1;i<count;i++) flow.supports.Add(trainingActors[trainingOrder[i]]);
        }
        var hud=FindFirstObjectByType<HudController>();
        if(hud != null) { hud.hero=hero.GetComponent<Hero>();hud.stats=hero.GetComponent<PlayerStats>();hud.heroHealth=hero.GetComponent<Health>(); }
        laneY=GetVisibleFeetY(hero,GameTuning.Current.HeroFeetY(0));
        RestoreRangeCamera();SetupRangeCamera();SpawnDummies();
        foreach(var id in trainingOrder)
        {
            var ps=trainingActors[id];
            var def=GameDatabase.GetSurvivor(id);
            ps.gameObject.AddComponent<SurvivorAbilities>().Init(def,SkillTreeSystem.GetBonuses(id,GameSession.Save.GetProgress(id)));
            ps.GetComponent<Hero>().focusTarget=null;
            ps.GetComponent<Hero>().enabled=true;
        }
        RefreshTrainingFormationUi();trainingBusy=false;
    }
    void DisposeTrainingFormation()
    {
        if(trainingCanvas) Destroy(trainingCanvas.gameObject);
        foreach(var ps in trainingActors.Values) if(ps) Destroy(ps.gameObject);
        foreach(var w in trainingWeapons) if(w) Destroy(w);
    }
}
