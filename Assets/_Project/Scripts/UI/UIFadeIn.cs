using UnityEngine;

/// <summary>
/// Fade-in curto ao ativar o objeto (telas do menu, resultado da missao, pausa).
/// Usa CanvasGroup + tempo nao escalado. Nao desativa o componente: OnEnable reinicia a cada exibicao.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIFadeIn : MonoBehaviour
{
    public float duration = 0.18f;

    CanvasGroup group;
    float t;
    bool done;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        t = 0f;
        done = duration <= 0f;
        if (group != null) group.alpha = done ? 1f : 0f;
    }

    void Update()
    {
        if (done || group == null) return;
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(t / duration);
        group.alpha = Mathf.SmoothStep(0f, 1f, k);
        if (k >= 1f) { group.alpha = 1f; done = true; }
    }
}
