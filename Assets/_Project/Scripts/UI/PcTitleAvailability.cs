using System;
using UnityEngine;
public sealed class PcTitleAvailability : MonoBehaviour
{
    public Action refresh;
    float next;
    void OnEnable(){next=0f;}
    void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+2f;refresh?.Invoke();}
}
