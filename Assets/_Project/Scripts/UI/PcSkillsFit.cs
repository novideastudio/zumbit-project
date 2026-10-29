using System;
using UnityEngine;
using UnityEngine.InputSystem;
public sealed class PcSkillsFit : MonoBehaviour
{
    public RectTransform viewport,content;
    public Action escape;
    Vector2 last;
    void OnEnable()=>Apply();
    void Update(){if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)escape?.Invoke();}
    void LateUpdate(){if(viewport && viewport.rect.size!=last)Apply();}
    public void Apply()
    {
        if(!viewport || !content)return;last=viewport.rect.size;float scale=Mathf.Min(last.x/1920f,last.y/1080f);if(scale<=0)return;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0.5f,0.5f);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(1920,1080);content.localScale=Vector3.one*scale;
    }
}
