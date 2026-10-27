using UnityEngine;
public class MissionController : MonoBehaviour
{
    public Health heroHealth;

    void OnEnable()
    {
        heroHealth.Died += OnHeroDied;
        GameEvents.MissionComplete += OnComplete;
    }

    void OnDisable()
    {
        heroHealth.Died -= OnHeroDied;
        GameEvents.MissionComplete -= OnComplete;
    }

    // Com o esquadrao registrado (sempre, via MissionSetup) quem decide a derrota e o HeroSquad:
    // no modo dupla a missao so acaba quando os DOIS caem.
    void OnHeroDied()
    {
        if (HeroSquad.Count > 0) return;
        GameEvents.MissionFailed?.Invoke(); Time.timeScale = 0f;
    }
    void OnComplete() { Debug.Log("MISSAO CONCLUIDA"); }
    void OnDestroy() { Time.timeScale = 1f; }
}
