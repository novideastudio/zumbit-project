using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Direct entry: selection is now inside the DPS range.
public partial class MainMenuController
{
    bool enteringTraining;

    void AddRangeSideButton(RectTransform parent)
    {
        var b = UIKit.Button(parent, "ESTANDE", OpenRangePicker, UIKit.BtnStyle.Secondary, 17f, "Side_Range");
        UIFactory.Elem(b.gameObject, 170f, 60f);
        UIFactory.SetButtonColor(b, new Color(0.075f, 0.10f, 0.13f, 0.96f));
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t) { t.alignment = TextAlignmentOptions.MidlineLeft; t.margin = new Vector4(22f, 2f, 8f, 2f); }
    }

    // Name retained so existing Home buttons and prefab bindings keep working.
    void OpenRangePicker() => StartRange();

    void CloseRangePicker() { }

    void StartRange()
    {
        var save = GameSession.Save;
        if (save == null || enteringTraining) return;
        var survivor = GameDatabase.GetSurvivor(save.selectedSurvivor);
        if (survivor == null) return;
        enteringTraining = true;
        GameSession.Location = new LocationDef
        {
            id = "range",
            isRange = true,
            name = "Estande de Tiro",
            description = "Area de treinamento para testar armas, alcance e dano.",
            theme = "escola",
            recommendedLevel = 1,
            difficulty = 1,
            waves = 0,
            countMult = 0f,
            goldReward = 0,
            goldMult = 0f,
            hasBoss = false,
            mapX = 0.5f,
            mapY = 0.5f
        };
        // Starts with the current principal; add others inside the range.
        // Does not change the campaign squad stored in SaveData.
        GameSession.Survivor = survivor;
        GameSession.Partner = null;
        GameSession.Third = null;
        GameSession.DebugRun = false;
        GameSession.Difficulty = (int)global::Difficulty.Normal;
        GameSession.InsuranceBought = false;
        SceneManager.LoadScene(GameSession.GameSceneIndex);
    }
}
