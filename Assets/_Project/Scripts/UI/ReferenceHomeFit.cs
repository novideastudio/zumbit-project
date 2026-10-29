using UnityEngine;
public sealed class ReferenceHomeFit : MonoBehaviour
{
    public RectTransform content,viewport;
    Vector2 lastSize;
    void OnEnable() { Apply(); }
    void LateUpdate() { if(viewport && viewport.rect.size!=lastSize)Apply(); }
    public void Apply()
    {
        if(!content || !viewport)return;
        lastSize=viewport.rect.size;
        float scale=Mathf.Min(lastSize.x/1672f,lastSize.y/941f);
        if(scale<=0f)return;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0.5f,0.5f);
        content.sizeDelta=new Vector2(1672f,941f);content.anchoredPosition=Vector2.zero;
        content.localScale=Vector3.one*scale;
    }
}
