using UnityEngine;
using TMPro;
[CreateAssetMenu(menuName="Zombit/UI/PC Training Theme")]
public sealed class PcRangeTheme : ScriptableObject
{
    public Sprite panel,red,green,gold,selected,logo,coin,scrap,targetIcon,targetPreview;
    public TMP_FontAsset font;
    [Header("Optional CLEAN world background - no HUD, actors or target baked in")]
    public Sprite worldBackground;
    public int backgroundSortingOrder=-900;
    [Header("Training camera only")]
    public bool composeCamera=true;
    public float cameraWidth=28f;
    [Range(0.05f,0.4f)]public float heroScreenX=0.17f;
    [Range(0.15f,0.45f)]public float feetScreenY=0.225f;
}
