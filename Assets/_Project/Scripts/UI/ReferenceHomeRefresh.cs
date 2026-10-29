using System;
using UnityEngine;
using UnityEngine.InputSystem;
public sealed class ReferenceHomeRefresh : MonoBehaviour
{
    public Action refresh,escape;
    float next;
    void Update()
    {
        if(Time.unscaledTime>=next) {next=Time.unscaledTime+0.25f;refresh?.Invoke();}
        var k=Keyboard.current;
        if(k!=null && k.escapeKey.wasPressedThisFrame)escape?.Invoke();
    }
}
