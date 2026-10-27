using UnityEngine;
using UnityEngine.UI;

/// <summary>Utilidades para ligar o codigo aos prefabs de UI feitos no Editor (procura por NOME de objeto).</summary>
public static class EditorUiBinding
{
    public const string ResourcesFolder = "UI/Screens/";

    public static bool Enabled => UITheme.Current.useEditorUI;

    public static Transform FindDeep(Transform parent, string objName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == objName) return c;
            var r = FindDeep(c, objName);
            if (r != null) return r;
        }
        return null;
    }

    public static T Find<T>(Transform parent, string objName) where T : Component
    {
        var t = FindDeep(parent, objName);
        if (t == null)
        {
            Debug.LogError("[ZomBit UI] Objeto '" + objName + "' nao encontrado no prefab '" + parent.name +
                           "'. Nao renomeie nem apague objetos que o codigo usa (veja o LEIA-ME).");
            return null;
        }
        var c = t.GetComponent<T>();
        if (c == null)
            Debug.LogError("[ZomBit UI] '" + objName + "' em '" + parent.name + "' precisa ter o componente " + typeof(T).Name + ".");
        return c;
    }

    // Mesmo comportamento de UIFactory.Btn: som de UI + acao.
    public static void Hook(Button b, System.Action action)
    {
        if (b == null) return;
        b.onClick.AddListener(() => { ZombitAudio.PlayUI(); action(); });
    }
}
