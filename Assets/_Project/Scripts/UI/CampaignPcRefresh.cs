using System;
using UnityEngine;
using UnityEngine.InputSystem;
public sealed class CampaignPcRefresh : MonoBehaviour
{
    public Action refresh,escape;
    float next;
    void Update()
    {
        if(Time.unscaledTime>=next){next=Time.unscaledTime+0.2f;refresh?.Invoke();}
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)escape?.Invoke();
    }
}
