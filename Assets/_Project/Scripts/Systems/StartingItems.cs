using UnityEngine;

// Entrega o kit inicial uma unica vez por save (ou sempre no modo de teste).
public class StartingItems : MonoBehaviour
{
    public Inventory inventory;
    public ItemData[] items;

    void Start()
    {
        if (!inventory || items == null) return;
        var save = GameSession.Save;
        if (save != null && save.starterGiven && !GameSession.DebugRun) return;

        foreach (var i in items) if (i) inventory.Add(ItemFactory.Create(i));
        if (save != null) save.starterGiven = true;
    }
}
