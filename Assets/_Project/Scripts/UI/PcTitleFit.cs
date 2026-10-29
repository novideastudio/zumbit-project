using UnityEngine;
public sealed class PcTitleFit : MonoBehaviour
{
    public RectTransform viewport,stage;
    Vector2 last;
    void OnEnable()=>Apply();
    void LateUpdate(){if(viewport && viewport.rect.size!=last)Apply();}
    public void Apply()
    {
        if(!viewport || !stage)return;
        last=viewport.rect.size;float k=Mathf.Min(last.x/1672f,last.y/941f);if(k<=0f)return;
        stage.anchorMin=stage.anchorMax=stage.pivot=new Vector2(0.5f,0.5f);stage.anchoredPosition=Vector2.zero;
        stage.sizeDelta=new Vector2(1672f,941f);stage.localScale=Vector3.one*k;
    }
}
