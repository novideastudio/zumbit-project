using UnityEngine;
[CreateAssetMenu(menuName = "Zombit/Item")]
public class ItemData : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public ItemSlot slot;
    public Rarity rarity;
    public float bonusDamage, bonusHealth, bonusDefense;
    public int sellValue = 10;

    [Header("Conteudo do Pesadelo")]
    [Tooltip("So aparece como recompensa no nivel Pesadelo.")]
    public bool nightmareOnly;
    [Tooltip("Afixo fixo (assinatura) do item, alem dos aleatorios.")]
    public bool hasSignature;
    public AffixType signatureAffix;
    public float signatureValue;
}
