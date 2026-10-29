using UnityEngine;
public sealed class CampaignPcFit : MonoBehaviour
{
    public RectTransform content,viewport;
    Vector2 last;
    void OnEnable()=>Apply();
    void LateUpdate(){if(viewport && viewport.rect.size!=last)Apply();}
    public void Apply()
    {
        if(!content || !viewport)return;last=viewport.rect.size;
        float k=Mathf.Min(last.x/1920f,last.y/1080f);if(k<=0f)return;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0.5f,0.5f);content.anchoredPosition=Vector2.zero;
        content.sizeDelta=new Vector2(1920,1080);content.localScale=Vector3.one*k;
    }
}
