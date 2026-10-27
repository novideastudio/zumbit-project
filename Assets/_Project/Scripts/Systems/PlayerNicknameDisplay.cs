using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rotulo flutuante sobre CADA sobrevivente: "CLASSE  NV x" (o nivel e so DELE) e, logo abaixo,
/// uma barrinha de vida. Com a postura OFENSIVA ativa, uma caveira avermelhada aparece antes do nome da classe.
/// </summary>
public class PlayerNicknameDisplay : MonoBehaviour
{
    const float BarWidth = 124f, BarHeight = 10f;
    const float SkullSize = 17f, SkullGap = 5f;
    static readonly Color SkullTint = new Color(1f, 0.34f, 0.30f, 1f);
    static Sprite skullSprite;
    static bool skullTried;

    Transform target;
    SpriteRenderer targetSprite;
    PlayerStats targetStats;
    Health targetHealth;
    SurvivorDef def;

    RectTransform canvasRect, labelRect, barRect, fillRect, skullRect;
    TMP_Text label;
    Image barBack, fill, skull;
    Camera cachedCamera;
    SquadCombatPosture posture;
    float nextPostureLookup;
    int shownLevel = -1;
    float shownFraction = -1f;

    /// <summary>Cria o rotulo (classe + nivel + vida) de um sobrevivente. Uma instancia por sobrevivente.</summary>
    public static PlayerNicknameDisplay Attach(Transform hero, SurvivorDef survivor)
    {
        if (hero == null) return null;
        var display = new GameObject("HeroTag_" + (survivor != null ? survivor.id : hero.name)).AddComponent<PlayerNicknameDisplay>();
        display.Configure(hero, survivor);
        return display;
    }

    static Sprite SkullSprite()
    {
        if (skullTried) return skullSprite;
        skullTried = true;
        var tex = Resources.Load<Texture2D>("Art/Home/posture_skull");
        if (tex != null)
            skullSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return skullSprite;
    }

    void Configure(Transform hero, SurvivorDef survivor)
    {
        target = hero;
        def = survivor;
        targetSprite = hero.GetComponent<SpriteRenderer>();
        if (targetSprite == null) targetSprite = hero.GetComponentInChildren<SpriteRenderer>();
        targetStats = hero.GetComponent<PlayerStats>();
        targetHealth = hero.GetComponent<Health>();
        BuildUI();
    }

    void BuildUI()
    {
        var canvas = UIFactory.MakeCanvas("HeroTagCanvas", 35);
        canvas.transform.SetParent(transform, false);
        canvasRect = canvas.transform as RectTransform;

        label = UIFactory.Txt(canvas.transform, "", 19, TextAlignmentOptions.Center, Color.white, "HeroTagLabel");
        labelRect = label.rectTransform;
        UIFactory.Place(labelRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 26f));
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 0.8f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.richText = true;
        label.raycastTarget = false;
        var sh = label.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(2f, -2f);

        var sprite = SkullSprite();
        if (sprite != null)
        {
            skull = UIFactory.Img(canvas.transform, SkullTint, "OffensiveSkull");
            skull.sprite = sprite;
            skull.preserveAspect = true;
            skull.raycastTarget = false;
            skullRect = skull.rectTransform;
            UIFactory.Place(skullRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SkullSize, SkullSize));
            skull.gameObject.SetActive(false);
        }

        barBack = UIFactory.Img(canvas.transform, new Color(0.02f, 0.03f, 0.04f, 0.88f), "HeroTagBar");
        barRect = barBack.rectTransform;
        UIFactory.Place(barRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BarWidth, BarHeight));
        barBack.raycastTarget = false;

        fill = UIFactory.Img(barRect, new Color(0.86f, 0.16f, 0.18f, 1f), "HeroTagFill");
        fillRect = fill.rectTransform;
        UIFactory.Stretch(fillRect, 0f, 0f, 1f, 1f);
        fillRect.offsetMin = new Vector2(1.5f, 1.5f);
        fillRect.offsetMax = new Vector2(-1.5f, -1.5f);
        fill.raycastTarget = false;
    }

    string ClassName()
    {
        if (def != null && !string.IsNullOrWhiteSpace(def.name)) return def.name.ToUpperInvariant();
        return "SOBREVIVENTE";
    }

    void SetVisible(bool v)
    {
        if (label != null && label.gameObject.activeSelf != v) label.gameObject.SetActive(v);
        if (barBack != null && barBack.gameObject.activeSelf != v) barBack.gameObject.SetActive(v);
        if (!v && skull != null && skull.gameObject.activeSelf) skull.gameObject.SetActive(false);
    }

    bool OffensiveActive()
    {
        if (posture == null && Time.unscaledTime >= nextPostureLookup)
        {
            nextPostureLookup = Time.unscaledTime + 0.5f;
            posture = FindFirstObjectByType<SquadCombatPosture>();
        }
        return posture != null && posture.IsOffensive && !GameSession.IsRange;
    }

    void LateUpdate()
    {
        if (target == null || canvasRect == null || label == null) { SetVisible(false); return; }
        // Sobrevivente caido: o rotulo some junto com o sprite.
        if (targetHealth != null && targetHealth.Current <= 0f) { SetVisible(false); return; }
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        int level = targetStats != null ? targetStats.Level : 1;
        if (level != shownLevel)
        {
            shownLevel = level;
            label.text = $"{ClassName()}  <color=#FFC24B>NV {level}</color>";
        }
        float frac = targetHealth != null && targetHealth.max > 0f ? Mathf.Clamp01(targetHealth.Current / targetHealth.max) : 1f;
        if (!Mathf.Approximately(frac, shownFraction))
        {
            shownFraction = frac;
            fillRect.anchorMax = new Vector2(frac, 1f);
        }

        float topY = targetSprite != null ? targetSprite.bounds.max.y : target.position.y + 1.6f;
        Vector3 screen = cachedCamera.WorldToScreenPoint(new Vector3(target.position.x, topY + 0.18f, target.position.z));
        bool visible = screen.z > 0f;
        SetVisible(visible);
        if (!visible) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            barRect.anchoredPosition = local + new Vector2(0f, 6f);
            labelRect.anchoredPosition = local + new Vector2(0f, 26f);

            if (skull != null)
            {
                bool offensive = OffensiveActive();
                if (skull.gameObject.activeSelf != offensive) skull.gameObject.SetActive(offensive);
                if (offensive)
                {
                    // O texto e centralizado: a caveira fica logo antes do primeiro caractere da classe.
                    float textWidth = label.preferredWidth;
                    skullRect.anchoredPosition = local + new Vector2(-(textWidth * 0.5f + SkullGap + SkullSize * 0.5f), 26f);
                }
            }
        }
    }
}
