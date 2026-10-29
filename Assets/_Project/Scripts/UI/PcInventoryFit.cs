using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public sealed class PcInventoryFit : MonoBehaviour
{
    public RectTransform viewport,content;
    public ScrollRect bag;
    public float normalized=1f;
    public Action escape;
    Vector2 last;
    void OnEnable()=>Apply();
    IEnumerator Start(){yield return null;Canvas.ForceUpdateCanvases();if(bag)bag.verticalNormalizedPosition=Mathf.Clamp01(normalized);}
    void Update(){if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)escape?.Invoke();}
    void LateUpdate(){if(viewport && viewport.rect.size!=last)Apply();}
    public void Apply()
    {
        if(!viewport || !content)return;last=viewport.rect.size;float scale=Mathf.Min(last.x/2048f,last.y/1152f);if(scale<=0)return;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0.5f,0.5f);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(2048,1152);content.localScale=Vector3.one*scale;
    }
}
