using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class MissionInventoryOverlay
{
    TacticalInventoryView sharedInventoryView;
    int sharedLastCategory=int.MinValue;
    InvSort sharedLastSort;
    PlayerStats sharedLastStats;
    float sharedNextRefresh;

    string SharedMissionMoney()=>"OURO DA MISSÃO  "+(inv?inv.Gold:0).ToString("N0")+"    |    SUCATA  "+(Save!=null?Save.scrap:0).ToString("N0");
    void BuildSharedMissionInventory(List<ItemInstance> visible,bool equipped)
    {
        if(!stats){Toast("Nenhum sobrevivente disponível.");Close();return;}
        sharedInventoryView=TacticalInventoryView.Create(screen,SharedMissionMoney(),Close,"FECHAR  [I]");
        BuildEquipmentPanel(sharedInventoryView.Equipment);
        BuildStatsPanel(sharedInventoryView.Stats);
        sharedInventoryView.ArrangeStatistics(Def(),Save,true);
        BuildBackpackPanel(sharedInventoryView.Bag,visible);
        BuildItemDetails(sharedInventoryView.Details,selected,equipped);
        sharedInventoryView.Finish(category,Def()!=null?Def().id:null,sharedRestoreScroll);
        sharedLastCategory=category;sharedLastSort=sort;sharedLastStats=stats;
        RefreshSharedLiveInventory(true);
        if(toast)toast.transform.SetAsLastSibling();
    }
    float sharedRestoreScroll=1f;
    void CaptureSharedInventoryScroll()
    {
        sharedRestoreScroll=1f;
        if(sharedLastCategory!=category || sharedLastSort!=sort || sharedLastStats!=stats || !screen)return;
        foreach(var s in screen.GetComponentsInChildren<ScrollRect>(true))if(s.name=="MapItemsViewport")sharedRestoreScroll=s.verticalNormalizedPosition;
    }
    void BuildSharedMissionEquipment()
    {
        var members=new List<PlayerStats>();
        foreach(var member in HeroSquad.Members)if(member!=null && member.stats)members.Add(member.stats);
        if(members.Count==0 && stats)members.Add(stats);
        sharedInventoryView.BuildEquipment(Def(),members.Count,
            (parent,slot,label,x,y,size)=>CreateEquipmentSlot(parent,slot,label,x,y,size),
            (strip,width)=>{foreach(var ps in members)BuildSurvivorChip(strip,ps,width);});
    }
    void RefreshSharedLiveInventory(bool force=false)
    {
        if(sharedInventoryView==null || (!force && Time.unscaledTime<sharedNextRefresh))return;
        sharedNextRefresh=Time.unscaledTime+0.25f;
        sharedInventoryView.SetMoney(SharedMissionMoney());
        sharedInventoryView.RefreshLiveStats(stats,Def(),Save);
    }
}
