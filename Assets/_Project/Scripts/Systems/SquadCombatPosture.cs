using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Opt-in wave-only squad movement. Does not change Hero shooting or any spawner.
public sealed class SquadCombatPosture : MonoBehaviour
{
    // Advance is determined by weapon range, not a fixed displacement.
    public float advanceSpeed = 1.0f;
    public float retreatSpeed = 1.5f;
    public float contactSafety = 2.2f;
    public float safeRelease = 2.9f;
    public float formationSpacing = 0.65f;
    public float rangeMargin = 0.65f;
    public float decisionInterval = 0.10f;

    sealed class Actor
    {
        public HeroSquad.Member member;
        public float homeX;
        public bool retreating;
        public PixelCharacterMotion motion;
        public float goal;
    }
    readonly List<Actor> actors = new List<Actor>();
    Canvas postureCanvas;
    Button toggle;
    TMP_Text label;
    bool offensive;
    public bool IsOffensive => offensive;
    float nextDecision;
    MissionFlow flow;
    bool hasStarted;

    System.Collections.IEnumerator Start()
    {
        if (GameSession.IsRange) { Destroy(this); yield break; }
        // MissionSetup has configured and registered the squad in Awake.
        yield return null;
        foreach (var m in HeroSquad.Members)
        {
            if (m == null || m.hero == null || m.stats == null) continue;
            actors.Add(new Actor { member=m, homeX=m.hero.transform.position.x,
                goal=m.hero.transform.position.x, motion=m.hero.GetComponent<PixelCharacterMotion>() });
        }
        if (actors.Count==0) { Destroy(this); yield break; }
        flow=FindFirstObjectByType<MissionFlow>();
        BuildUi();
        hasStarted=true;
    }
    void BuildUi()
    {
        postureCanvas=UIFactory.MakeCanvas("CombatPostureCanvas",35);
        toggle=UIFactory.Btn(postureCanvas.transform,"",20,Toggle,
            new Color(0.06f,0.10f,0.13f,0.95f),"CombatPostureToggle");
        // Lower right, away from left squad cards and centre shock button.
        UIFactory.Place((RectTransform)toggle.transform,new Vector2(1f,0f),
            new Vector2(-18f,18f),new Vector2(330f,86f));
        label=toggle.GetComponentInChildren<TMP_Text>(true);
        if(label != null) { label.enableAutoSizing=true;label.fontSizeMin=14f;label.fontSizeMax=22f; }
        RefreshLabel();
    }
    void Toggle()
    {
        if(Time.timeScale<=0f || (flow != null && flow.IsFinished)) return;
        offensive=!offensive;
        foreach(var a in actors) a.retreating=false;
        nextDecision=0f;
        ZombitAudio.PlayUI();RefreshLabel();
    }
    void RefreshLabel()
    {
        if(label != null) label.text=offensive ? "POSTURA: OFENSIVA\n<size=65%>Clique / toque para manter posição</size>" : "POSTURA: DEFENSIVA\n<size=65%>Clique / toque para avançar</size>";
        if(toggle != null) UIFactory.SetButtonColor(toggle,offensive?new Color(0.40f,0.10f,0.08f,0.96f):new Color(0.06f,0.10f,0.13f,0.95f));
    }
    void Update()
    {
        if(!hasStarted || GameSession.IsRange || Time.timeScale<=0f) return;
        if(flow != null && flow.IsFinished)
        {
            if(postureCanvas) postureCanvas.gameObject.SetActive(false);
            foreach(var a in actors) if(a.motion) a.motion.SetMoving(false);
            return;
        }
        if(Time.time>=nextDecision)
        {
            nextDecision=Time.time+Mathf.Max(0.03f,decisionInterval);
            Decide();
        }
        float aheadX=float.PositiveInfinity;
        foreach(var a in actors)
        {
            if(a.member.hero == null || !a.member.Alive) { if(a.motion) a.motion.SetMoving(false);continue; }
            Transform t=a.member.hero.transform;
            float goal=Mathf.Max(a.homeX,a.goal);
            if(!float.IsPositiveInfinity(aheadX)) goal=Mathf.Min(goal,aheadX-formationSpacing);
            // Never move behind the actor's original home position.
            goal=Mathf.Max(a.homeX,goal);
            float before=t.position.x;
            float speed=goal<before?retreatSpeed:advanceSpeed;
            Vector3 p=t.position;
            p.x=Mathf.MoveTowards(before,goal,Mathf.Max(0f,speed)*Time.deltaTime);
            t.position=p; // preserve procedural bob and lane Y
            if(a.motion) a.motion.SetMoving(Mathf.Abs(p.x-before)>0.0001f);
            aheadX=p.x;
        }
    }
    void Decide()
    {
        bool groupRetreat=false;
        foreach(var a in actors)
        {
            if(a.member.hero==null || !a.member.Alive) continue;
            float nearest=float.PositiveInfinity;
            foreach(var e in Enemy.Alive)
            {
                if(e==null || e.IsDead || e.isTrainingDummy) continue;
                nearest=Mathf.Min(nearest,Mathf.Abs(e.transform.position.x-a.member.hero.transform.position.x));
            }
            if(nearest<contactSafety) a.retreating=true;
            else if(nearest>safeRelease) a.retreating=false;
            groupRetreat |= a.retreating;
        }
        foreach(var a in actors)
        {
            if(a.member.hero==null || !a.member.Alive) continue;
            a.goal=a.homeX;
            if(!offensive || groupRetreat) continue;
            var h=a.member.hero;
            if(h.weapon==null) continue;
            Enemy nearest=null;
            float best=float.PositiveInfinity;
            foreach(var e in Enemy.Alive)
            {
                if(e==null || e.IsDead || e.isTrainingDummy) continue;
                float dx=e.transform.position.x-h.transform.position.x;
                // No chasing enemies behind the formation.
                if(dx<0f) continue;
                if(dx<best) { best=dx;nearest=e; }
            }
            if(nearest==null) continue;
            float reach=h.weapon.range*h.rangeMult*(1f+h.affixRange);
            float dy=nearest.transform.position.y-h.transform.position.y;
            float horizontal=Mathf.Sqrt(Mathf.Max(0f,reach*reach-dy*dy));
            float desired=nearest.transform.position.x-Mathf.Max(0.5f,horizontal-rangeMargin);
            // While in reach, hold until safety retreat: no oscillation along weapon boundary.
            if(best<=Mathf.Max(0.5f,horizontal-rangeMargin)) desired=h.transform.position.x;
            a.goal=Mathf.Max(a.homeX,desired);
        }
    }
    /// <summary>Applies a new front-to-back order and seat X positions. Returns false if members are not tracked.</summary>
    public bool ApplySeatOrder(IList<HeroSquad.Member> frontToBack, IList<float> homeXs)
    {
        if (frontToBack == null || homeXs == null || frontToBack.Count != homeXs.Count || frontToBack.Count != actors.Count) return false;
        var sorted = new List<Actor>(actors.Count);
        for (int i = 0; i < frontToBack.Count; i++)
        {
            Actor found = null;
            for (int k = 0; k < actors.Count; k++)
                if (actors[k].member == frontToBack[i]) { found = actors[k]; break; }
            if (found == null) return false;
            sorted.Add(found);
        }
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].homeX = homeXs[i];
            sorted[i].goal = homeXs[i];
            sorted[i].retreating = false;
        }
        actors.Clear();
        actors.AddRange(sorted);
        return true;
    }
    void OnDestroy() { if(postureCanvas) Destroy(postureCanvas.gameObject); }
}
