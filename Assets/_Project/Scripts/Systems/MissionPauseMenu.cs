using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>ESC pause overlay, with an explicit confirmation before returning to the map.</summary>
public class MissionPauseMenu : MonoBehaviour
{
    public MissionFlow flow;
    Canvas pauseCanvas;
    bool isOpen;
    bool confirmingExit;
    bool editorUiBaking;   // true so enquanto o UIBaker gera os prefabs

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (SquadSkillsOverlay.IsOpen) return;   // ESC fecha primeiro o painel de habilidades
        if (MissionInventoryOverlay.IsOpen) return;   // ...e a mochila
        if (flow == null) flow = FindFirstObjectByType<MissionFlow>();
        if (flow != null && flow.IsFinished) return;

        if (!isOpen) OpenPause();
        else if (confirmingExit) BuildPausePanel();
        else ClosePause();
    }

    /// <summary>Atalho PAUSA do HUD.</summary>
    public void Open()
    {
        if (!isOpen) OpenPause();
    }

    void OpenPause()
    {
        if (flow != null && flow.IsFinished) return;
        isOpen = true;
        confirmingExit = false;
        Time.timeScale = 0f;
        BuildPausePanel();
    }

    void ClosePause()
    {
        if (pauseCanvas != null) Destroy(pauseCanvas.gameObject);
        pauseCanvas = null;
        isOpen = false;
        confirmingExit = false;
        Time.timeScale = 1f;
    }

    Canvas NewOverlay()
    {
        if (pauseCanvas != null) Destroy(pauseCanvas.gameObject);
        pauseCanvas = UIFactory.MakeCanvas("PauseMenuCanvas", 120);
        var dim = UIFactory.Img(pauseCanvas.transform, new Color(0.01f, 0.015f, 0.02f, 0.82f), "PauseDim");
        UIFactory.Stretch(dim.rectTransform);
        var panel = UIKit.Frame(pauseCanvas.transform, "PausePanel", new Color(0.106f, 0.133f, 0.169f, 0.97f),
            new Color(0.36f, 0.42f, 0.49f, 0.95f), 2f);
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 500f));
        return pauseCanvas;
    }

    // UI vinda de prefab (UITheme.useEditorUI). Retorna null = usar o codigo antigo.
    Canvas SpawnEditorOverlay(string prefabName)
    {
        if (editorUiBaking || !EditorUiBinding.Enabled) return null;
        var prefab = Resources.Load<GameObject>(EditorUiBinding.ResourcesFolder + prefabName);
        if (prefab == null)
        {
            Debug.LogWarning("[ZomBit UI] Prefab '" + prefabName + "' nao encontrado. Usando a UI em codigo. Rode ZomBit > UI > 1 - Gerar prefabs das telas.");
            return null;
        }
        if (pauseCanvas != null) Destroy(pauseCanvas.gameObject);
        UIFactory.EnsureEventSystem();
        var go = Instantiate(prefab);
        go.name = "PauseMenuCanvas";
        pauseCanvas = go.GetComponent<Canvas>();
        return pauseCanvas;
    }

    void BuildPausePanel()
    {
        confirmingExit = false;
        var prefabCanvas = SpawnEditorOverlay("Overlay_pause");
        if (prefabCanvas != null)
        {
            EditorUiBinding.Hook(EditorUiBinding.Find<Button>(prefabCanvas.transform, "BtnResume"), ClosePause);
            EditorUiBinding.Hook(EditorUiBinding.Find<Button>(prefabCanvas.transform, "BtnLeave"), BuildExitConfirmation);
            return;
        }
        var c = NewOverlay();
        var panel = c.transform.Find("PausePanel");
        if (panel == null) return;
        var title = UIFactory.Txt(panel, "PAUSADO", 54, TextAlignmentOptions.Center, UIFactory.Gold, "PauseTitle");
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(600f, 74f));
        var subtitle = UIFactory.Txt(panel, "A missão está pausada.", 25, TextAlignmentOptions.Center, UIFactory.Gray, "PauseSubtitle");
        UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(600f, 42f));

        var resume = UIFactory.Btn(panel, "Continuar", 30, ClosePause, new Color(0.106f, 0.36f, 0.22f, 1f), "BtnResume");
        UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(470f, 70f));
        var leave = UIFactory.Btn(panel, "Voltar ao Mapa", 30, BuildExitConfirmation, UIFactory.Red, "BtnLeave");
        UIFactory.Place((RectTransform)leave.transform, new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(470f, 70f));
        var hint = UIFactory.Txt(panel, "ESC: continuar a missão", 20, TextAlignmentOptions.Center, UIFactory.Gray, "EscapeHint");
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(560f, 32f));
    }

    void BuildExitConfirmation()
    {
        confirmingExit = true;
        var prefabCanvas = SpawnEditorOverlay("Overlay_confirm");
        if (prefabCanvas != null)
        {
            var bodyText = EditorUiBinding.Find<TMP_Text>(prefabCanvas.transform, "ConfirmBody");
            if (bodyText != null) bodyText.text = ConfirmBodyText();
            EditorUiBinding.Hook(EditorUiBinding.Find<Button>(prefabCanvas.transform, "BtnCancel"), BuildPausePanel);
            EditorUiBinding.Hook(EditorUiBinding.Find<Button>(prefabCanvas.transform, "BtnConfirm"), ConfirmExit);
            return;
        }
        var c = NewOverlay();
        var panel = c.transform.Find("PausePanel");
        if (panel == null) return;
        var title = UIFactory.Txt(panel, GameSession.IsRange ? "SAIR DO ESTANDE?" : "ABANDONAR A MISSÃO?", 42, TextAlignmentOptions.Center, UIFactory.Red, "ConfirmTitle");
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(600f, 65f));
        var body = UIFactory.Txt(panel,
            ConfirmBodyText(), 22, TextAlignmentOptions.Center, Color.white, "ConfirmBody");
        UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(590f, 170f));

        var cancel = UIFactory.Btn(panel, "Continuar jogando", 25, BuildPausePanel, new Color(0.149f, 0.188f, 0.235f, 1f), "BtnCancel");
        UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 36f), new Vector2(280f, 68f));
        var confirm = UIFactory.Btn(panel, "Sair para o Mapa", 25, ConfirmExit, new Color(0.588f, 0.075f, 0.09f, 1f), "BtnConfirm");
        UIFactory.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(150f, 36f), new Vector2(280f, 68f));
    }

    // Abandonar conta como MORTE. Mostra o risco real do nivel atual.
    static string ConfirmBodyText()
    {
        if (GameSession.IsRange) return "Sair do estande de tiro.\nSem penalidade: nenhum nível, XP ou item é perdido.\nO equipamento trocado aqui fica salvo.";
        var d = Difficulties.Get(GameSession.Difficulty);
        string loss = d.itemLossChance <= 0f
            ? "Neste nível não há risco de perder equipamento."
            : $"{Mathf.RoundToInt(d.itemLossChance * 100)}% de chance de perder 1 item equipado" +
              (d.secondLossChance > 0f ? $" (+{Mathf.RoundToInt(d.secondLossChance * 100)}% de um segundo)" : "") + ".";
        return "Sair CONTA COMO MORTE: o local não será concluído, sem recompensa final.\n" +
               "Nível, XP e pontos ganhos nesta missão serão PERDIDOS.\n" + loss + "\n" +
               "Você conserva metade do ouro recolhido. A mochila fica segura.";
    }

    void ConfirmExit()
    {
        if (flow == null) flow = FindFirstObjectByType<MissionFlow>();
        if (flow != null) flow.AbandonToMap();
        else
        {
            // Defensive fallback for an incorrectly configured mission scene.
            Time.timeScale = 1f;
            GameSession.OpenMapOnLoad = !GameSession.DebugRun && GameSession.Save != null;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.MenuSceneIndex);
        }
    }

#if UNITY_EDITOR
    /// <summary>Usado somente pelo UIBaker (Editor): monta as duas janelas de pausa e devolve os canvases prontos.</summary>
    public static List<KeyValuePair<string, GameObject>> EditorUiBake(out GameObject host)
    {
        bool hadEventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null;
        host = new GameObject("__ZombitPauseBake");
        var m = host.AddComponent<MissionPauseMenu>();
        m.editorUiBaking = true;

        var list = new List<KeyValuePair<string, GameObject>>();
        m.BuildPausePanel();
        list.Add(new KeyValuePair<string, GameObject>("Overlay_pause", m.pauseCanvas.gameObject));
        m.pauseCanvas = null;   // evita Destroy() em modo Editor
        m.BuildExitConfirmation();
        list.Add(new KeyValuePair<string, GameObject>("Overlay_confirm", m.pauseCanvas.gameObject));
        m.pauseCanvas = null;

        if (!hadEventSystem)
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es != null) DestroyImmediate(es.gameObject);
        }
        return list;
    }
#endif
}
