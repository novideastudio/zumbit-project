using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel HABILIDADES (atalho [H] ou botao no HUD): pausa a missao e mostra, para cada sobrevivente
/// do esquadrao, a arma exclusiva e a habilidade propria, mais os comandos ativos do jogador.
/// So leitura: a arvore de habilidades continua no menu (os pontos sao gravados no fim da missao).
/// </summary>
public sealed class SquadSkillsOverlay : MonoBehaviour
{
    static SquadSkillsOverlay instance;
    static int closedFrame = -1;
    float previousTimeScale = 1f;
    Canvas canvas;

    /// <summary>True enquanto aberto (e no quadro em que fechou, para o ESC nao abrir a pausa junto).</summary>
    public static bool IsOpen => instance != null || closedFrame == Time.frameCount;

    public static void Toggle()
    {
        if (instance != null) { instance.Close(); return; }
        // Nao abre por cima da pausa, da tela de resultado ou com o jogo ja parado.
        if (Time.timeScale == 0f) return;
        var flow = FindFirstObjectByType<MissionFlow>();
        if (flow != null && flow.IsFinished) return;
        instance = new GameObject("SquadSkillsOverlay").AddComponent<SquadSkillsOverlay>();
        instance.Build();
    }

    void Build()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        canvas = UIFactory.MakeCanvas("SquadSkillsCanvas", 110);
        canvas.transform.SetParent(transform, false);
        var dim = UIFactory.Img(canvas.transform, new Color(0.01f, 0.015f, 0.02f, 0.82f), "Dim");
        UIFactory.Stretch(dim.rectTransform);

        int n = Mathf.Max(1, HeroSquad.Count);
        const float colW = 380f, gap = 20f;
        float panelW = Mathf.Max(820f, n * colW + (n - 1) * gap + 80f);
        var panel = UIKit.Frame(canvas.transform, "SkillsPanel", new Color(0.018f, 0.031f, 0.043f, 0.97f), UIKit.Col.BorderHi, 2f);
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, 700f));
        var accent = UIFactory.Img(panel, UIKit.Col.Cyan, "Accent");
        UIFactory.Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(6f, 700f));
        accent.raycastTarget = false;

        var title = Txt(panel, "HABILIDADES DO ESQUADRÃO", 40f, UIKit.Col.Gold, TextAlignmentOptions.Center);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(panelW - 80f, 56f));
        var sub = Txt(panel, "Cada sobrevivente usa a própria arma e a própria habilidade (automáticas). A missão fica pausada.",
            18f, UIKit.Col.TextDim, TextAlignmentOptions.Center);
        sub.fontStyle = FontStyles.Normal;
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(panelW - 80f, 28f));

        float total = n * colW + (n - 1) * gap;
        if (HeroSquad.Count == 0) MemberColumn(panel, null, GameSession.Survivor, 0, 1, 0f, colW);
        else
            for (int i = 0; i < n; i++)
            {
                var m = HeroSquad.Members[i];
                float cx = -total * 0.5f + colW * 0.5f + i * (colW + gap);
                MemberColumn(panel, m, m.def, i, n, cx, colW);
            }

        // Comandos do jogador.
        var cmd = UIKit.Frame(panel, "Commands", UIKit.Col.Surface, UIKit.Col.Border, 1.5f);
        UIFactory.Place(cmd, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(panelW - 80f, 96f));
        var active = HeroSquad.PrimaryMember != null && HeroSquad.PrimaryMember.stats ? HeroSquad.PrimaryMember.stats.GetComponent<ActiveSkills>() : null;
        string cd = active != null ? $"{active.shockCooldown:0}s" : "14s";
        string gold = ColorUtility.ToHtmlStringRGB(UIKit.Col.Gold);
        var ct = Txt(cmd,
            $"<color=#{gold}>[ESPAÇO]</color> Empurrar zumbi: empurra, atordoa e fere quem está perto (recarga {cd}). Também dá para clicar no botão do HUD.\n" +
            $"<color=#{gold}>[CLIQUE]</color> no zumbi: todo o esquadrão foca nele    <color=#{gold}>[I]</color> Mochila    <color=#{gold}>[H]</color> Habilidades    <color=#{gold}>[ESC]</color> Pausa",
            17f, Color.white, TextAlignmentOptions.MidlineLeft);
        ct.fontStyle = FontStyles.Normal;
        ct.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.Stretch(ct.rectTransform);
        UIFactory.Inset(ct.rectTransform, 20f, 10f);

        var close = UIKit.Button(panel, "FECHAR  [H]", Close, UIKit.BtnStyle.Primary, 22f, "CloseSkills");
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 60f));
    }

    void MemberColumn(RectTransform panel, HeroSquad.Member m, SurvivorDef def, int index, int count, float centerX, float w)
    {
        Color accent = index == 0 ? UIKit.Col.Accent : UIKit.Col.Cyan;
        var col = UIKit.Frame(panel, "Member_" + index, UIKit.Col.Surface, accent, 1.5f);
        UIFactory.Place(col, new Vector2(0.5f, 1f), new Vector2(centerX, -132f), new Vector2(w, 330f));
        bool alive = m == null || m.Alive;

        string role = index == 0 ? "FRENTE" : count > 2 ? (index == 1 ? "MEIO" : "FUNDO") : "APOIO";
        var r = Txt(col, role + (alive ? "" : "  ·  CAIU"), 14f, alive ? accent : UIKit.Col.TextDim, TextAlignmentOptions.MidlineLeft);
        r.characterSpacing = 1.6f;
        Put(r.rectTransform, 20f, 14f, w - 40f, 20f);

        int lvl = m != null && m.stats ? m.stats.Level : 1;
        var name = Txt(col, (def != null ? def.name.ToUpperInvariant() : "SOBREVIVENTE") +
            $"  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(UIKit.Col.Gold)}>NV {lvl}</color></size>", 26f, Color.white, TextAlignmentOptions.MidlineLeft);
        Put(name.rectTransform, 20f, 36f, w - 40f, 34f);

        // Arma exclusiva.
        var hero = m != null ? m.hero : null;
        var wpn = hero != null ? hero.weapon : null;
        var wt = Txt(col, "ARMA", 12f, UIKit.Col.TextDim, TextAlignmentOptions.MidlineLeft);
        wt.characterSpacing = 1.6f;
        Put(wt.rectTransform, 20f, 82f, w - 40f, 18f);
        string wline = wpn != null
            ? $"{(wpn.displayName ?? "").ToUpperInvariant()}\n<size=80%><color=#9AA6B2>Dano {wpn.damage:0.#}  ·  {wpn.shotsPerSecond:0.#} tiros/s  ·  Pente {wpn.magazineSize}  ·  Recarga {wpn.reloadTime:0.#}s</color></size>"
            : "-";
        var wv = Txt(col, wline, 18f, Color.white, TextAlignmentOptions.TopLeft);
        wv.textWrappingMode = TextWrappingModes.Normal;
        Put(wv.rectTransform, 20f, 102f, w - 40f, 56f);

        // Habilidade propria.
        var at = Txt(col, "HABILIDADE", 12f, UIKit.Col.TextDim, TextAlignmentOptions.MidlineLeft);
        at.characterSpacing = 1.6f;
        Put(at.rectTransform, 20f, 170f, w - 40f, 18f);
        string an = def != null && !string.IsNullOrWhiteSpace(def.utilityName) ? def.utilityName.ToUpperInvariant() : "-";
        var av = Txt(col, an, 20f, UIKit.Col.Gold, TextAlignmentOptions.MidlineLeft);
        Put(av.rectTransform, 20f, 190f, w - 40f, 28f);
        var ad = Txt(col, def != null ? def.utilityDesc ?? "" : "", 16f, new Color(0.85f, 0.88f, 0.90f), TextAlignmentOptions.TopLeft);
        ad.fontStyle = FontStyles.Normal;
        ad.textWrappingMode = TextWrappingModes.Normal;
        ad.enableAutoSizing = true; ad.fontSizeMin = 12f; ad.fontSizeMax = 16f;
        Put(ad.rectTransform, 20f, 222f, w - 40f, 96f);
    }

    static TMP_Text Txt(RectTransform parent, string text, float size, Color color, TextAlignmentOptions al)
    {
        var t = UIFactory.Txt(parent, text, size, al, color, "Text");
        t.fontStyle = FontStyles.Bold;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.raycastTarget = false;
        return t;
    }

    static void Put(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();   // [H] e tratado pelo HUD (Toggle)
    }

    void Close()
    {
        if (instance == this) { instance = null; closedFrame = Time.frameCount; }
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; Time.timeScale = 1f; }
    }
}
