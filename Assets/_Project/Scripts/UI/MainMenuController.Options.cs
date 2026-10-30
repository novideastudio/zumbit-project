using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tela de opcoes e creditos (parte de MainMenuController).
public partial class MainMenuController
{
    // ---------- opções ----------
    void BuildOptions()
    {
        var editorScreen = EditorUiScreen("options");
        if (editorScreen != null) { BindOptionsFromPrefab(editorScreen); return; }

        var s = NewScreen("options");
        AddCinematicBackground(s, 0.70f);
        var optionsPanel = UIKit.Frame(s, "OptionsPanel", new Color(0.018f, 0.031f, 0.043f, 0.94f),
            new Color(0.36f, 0.42f, 0.49f, 0.9f), 2f);
        UIFactory.Place(optionsPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(860f, 540f));
        Title(s, "OPÇÕES");
        var list = UIFactory.Node("List", s);
        UIFactory.Place(list, Mid, Vector2.zero, new Vector2(700, 370));
        UIFactory.VLayout(list, 24);

        var row = UIFactory.Node("VolumeRow", list);
        UIFactory.Elem(row.gameObject, 700, 70);
        UIFactory.HLayout(row, 10);
        var minus = UIFactory.Btn(row, "-", 40, () => ChangeVolume(-0.1f), null, "VolMinus");
        UIFactory.Elem(minus.gameObject, 90, 70);
        volumeLabel = UIFactory.Txt(row, "", 34, TextAlignmentOptions.Center, Color.white, "VolLabel");
        UIFactory.Elem(volumeLabel.gameObject, 440, 70);
        var plus = UIFactory.Btn(row, "+", 40, () => ChangeVolume(0.1f), null, "VolPlus");
        UIFactory.Elem(plus.gameObject, 90, 70);

        var fs = UIFactory.Btn(list, "", 32, ToggleFullscreen, null, "BtnFullscreen");
        UIFactory.Elem(fs.gameObject, 700, 70);
        fsLabel = fs.GetComponentInChildren<TMP_Text>();

        var resolution = UIFactory.Btn(list, "", 30, CycleResolution, null, "BtnResolution");
        UIFactory.Elem(resolution.gameObject, 700, 70);
        resolutionLabel = resolution.GetComponentInChildren<TMP_Text>();

        var shake = UIFactory.Btn(list, "", 32, ToggleScreenShake, null, "BtnShake");
        UIFactory.Elem(shake.gameObject, 700, 70);
        shakeLabel = shake.GetComponentInChildren<TMP_Text>();

        BackButton(s, "Voltar", ReturnFromOptions, 0, "BtnBack");
    }

    void ReturnFromOptions()
    {
        string destination = string.IsNullOrEmpty(optionsReturnScreen) ? "main" : optionsReturnScreen;
        optionsReturnScreen = null;
        Show(destination);
    }

    void RefreshOptions()
    {
        volumeLabel.text = $"Volume: {Mathf.RoundToInt(AudioListener.volume * 100)}%";
        fsLabel.text = Screen.fullScreen ? "Tela cheia: LIGADA" : "Tela cheia: DESLIGADA";
        if (shakeLabel) shakeLabel.text = HeroDamageFeedback.ShakeEnabled ? "Tremor de tela: LIGADO" : "Tremor de tela: DESLIGADO";
        RefreshResolutionLabel();
    }

    TMP_Text shakeLabel;

    void ToggleScreenShake()
    {
        HeroDamageFeedback.ShakeEnabled = !HeroDamageFeedback.ShakeEnabled;
        RefreshOptions();
    }

    void ChangeVolume(float d)
    {
        AudioListener.volume = Mathf.Clamp01(Mathf.Round((AudioListener.volume + d) * 10f) / 10f);
        PlayerPrefs.SetFloat("zombit_vol", AudioListener.volume);
        RefreshOptions();
    }

    void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        PlayerPrefs.SetInt("zombit_fs", Screen.fullScreen ? 1 : 0);
        PlayerPrefs.Save();
        RefreshOptions();
    }

    // Screen.resolutions lista a mesma largura x altura uma vez por taxa de atualizacao (60/120/144 Hz...),
    // entao o botao "Resolucao" parecia nao fazer nada em varios cliques. Aqui fica uma entrada por tamanho.
    static Resolution[] UniqueResolutions()
    {
        var src = Screen.resolutions;
        var list = new List<Resolution>();
        if (src == null) return list.ToArray();
        for (int i = 0; i < src.Length; i++)
        {
            int found = -1;
            for (int j = 0; j < list.Count; j++)
                if (list[j].width == src[i].width && list[j].height == src[i].height) { found = j; break; }
            if (found < 0) list.Add(src[i]);
            else if (src[i].refreshRateRatio.value > list[found].refreshRateRatio.value) list[found] = src[i];
        }
        return list.ToArray();
    }

    void ApplySavedResolution()
    {
        var all = UniqueResolutions();
        if (all == null || all.Length == 0) return;
        // Não altera a resolução padrão na primeira execução; só reaplica uma escolha salva.
        if (!PlayerPrefs.HasKey("zombit_res_w") || !PlayerPrefs.HasKey("zombit_res_h")) return;
        int savedW = PlayerPrefs.GetInt("zombit_res_w");
        int savedH = PlayerPrefs.GetInt("zombit_res_h");
        resolutionIndex = -1;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].width == savedW && all[i].height == savedH) { resolutionIndex = i; break; }
        }
        if (resolutionIndex < 0)
        {
            resolutionIndex = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].width == 1600 && all[i].height == 900) { resolutionIndex = i; break; }
        }
        Screen.SetResolution(all[resolutionIndex].width, all[resolutionIndex].height, Screen.fullScreen);
    }

    void CycleResolution()
    {
        var all = UniqueResolutions();
        if (all == null || all.Length == 0) { Toast("Nenhuma resolução adicional disponível."); return; }
        if (resolutionIndex < 0) resolutionIndex = 0;
        resolutionIndex = (resolutionIndex + 1) % all.Length;
        var r = all[resolutionIndex];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        PlayerPrefs.SetInt("zombit_res_w", r.width);
        PlayerPrefs.SetInt("zombit_res_h", r.height);
        PlayerPrefs.Save();
        RefreshResolutionLabel();
    }

    void RefreshResolutionLabel()
    {
        if (!resolutionLabel) return;
        resolutionLabel.text = $"Resolução: {Screen.width} × {Screen.height}  (clicar para alterar)";
    }

    // ---------- créditos ----------
    void BuildCredits()
    {
        var editorScreen = EditorUiScreen("credits");
        if (editorScreen != null) { BindCreditsFromPrefab(editorScreen); return; }

        var s = NewScreen("credits");
        AddCinematicBackground(s, 0.72f);
        var creditsPanel = UIKit.Frame(s, "CreditsPanel", new Color(0.018f, 0.031f, 0.043f, 0.94f),
            new Color(0.36f, 0.42f, 0.49f, 0.9f), 2f);
        UIFactory.Place(creditsPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f, 620f));
        Title(s, "CRÉDITOS");
        var t = UIFactory.Txt(s,
            "ZOMBIT SURVIVAL\n\nIdealização e desenvolvimento: Tiago Gomes\nEngine: Unity 6.6\nArte e áudio: em desenvolvimento\n\nObrigado por jogar!",
            36, TextAlignmentOptions.Center, Color.white, "Credits");
        UIFactory.Place(t.rectTransform, Mid, Vector2.zero, new Vector2(1200, 600));
        BackButton(s, "Voltar", () => Show("main"), 0, "BtnBack");
    }
}
