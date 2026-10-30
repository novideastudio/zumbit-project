using System;
using UnityEngine;
public sealed class PcRangeLayout : MonoBehaviour
{
    public RectTransform viewport,content;
    public Action<bool> tick;
    Vector2 last;
    float next;
    public void Apply()
    {
        if(!viewport || !content)return;last=viewport.rect.size;float scale=Mathf.Min(last.x/1920f,last.y/1080f);if(scale<=0)return;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0.5f,0.5f);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(1920,1080);content.localScale=Vector3.one*scale;
    }
    void LateUpdate()
    {
        bool changed=viewport && viewport.rect.size!=last;if(changed)Apply();
        if(changed || Time.unscaledTime>=next){next=Time.unscaledTime+0.15f;tick?.Invoke(changed);}
    }
}
