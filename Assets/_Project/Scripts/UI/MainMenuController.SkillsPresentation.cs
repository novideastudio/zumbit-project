using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation-only layout for the 18-node / six-rank skill screen.
public partial class MainMenuController
{
    static Transform SkillUiChild(Transform root, string name)
    {
        if (root == null) return null;
        for (int i=0;i<root.childCount;i++)
        {
            var c=root.GetChild(i);
            if(c.name==name) return c;
            var found=SkillUiChild(c,name);
            if(found!=null) return found;
        }
        return null;
    }

    static void SkillUiRect(Transform t,float x,float y,float w,float h)
    {
        var rt=t as RectTransform;
        if(rt==null) return;
        rt.localScale=Vector3.one;
        rt.localRotation=Quaternion.identity;
        rt.anchorMin=new Vector2(x,1f-y-h);
        rt.anchorMax=new Vector2(x+w,1f-y);
        rt.pivot=new Vector2(0.5f,0.5f);
        rt.offsetMin=Vector2.zero;
        rt.offsetMax=Vector2.zero;
    }

    static void SkillUiText(Transform t,float min,float max,bool wrap=false)
    {
        if(t==null) return;
        var text=t.GetComponent<TMP_Text>();
        if(text==null) return;
        text.enableAutoSizing=true;
        text.fontSizeMin=min;text.fontSizeMax=max;text.fontSize=max;
        text.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
        text.overflowMode=TextOverflowModes.Truncate;
        text.raycastTarget=false;
        text.margin=new Vector4(2f,0f,2f,0f);
    }

    static void SkillUiPlace(Transform root,string name,float x,float y,float w,float h,float min=10f,float max=18f,bool wrap=false)
    {
        var t=SkillUiChild(root,name);
        SkillUiRect(t,x,y,w,h);SkillUiText(t,min,max,wrap);
    }

    void FixSkillScreenPresentation(RectTransform screen)
    {
        // Run after Show(), so the layout operates on the final visible screen geometry.
        Canvas.ForceUpdateCanvases();
        for(int branch=0;branch<3;branch++)
        {
            var p=screen.Find("SkillBranch_"+branch);
            if(p==null) continue;
            SkillUiRect(p,0.01f+branch*0.21f,0.12f,0.202f,0.85f);
            SkillUiPlace(p,"BranchBand",0.02f,0.01f,0.96f,0.10f);
            SkillUiPlace(p,"EmblemRing",0.04f,0.024f,0.14f,0.065f);
            SkillUiPlace(p,"EmblemDisc",0.045f,0.028f,0.13f,0.057f);
            SkillUiPlace(p,"EmblemIcon",0.06f,0.035f,0.10f,0.045f);
            SkillUiPlace(p,"BranchTitle",0.23f,0.018f,0.73f,0.042f,12f,24f);
            SkillUiPlace(p,"BranchProgress",0.23f,0.062f,0.73f,0.025f,10f,14f);
            SkillUiPlace(p,"BranchBarTrack",0.23f,0.095f,0.72f,0.007f);
            SkillUiPlace(p,"BranchBarFill",0.23f,0.095f,0.72f,0.007f);
            // Preserve actual progress fill instead of displaying a full bar.
            var progress=SkillUiChild(p,"BranchProgress");
            var track=SkillUiChild(p,"BranchBarTrack") as RectTransform;
            var fill=SkillUiChild(p,"BranchBarFill") as RectTransform;
            var survivor=GameDatabase.GetSurvivor(SkillsSurvivorId(GameSession.Save));
            if(track!=null && fill!=null && survivor!=null)
            {
                var prog=GameSession.Save.GetProgress(survivor.id);
                int ranks=0,total=0;
                foreach(var n in SkillTreeSystem.GetNodes(survivor.id))
                    if(n.branchIndex==branch) { ranks+=SkillTreeSystem.GetRank(prog,n.id);total+=SkillTreeSystem.MaxRank; }
                SkillUiRect(fill,0.23f,0.095f,0.72f*(total>0?(float)ranks/total:0f),0.007f);
            }
            for(int i=0;i<p.childCount;i++)
            {
                var card=p.GetChild(i);
                if(card.name=="SelectedGlow") { card.gameObject.SetActive(false); continue; }
                if(!card.name.StartsWith("Skill_",StringComparison.Ordinal)) continue;
                int cut=card.name.LastIndexOf('_');
                if(cut<0 || !int.TryParse(card.name.Substring(cut+1),out int tier)) continue;
                SkillUiRect(card,0.035f,0.125f+(tier-1)*0.143f,0.93f,0.132f);
                var fx=card.GetComponent<UIButtonFx>();if(fx!=null) fx.enabled=false;
                card.localScale=Vector3.one;
                SkillUiPlace(card,"IconFrame",0.035f,0.16f,0.19f,0.68f);
                // SkillIcon belongs to IconFrame, not directly to the card.
                var iconFrame=card.Find("IconFrame");
                SkillUiPlace(iconFrame,"SkillIcon",0.12f,0.12f,0.76f,0.76f);
                SkillUiPlace(card,"SkillName",0.255f,0.10f,0.71f,0.28f,11f,21f);
                SkillUiPlace(card,"SkillRank",0.255f,0.41f,0.71f,0.20f,9f,14f);
                for(int rank=0;rank<SkillTreeSystem.MaxRank;rank++)
                    SkillUiPlace(card,"RankPip_"+rank,0.255f+rank*0.113f,0.70f,0.10f,0.065f);
                SkillUiPlace(card,"StateGlyph",0.925f,0.83f,0.045f,0.12f);
            }
        }
        var detail=screen.Find("SelectedSkillPanel");
        if(detail!=null)
        {
            SkillUiRect(detail,0.645f,0.12f,0.345f,0.85f);
            SkillUiPlace(detail,"SpecBox",0.025f,0.015f,0.95f,0.20f);
            SkillUiPlace(detail,"SkillTopLine",0.03f,0.226f,0.94f,0.002f);
            SkillUiPlace(detail,"DetailIconFrame",0.03f,0.24f,0.13f,0.085f);
            SkillUiPlace(SkillUiChild(detail,"DetailIconFrame"),"DetailIcon",0.12f,0.12f,0.76f,0.76f);
            SkillUiPlace(detail,"SkillDetailName",0.19f,0.24f,0.78f,0.045f,14f,28f);
            SkillUiPlace(detail,"SkillDetailMeta",0.19f,0.29f,0.78f,0.028f,9f,13f);
            SkillUiPlace(detail,"SkillCurrentRank",0.19f,0.325f,0.78f,0.028f,10f,15f);
            SkillUiPlace(detail,"RanksLabel",0.035f,0.375f,0.29f,0.027f,9f,13f);
            SkillUiPlace(detail,"RanksBonusLabel",0.34f,0.375f,0.48f,0.027f,9f,13f);
            SkillUiPlace(detail,"RanksLevelLabel",0.84f,0.375f,0.125f,0.027f,9f,13f);
            for(int r=1;r<=SkillTreeSystem.MaxRank;r++)
            {
                float y=0.41f+(r-1)*0.041f;
                SkillUiPlace(detail,"RankRow_"+r,0.025f,y,0.95f,0.037f);
                SkillUiPlace(detail,"RankName_"+r,0.035f,y,0.29f,0.037f,9f,15f);
                SkillUiPlace(detail,"RankBonus_"+r,0.34f,y,0.48f,0.037f,9f,15f);
                SkillUiPlace(detail,"RankLevel_"+r,0.84f,y,0.125f,0.037f,9f,14f);
            }
            // Mark and text are siblings, so both must be moved.
            string[] req={"ReqLevel","ReqPrevious","ReqPoints"};
            for(int i=0;i<req.Length;i++)
            {
                float y=0.68f+i*0.042f;
                SkillUiPlace(detail,req[i]+"Mark",0.035f,y+0.005f,0.035f,0.025f);
                SkillUiPlace(detail,req[i],0.085f,y,0.88f,0.035f,10f,16f);
            }
            SkillUiPlace(detail,"MaxedText",0.035f,0.70f,0.93f,0.05f,12f,18f);
            SkillUiPlace(detail,"UnlockSkillButton",0.025f,0.823f,0.95f,0.06f);
            SkillUiPlace(detail,"SkillLockedStatus",0.025f,0.823f,0.95f,0.06f);
            SkillUiPlace(detail,"SkillMaxedStatus",0.025f,0.823f,0.95f,0.06f);
            SkillUiPlace(detail,"ResetSkillsButton",0.025f,0.90f,0.95f,0.05f);
            SkillUiPlace(detail,"ResetHint",0.035f,0.955f,0.93f,0.037f,8f,11f,true);
            var spec=SkillUiChild(detail,"SpecBox");
            SkillUiPlace(spec,"SpecCaption",0.025f,0.025f,0.95f,0.13f,10f,13f);
            SkillUiPlace(spec,"SpecHint",0.025f,0.02f,0.95f,0.13f,8f,10f);
            SkillUiPlace(spec,"SpecName",0.025f,0.18f,0.95f,0.20f,12f,23f);
            SkillUiPlace(spec,"SpecDesc",0.025f,0.40f,0.95f,0.23f,9f,14f,true);
            SkillUiPlace(spec,"SpecEffects",0.025f,0.66f,0.95f,0.30f,9f,13f,true);
            SkillUiPlace(spec,"SpecLocked",0.025f,0.20f,0.95f,0.22f,12f,20f);
            SkillUiPlace(spec,"SpecPreview",0.025f,0.46f,0.95f,0.38f,10f,15f,true);
            SkillUiPlace(spec,"SpecLock",0.92f,0.80f,0.04f,0.15f);
            for(int i=0;i<2;i++)
            {
                var c=SkillUiChild(spec,"SpecCard_"+i);
                SkillUiRect(c,0.025f+i*0.48f,0.20f,0.46f,0.76f);
                SkillUiPlace(c,"SpecCardName",0.035f,0.025f,0.93f,0.22f,9f,15f);
                SkillUiPlace(c,"SpecCardEffects",0.035f,0.27f,0.93f,0.70f,9f,12f,true);
            }
        }
        var legend=screen.Find("SkillLegend");
        if(legend!=null) legend.gameObject.SetActive(false); // state is already shown on every card
        // No missing healing icon, even if a differently named theme asset is used.
        foreach(var image in screen.GetComponentsInChildren<Image>(true))
            if(image.name=="SkillIcon" || image.name=="EmblemIcon" || image.name=="DetailIcon") image.raycastTarget=false;
        Canvas.ForceUpdateCanvases();
    }
}
