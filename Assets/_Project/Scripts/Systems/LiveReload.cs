using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Ferramenta de desenvolvimento (F5 recarrega o menu). Fica fora das builds de lancamento.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class LiveReload : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<LiveReload>() != null) return;
        var go = new GameObject("LiveReload");
        DontDestroyOnLoad(go);
        go.AddComponent<LiveReload>();
    }

    void Update()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetKeyDown(KeyCode.F5);
#endif
        if (!pressed) return;

        if (FindFirstObjectByType<MainMenuController>() == null)
        {
            Debug.Log("F5 recarrega a interface apenas no menu.");
            return;
        }
        if (GameObject.Find("map") != null && GameSession.Save != null) GameSession.OpenMapOnLoad = true;

        GameDatabase.Reload();
        UITheme.Reload();
        GameTuning.Reload();
        ArtLoader.Clear();
        UIFactory.ResetCache();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Debug.Log("Interface recarregada (tema, sobreviventes e arte).");
    }
}
#endif
