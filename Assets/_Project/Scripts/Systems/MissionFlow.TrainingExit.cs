using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dedicated range exit: no death rolls, completion rewards or campaign progression.
public partial class MissionFlow
{
    Canvas trainingExitError;

    public void ExitTrainingToMap()
    {
        if (!GameSession.IsRange || done) return;
        done = true;
        try
        {
            var save = GameSession.Save;
            if (!GameSession.DebugRun && save != null)
            {
                var inventory = FindFirstObjectByType<Inventory>();
                // Resolve the inventory owner from its actual PlayerStats, not campaign selection.
                if (inventory != null)
                {
                    var owner = inventory.stats;
                    if (owner == null || owner.Survivor == null || string.IsNullOrEmpty(owner.Survivor.id))
                        throw new InvalidOperationException("Não foi possível identificar o dono atual da mochila. Saída cancelada para proteger o equipamento.");
                    inventory.SaveTo(save, owner.Survivor.id, owner);
                }
                // Consult the live training squad; MissionFlow.squad may describe the initial group.
                var visited = new HashSet<string>();
                foreach (var member in HeroSquad.Members)
                {
                    if (member == null || member.stats == null || member.def == null || string.IsNullOrEmpty(member.def.id)) continue;
                    if (!visited.Add(member.def.id)) continue;
                    save.GetProgress(member.def.id).equipped = member.stats.EquippedList();
                }
                // Deliberately DO NOT touch gold, scrap, XP, level, skill points, formation,
                // completed, lostItems, pendingNotice or an unrelated activeRun.
                if (save.slot > 0) SaveSystem.Save(save);
            }
        }
        catch (Exception e)
        {
            done = false;
            Debug.LogError("Zombit: não foi possível salvar ao sair do treinamento. A cena foi mantida aberta. " + e.Message);
            ShowTrainingExitError();
            return;
        }

        // Do not restore GameSession.Survivor here: Inventory.OnDestroy may still flush
        // using the current training owner. The menu reads the campaign formation from Save.
        GameSession.OpenMapOnLoad = !GameSession.DebugRun && GameSession.Save != null;
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameSession.MenuSceneIndex);
    }

    void ShowTrainingExitError()
    {
        if (trainingExitError != null) Destroy(trainingExitError.gameObject);
        trainingExitError = UIFactory.MakeCanvas("TrainingExitSaveError", 250);
        var dim = UIFactory.Img(trainingExitError.transform, new Color(0f,0f,0f,0.80f), "Dim");
        UIFactory.Stretch(dim.rectTransform);
        var panel = UIKit.Frame(trainingExitError.transform, "Panel", new Color(0.04f,0.06f,0.08f,1f), UIFactory.Red, 2f);
        UIFactory.Stretch(panel, 0.20f,0.30f,0.80f,0.70f);
        var text = UIFactory.Txt(panel,
            "NÃO FOI POSSÍVEL SALVAR\nVocê continua no treinamento.\nNenhuma penalidade de abandono foi aplicada.\nConfira o Console e tente sair novamente.",
            26, TextAlignmentOptions.Center, Color.white, "Message");
        text.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.Stretch(text.rectTransform,0.04f,0.30f,0.96f,0.95f);
        var close = UIFactory.Btn(panel,"ENTENDI",24,() =>
        {
            if (trainingExitError != null) Destroy(trainingExitError.gameObject);
            trainingExitError = null;
        },null,"Close");
        UIFactory.Stretch((RectTransform)close.transform,0.30f,0.05f,0.70f,0.25f);
    }
}
