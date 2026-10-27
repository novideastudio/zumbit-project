using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Zombit/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    [Header("Interface")]
    [Tooltip("LIGADO = telas do menu vem dos prefabs em Resources/UI/Screens (editaveis na Unity). DESLIGADO = UI montada por codigo, como antes.")]
    public bool useEditorUI = false;
    [Tooltip("DESLIGADO (recomendado) = botoes dos prefabs ficam EXATAMENTE como voce editou (cores, ColorBlock, fonte). LIGADO = o codigo reaplica o realce dourado/negrito do menu por cima dos prefabs.")]
    public bool polishPrefabButtons = false;

    [Header("Fonte")]
    [Tooltip("Vazio = fonte padrao do TextMeshPro")]
    public TMP_FontAsset font;
    [Tooltip("Fonte para titulos e botoes (tamanho 20+). Vazio = usa a fonte padrao acima")]
    public TMP_FontAsset displayFont;
    [Min(0f), Tooltip("Tamanho minimo de qualquer texto (referencia 1920x1080). 0 = desligado")]
    public float minFontSize = 14f;
    [Min(0.1f), Tooltip("Multiplica todos os textos")]
    public float fontScale = 1f;
    [Min(0.1f), Tooltip("Multiplica apenas os titulos grandes (tamanho 60+)")]
    public float titleScale = 1f;
    [Range(0f, 1f)] public float outlineWidth = 0f;
    public Color outlineColor = Color.black;

    [Header("Cores")]
    public Color backgroundColor = new Color(0.051f, 0.067f, 0.082f, 1f);   // carvão profundo #0D1115
    public Color panelColor = new Color(0.106f, 0.133f, 0.169f, 0.96f);      // #1B222B
    public Color accentColor = new Color(0.847f, 0.141f, 0.169f);            // vermelho sangue #D8242B
    public Color goldColor = new Color(1f, 0.761f, 0.294f);                   // dourado #FFC24B
    public Color grayColor = new Color(0.604f, 0.651f, 0.698f);
    public Color buttonColor = new Color(0.149f, 0.188f, 0.235f);              // #26303C
    public Color cyanColor = new Color(0.247f, 0.784f, 0.910f);               // ciano tático #3FC8E8

    [Header("Sprites (opcionais)")]
    [Tooltip("Imagem de fundo das telas de menu")]
    public Sprite menuBackground;
    [Tooltip("Moldura dos paineis. Defina as bordas (9-slice) no Sprite Editor")]
    public Sprite panelSprite;
    public Color panelTint = Color.white;
    [Tooltip("Moldura dos botoes. Defina as bordas (9-slice) no Sprite Editor")]
    public Sprite buttonSprite;
    public Color buttonTint = Color.white;

    static UITheme current;

    public static UITheme Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<UITheme>("Config/UITheme");
                if (current == null)
                {
                    current = CreateInstance<UITheme>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return current;
        }
    }

    public static void Reload() => current = null;
}
