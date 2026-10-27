using UnityEngine;

/// <summary>
/// Boneco do inventario (mapa e wave). Cada sobrevivente pode ter o proprio sprite em
/// Resources/Art/Characters/{id}_mannequin.png (ex.: medic_mannequin.png, sniper_mannequin.png).
/// Sem o arquivo, usa o boneco generico inventory_mannequin.png (e, em ultimo caso, o retrato do sobrevivente).
/// </summary>
public static class InventoryMannequin
{
    public const string GenericPath = "Art/Characters/inventory_mannequin";

    public static Sprite Get(SurvivorDef def)
    {
        Sprite sp = null;
        if (def != null && !string.IsNullOrEmpty(def.id))
            sp = ArtLoader.Get("Art/Characters/" + def.id + "_mannequin");
        if (sp == null) sp = ArtLoader.Get(GenericPath);
        return sp;
    }
}
