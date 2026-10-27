using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Atalho da mochila dentro da wave ([I] e botao MOCHILA do HUD).
/// Abre a MissionInventoryOverlay, que usa o mesmo layout do inventario do mapa.
/// O antigo InventoryPanel da cena (layout proprio, com textos vazando) fica desativado e e removido ao iniciar.
/// </summary>
public class PanelToggle : MonoBehaviour
{
    [Tooltip("LEGADO: painel antigo da cena. E desativado e destruido ao iniciar; a mochila agora e a MissionInventoryOverlay.")]
    public GameObject panel;
    public bool startHidden = true;

    /// <summary>Usado pelo atalho MOCHILA do HUD.</summary>
    public static PanelToggle Instance { get; private set; }
    public bool IsOpen => MissionInventoryOverlay.IsOpen;

    void Awake()
    {
        Instance = this;
        if (panel)
        {
            panel.SetActive(false);
            Destroy(panel);
            panel = null;
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Toggle() => MissionInventoryOverlay.Toggle();

    void Update()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetKeyDown(KeyCode.I);
#endif
        if (pressed) Toggle();
    }
}
