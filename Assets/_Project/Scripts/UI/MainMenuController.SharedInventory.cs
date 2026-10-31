using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public partial class MainMenuController
{
    TacticalInventoryView sharedInventoryView;
    void BuildSharedMenuInventory(SaveData save,List<ItemInstance> visible,ItemInstance item,bool equipped)
    {
        float scroll=1f;
        if(pcInvLastCategory==invCategory && pcInvLastSort==invSort && pcInvLastSurvivor==invSurvivorId && screens.TryGetValue("mapInventory",out var old) && old)
            foreach(var s in old.GetComponentsInChildren<ScrollRect>(true))if(s.name=="MapItemsViewport")scroll=s.verticalNormalizedPosition;
        pcInvLastCategory=invCategory;pcInvLastSort=invSort;pcInvLastSurvivor=invSurvivorId;
        var screen=Rebuild("mapInventory");
        sharedInventoryView=TacticalInventoryView.Create(screen,"OURO  "+save.gold.ToString("N0")+"    |    SUCATA  "+save.scrap.ToString("N0"),ReturnFromUtility,"VOLTAR");
        BuildMapEquipmentPanel(sharedInventoryView.Equipment,save,mapInventorySelectedUid);
        BuildMapStatsPanel(sharedInventoryView.Stats,save);
        ReflowPcInventoryStats(sharedInventoryView.Stats,save);
        BuildMapBackpackPanel(sharedInventoryView.Bag,save,visible,mapInventorySelectedUid);
        BuildMapItemDetails(sharedInventoryView.Details,item,equipped,save);
        sharedInventoryView.Finish(invCategory,invSurvivorId,scroll);
        Show("mapInventory");sharedInventoryView.Fit.Apply();
    }
    void BuildSharedMenuEquipment(SaveData save,string selectedUid)
    {
        sharedInventoryView.BuildEquipment(GameDatabase.GetSurvivor(invSurvivorId),GameDatabase.Survivors.Count,
            (p,slot,label,x,y,size)=>CreateMapEquipmentSlot(p,save,slot,label,x,y,size,selectedUid),
            (strip,width)=>{foreach(var def in GameDatabase.Survivors)BuildSurvivorChip(strip,def,save,width);});
    }
}
