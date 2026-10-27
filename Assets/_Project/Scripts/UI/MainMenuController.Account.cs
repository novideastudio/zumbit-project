using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Conta online: tela de login (Google), registro de nick unico e selo da conta no menu (parte de MainMenuController).
public partial class MainMenuController
{
    string currentScreen = "";
    TMP_Text loginStatus, accountLabel;
    Button loginGoogleBtn, loginRetryBtn;
    bool nicknameForAccount, accountNickFirst, nickBusy;

    // ---------- tela de login ----------
    void BuildLogin()
    {
        var editorScreen = EditorUiScreen("login");
        if (editorScreen != null) { BindLoginFromPrefab(editorScreen); return; }

        var s = NewScreen("login");
        AddCinematicBackground(s, 0.78f);

        var logo = UIFactory.Txt(s, "<color=#F2F4F5>ZOMBIT</color> <color=#D8242B>SURVIVAL</color>", 84,
            TextAlignmentOptions.Center, Color.white, "LoginLogo");
        UIFactory.Place(logo.rectTransform, TopC, new Vector2(0f, -70f), new Vector2(1400f, 130f));
        logo.fontStyle = FontStyles.Bold;
        logo.textWrappingMode = TextWrappingModes.NoWrap;

        var panel = UIKit.Frame(s, "LoginPanel", new Color(0.018f, 0.031f, 0.043f, 0.94f),
            new Color(0.36f, 0.42f, 0.49f, 0.9f), 2f);
        UIFactory.Place(panel, Mid, new Vector2(0f, -40f), new Vector2(900f, 540f));

        var title = UIFactory.Txt(panel, "CONTA ZOMBIT", 44, TextAlignmentOptions.Center, UIKit.Col.Gold, "LoginTitle");
        title.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(title.rectTransform, 0.05f, 0.80f, 0.95f, 0.95f);

        var sub = UIFactory.Txt(panel,
            "Entre com sua conta Google para salvar a campanha online e continuar de qualquer aparelho.\nNa primeira vez você escolhe o seu nick.",
            24, TextAlignmentOptions.Center, UIKit.Col.TextDim, "LoginSub");
        sub.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.Stretch(sub.rectTransform, 0.08f, 0.52f, 0.92f, 0.80f);

        loginStatus = UIFactory.Txt(panel, "", 24, TextAlignmentOptions.Center, UIKit.Col.Gold, "LoginStatus");
        loginStatus.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.Stretch(loginStatus.rectTransform, 0.08f, 0.37f, 0.92f, 0.52f);

        loginGoogleBtn = UIFactory.Btn(panel,
            (!editorUiBaking && ZombitCloud.IsLocalBackend) ? "ENTRAR (CONTA LOCAL DE TESTE)" : "ENTRAR COM GOOGLE",
            30, ZombitCloud.SignInWithGoogle, UIFactory.Red, "LoginGoogleButton");
        UIFactory.Stretch((RectTransform)loginGoogleBtn.transform, 0.12f, 0.20f, 0.88f, 0.35f);

        loginRetryBtn = UIFactory.Btn(panel, "TENTAR NOVAMENTE", 26, ZombitCloud.Retry, null, "LoginRetryButton");
        UIFactory.Stretch((RectTransform)loginRetryBtn.transform, 0.25f, 0.05f, 0.75f, 0.17f);
        loginRetryBtn.gameObject.SetActive(false);
    }

    void ShowLogin()
    {
        RefreshLogin();
        Show("login");
    }

    void RefreshLogin()
    {
        if (loginStatus == null) return;
        string status;
        bool canSignIn = false, canRetry = false, isError = false;
        switch (ZombitCloud.State)
        {
            case CloudState.Starting: status = "Conectando aos servidores..."; break;
            case CloudState.SignedOut:
                status = ZombitCloud.LastError;
                isError = !string.IsNullOrEmpty(status);
                canSignIn = true;
                break;
            case CloudState.SigningIn: status = "Aguardando o login do Google...\nConclua na janela que abriu."; break;
            case CloudState.LoadingProfile: status = "Carregando sua conta..."; break;
            case CloudState.NeedsNick: status = "Conta encontrada! Falta escolher o seu nick."; break;
            case CloudState.Syncing: status = "Sincronizando seu progresso..."; break;
            case CloudState.Ready: status = "Tudo pronto!"; break;
            default: status = ZombitCloud.LastError; isError = true; canRetry = true; break;
        }
        loginStatus.text = status;
        loginStatus.color = isError ? new Color(1f, 0.45f, 0.42f) : UIKit.Col.Gold;
        loginGoogleBtn.interactable = canSignIn;
        loginRetryBtn.gameObject.SetActive(canRetry);
    }

    // ---------- reacao aos estados da conta ----------
    void OnCloudStateChanged()
    {
        if (this == null) return;
        RefreshLogin();
        switch (ZombitCloud.State)
        {
            case CloudState.Ready:
                ApplyAccountNickPrefs();
                if (currentScreen == "login" || (currentScreen == "nickname" && nicknameForAccount && accountNickFirst))
                {
                    nicknameForAccount = false;
                    accountNickFirst = false;
                    Show("main");
                }
                else RefreshAccountLabel();
                break;
            case CloudState.NeedsNick:
                OpenAccountNick("main", true);
                break;
            case CloudState.SignedOut:
                PlayerPrefs.DeleteKey(PendingNicknameKey);
                nicknameForAccount = false;
                if (currentScreen != "login") ShowLogin();
                break;
            default:
                if (currentScreen != "login") ShowLogin();
                break;
        }
    }

    void ApplyAccountNickPrefs()
    {
        if (!ZombitCloud.HasNick) return;
        PlayerPrefs.SetString(PendingNicknameKey, ZombitCloud.Nick);
        PlayerPrefs.Save();
    }

    /// <summary>Todo save aberto neste menu passa a mostrar o nick da conta (um nick por conta, nao por slot).</summary>
    void UseSave(SaveData d)
    {
        if (d != null && ZombitCloud.IsReady && ZombitCloud.HasNick) d.playerNickname = ZombitCloud.Nick;
        GameSession.Save = d;
    }

    // ---------- selo da conta no menu principal ----------
    void BuildAccountBadge()
    {
        if (!screens.TryGetValue("main", out var s)) return;
        accountLabel = UIFactory.Txt(s, "", 20, TextAlignmentOptions.TopRight, new Color(0.78f, 0.85f, 0.88f), "AccountLabel");
        UIFactory.Place(accountLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(700f, 64f));
        accountLabel.richText = true;
        accountLabel.textWrappingMode = TextWrappingModes.NoWrap;
        AddTextShadow(accountLabel.gameObject);

        var signOut = UIFactory.Btn(s, "SAIR DA CONTA", 16, ZombitCloud.SignOut,
            new Color(0.30f, 0.09f, 0.10f, 0.92f), "SignOutButton");
        UIFactory.Place((RectTransform)signOut.transform, new Vector2(1f, 1f), new Vector2(-40f, -98f), new Vector2(220f, 36f));
    }

    void RefreshAccountLabel()
    {
        if (accountLabel == null) return;
        if (!ZombitCloud.HasNick) { accountLabel.text = ""; return; }
        string email = string.IsNullOrEmpty(ZombitCloud.Email) ? "" : "\n<size=75%><color=#8999A2>" + ZombitCloud.Email + "</color></size>";
        accountLabel.text = "<color=#FFC04A><b>" + ZombitCloud.Nick.ToUpperInvariant() + "</b></color>" + email;
    }

    // ---------- nick da conta ----------
    static bool IsNickChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-';

    /// <summary>Com conta, o campo de nick so aceita os caracteres permitidos (o servidor valida de novo).</summary>
    void HookNicknameInput()
    {
        if (nicknameInput == null) return;
        nicknameInput.onValidateInput += (text, index, ch) => (!nicknameForAccount || IsNickChar(ch)) ? ch : '\0';
    }

    void OpenAccountNick(string returnScreen, bool first)
    {
        nicknameForAccount = true;
        accountNickFirst = first;
        nicknameForNewGame = false;
        nicknameEditsCurrentSave = false;
        nicknameReturnScreen = returnScreen;
        if (nicknameHelp != null)
            nicknameHelp.text = first
                ? "CRIE O SEU NICK.\nEle é único, fica registrado na sua conta e aparece na campanha.\n" + ZombitCloud.NickRule + "."
                : "ALTERAR O NICK DA CONTA.\nO nick antigo fica livre para outros jogadores.\n" + ZombitCloud.NickRule + ".";
        if (nicknameInput != null)
        {
            nicknameInput.SetTextWithoutNotify(first ? "" : (ZombitCloud.Nick ?? ""));
            Show("nickname");
            nicknameInput.ActivateInputField();
            nicknameInput.caretPosition = nicknameInput.text.Length;
        }
    }

    void SubmitAccountNick(string value)
    {
        if (nickBusy) return;
        if (!ZombitCloud.ValidNick(value))
        {
            Toast("Nick inválido. Use " + ZombitCloud.NickRule + ".");
            return;
        }
        nickBusy = true;
        Toast("Registrando nick...");
        bool first = accountNickFirst;
        string returnScreen = nicknameReturnScreen;
        ZombitCloud.RegisterNick(value, (ok, message) =>
        {
            nickBusy = false;
            if (this == null) return;
            if (!ok) { Toast(message); return; }

            ApplyAccountNickPrefs();
            if (GameSession.Save != null)
            {
                GameSession.Save.playerNickname = value;
                if (!GameSession.DebugRun)
                {
                    try { SaveSystem.Save(GameSession.Save); }
                    catch (System.Exception e) { Debug.LogWarning("Zombit: falha ao gravar o nick no save: " + e.Message); }
                }
            }
            Toast("Nick registrado: " + value);
            if (first) return;   // quando a conta fica pronta, StateChanged leva ao menu principal
            nicknameForAccount = false;
            if (returnScreen == "map") ShowMap(); else Show(returnScreen);
        });
    }

    void OnDestroy()
    {
        ZombitCloud.StateChanged -= OnCloudStateChanged;
    }
}
