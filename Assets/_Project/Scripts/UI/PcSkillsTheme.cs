using System;
using UnityEngine;
[Serializable] public class PcSkillArtOverride { public string nodeId; public Sprite icon,banner; }
[Serializable] public class PcBranchArtOverride { public string survivorId; public int branchIndex; public Sprite banner; }
[CreateAssetMenu(menuName="Zombit/UI/PC Skills Theme")]
public sealed class PcSkillsTheme : ScriptableObject
{
    public Sprite panelFrame,cardFrame,selectedFrame,buttonFrame;
    public Sprite logo,defaultIcon,lockedIcon,plusIcon,defaultDetailBanner;
    public Sprite[] branchBanners=new Sprite[3];
    public PcSkillArtOverride[] skills=new PcSkillArtOverride[0];
    public PcBranchArtOverride[] branches=new PcBranchArtOverride[0];
}
