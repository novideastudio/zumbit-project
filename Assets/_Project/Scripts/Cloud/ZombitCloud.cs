using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public enum CloudState { Starting, SignedOut, SigningIn, LoadingProfile, NeedsNick, Syncing, Ready, Error }

/// <summary>
/// Conta online do jogador: login com Google (Supabase Auth), nick unico (Postgres) e backup/sincronizacao
/// dos slots de save na nuvem. No WebGL fala com o supabase-js pelo plugin Plugins/WebGL/ZombitSupabase.jslib.
/// No Editor/Windows usa um backend LOCAL de simulacao (conta "Jogador Local"), so para testar o fluxo.
/// </summary>
public class ZombitCloud : MonoBehaviour
{
    public const string NickRule = "3 a 18 caracteres: letras, números, _ . -";
    const float FlushDelaySeconds = 6f;

    public static ZombitCloud I { get; private set; }
    public static CloudState State { get; private set; } = CloudState.Starting;
    public static string Uid { get; private set; }
    public static string Email { get; private set; }
    public static string DisplayName { get; private set; }
    public static string Nick { get; private set; }
    public static string LastError { get; private set; } = "";
    public static event Action StateChanged;

    public static bool IsReady => State == CloudState.Ready;
    public static bool HasNick => !string.IsNullOrEmpty(Nick);
#if UNITY_WEBGL && !UNITY_EDITOR
    public static bool IsLocalBackend => false;
#else
    public static bool IsLocalBackend => true;
#endif

    // ---------- tipos trocados com o JS (JsonUtility) ----------
    [Serializable] class JsResult { public int id; public bool ok; public string data; public string error; }
    [Serializable] class AuthInfo { public bool signedIn; public string uid; public string email; public string name; }
    [Serializable] class ProfileInfo { public string nick; public string nickKey; }
    [Serializable] class NickArgs { public string nick; }
    [Serializable] class PutSaveArgs { public int slot; public string ticks; public string json; public bool deleted; }
    [Serializable] class SaveItem { public int slot; public string ticks; public string json; public bool deleted; }
    [Serializable] class SaveList { public List<SaveItem> items = new List<SaveItem>(); }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void Zb_Call(int id, string op, string arg);
    [DllImport("__Internal")] static extern string Zb_Poll();
#endif

    static int nextId = 1;
    static readonly Dictionary<int, Action<JsResult>> callbacks = new Dictionary<int, Action<JsResult>>();
    static bool backendReady;

    readonly Dictionary<int, PutSaveArgs> pending = new Dictionary<int, PutSaveArgs>();
    float firstDirtyTime = -1f;

    // ---------- ciclo de vida ----------
    public static void Ensure()
    {
        if (I != null) return;
        var go = new GameObject("ZombitCloud");
        DontDestroyOnLoad(go);
        I = go.AddComponent<ZombitCloud>();
        I.StartBackend();
    }

    void StartBackend()
    {
        SetState(CloudState.Starting);
        string cfg = "{}";
#if UNITY_WEBGL && !UNITY_EDITOR
        var ta = Resources.Load<TextAsset>("Config/supabase_config");
        if (ta == null || ta.text.Contains("COLE_AQUI") || ta.text.Contains("SEU-PROJETO"))
        {
            Fail("Supabase ainda não configurado. Preencha Assets/_Project/Resources/Config/supabase_config.json e gere a build de novo.");
            return;
        }
        cfg = ta.text;
#endif
        Call("init", cfg, r =>
        {
            if (!r.ok) { Fail(Friendly(r.error)); return; }
            backendReady = true;   // o estado seguinte vem do evento de autenticacao
        });
    }

    void Update()
    {
        for (int i = 0; i < 16; i++)
        {
            string raw = PollRaw();
            if (string.IsNullOrEmpty(raw)) break;
            Handle(raw);
        }
        if (pending.Count > 0 && firstDirtyTime >= 0f && Time.unscaledTime - firstDirtyTime >= FlushDelaySeconds)
            FlushNow();
    }

    void OnApplicationFocus(bool focus) { if (!focus) FlushNow(); }
    void OnApplicationPause(bool paused) { if (paused) FlushNow(); }

    // ---------- API publica ----------
    public static void SignInWithGoogle()
    {
        if (State != CloudState.SignedOut) return;
        LastError = "";
        SetState(CloudState.SigningIn);
        Call("signIn", "", r =>
        {
            if (r.ok) return;   // o evento de autenticacao assume daqui
            LastError = Friendly(r.error);
            if (State == CloudState.SigningIn) SetState(CloudState.SignedOut);
        });
    }

    public static void SignOut()
    {
        if (I != null) I.FlushNow();
        GameSession.Reset();
        Call("signOut", "", r => { if (!r.ok) Debug.LogWarning("Zombit: falha ao sair da conta: " + r.error); });
    }

    public static void Retry()
    {
        if (I == null) { Ensure(); return; }
        LastError = "";
        if (!backendReady) I.StartBackend();
        else if (!string.IsNullOrEmpty(Uid)) I.LoadProfile();
        else SetState(CloudState.SignedOut);
    }

    public static bool ValidNick(string nick)
    {
        if (string.IsNullOrEmpty(nick) || nick.Length < 3 || nick.Length > 18) return false;
        foreach (char c in nick)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-';
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>Registra o nick (primeira vez) ou troca o nick da conta. O nick e unico (sem diferenciar maiusculas).</summary>
    public static void RegisterNick(string nick, Action<bool, string> done)
    {
        nick = (nick ?? "").Trim();
        if (!ValidNick(nick)) { done?.Invoke(false, Friendly("nick-invalid")); return; }
        if (State != CloudState.Ready && State != CloudState.NeedsNick) { done?.Invoke(false, "Conta ainda não está pronta."); return; }
        Call("setNick", JsonUtility.ToJson(new NickArgs { nick = nick }), r =>
        {
            if (!r.ok) { done?.Invoke(false, Friendly(r.error)); return; }
            bool first = State == CloudState.NeedsNick;
            Nick = nick;
            done?.Invoke(true, "");
            if (first) I.BeginSync();
        });
    }

    // ---------- save na nuvem ----------
    public static void QueueSave(int slot, string json, long ticks)
    {
        if (I == null || !IsReady || string.IsNullOrEmpty(Uid)) return;
        I.pending[slot] = new PutSaveArgs { slot = slot, ticks = ticks.ToString(), json = json, deleted = false };
        if (I.firstDirtyTime < 0f) I.firstDirtyTime = Time.unscaledTime;
    }

    public static void QueueDelete(int slot)
    {
        if (I == null || !IsReady || string.IsNullOrEmpty(Uid)) return;
        I.pending[slot] = new PutSaveArgs { slot = slot, ticks = DateTime.UtcNow.Ticks.ToString(), json = "", deleted = true };
        if (I.firstDirtyTime < 0f) I.firstDirtyTime = Time.unscaledTime;
    }

    public void FlushNow()
    {
        if (pending.Count == 0) { firstDirtyTime = -1f; return; }
        var batch = new List<PutSaveArgs>(pending.Values);
        pending.Clear();
        firstDirtyTime = -1f;
        foreach (var args in batch) PutSave(args);
    }

    void PutSave(PutSaveArgs args)
    {
        Call("putSave", JsonUtility.ToJson(args), r =>
        {
            if (r.ok) return;
            Debug.LogWarning("Zombit: falha ao enviar o slot " + args.slot + " para a nuvem (" + r.error + "). Nova tentativa em instantes.");
            if (!pending.ContainsKey(args.slot)) pending[args.slot] = args;   // um save mais novo, se houver, tem prioridade
            firstDirtyTime = Time.unscaledTime + 10f;
        });
    }

    // ---------- fluxo da conta ----------
    void OnAuth(AuthInfo a)
    {
        if (a == null || !a.signedIn)
        {
            if (State == CloudState.SigningIn) return;   // evento inicial "sem sessao" durante um login em andamento
            Uid = Email = DisplayName = Nick = null;
            pending.Clear();
            SetState(CloudState.SignedOut);
            return;
        }
        bool same = Uid == a.uid;
        bool busyOrDone = State == CloudState.LoadingProfile || State == CloudState.Syncing
                          || State == CloudState.NeedsNick || State == CloudState.Ready;
        if (same && busyOrDone) return;
        Uid = a.uid; Email = a.email; DisplayName = a.name;
        LoadProfile();
    }

    void LoadProfile()
    {
        SetState(CloudState.LoadingProfile);
        Call("getProfile", "", r =>
        {
            if (!r.ok) { Fail(Friendly(r.error)); return; }
            ProfileInfo p = null;
            if (!string.IsNullOrEmpty(r.data)) { try { p = JsonUtility.FromJson<ProfileInfo>(r.data); } catch (Exception) { } }
            if (p == null || string.IsNullOrEmpty(p.nick)) { Nick = null; SetState(CloudState.NeedsNick); return; }
            Nick = p.nick;
            BeginSync();
        });
    }

    /// <summary>Compara os slots locais (desta conta) com os da nuvem; o mais recente (ticks) vence.</summary>
    void BeginSync()
    {
        SetState(CloudState.Syncing);
        SaveSystem.AdoptLegacyFiles();
        Call("getSaves", "", r =>
        {
            if (!r.ok) { Fail(Friendly(r.error)); return; }
            SaveList list = null;
            try { list = JsonUtility.FromJson<SaveList>(r.data); } catch (Exception e) { Debug.LogWarning("Zombit: resposta de saves inválida: " + e.Message); }
            if (list == null || list.items == null) list = new SaveList();

            for (int slot = 1; slot <= SaveSystem.SlotCount; slot++)
            {
                SaveItem c = list.items.Find(x => x.slot == slot);
                string localJson = SaveSystem.ReadRawJson(slot);
                long lt = localJson != null ? SaveSystem.TicksOf(localJson) : 0L;
                if (c == null)
                {
                    if (localJson != null) PutSave(new PutSaveArgs { slot = slot, ticks = lt.ToString(), json = localJson });
                    continue;
                }
                long ct; long.TryParse(c.ticks, out ct);
                if (ct > lt)
                {
                    if (c.deleted) SaveSystem.DeleteLocalOnly(slot);
                    else if (!string.IsNullOrEmpty(c.json)) SaveSystem.ImportFromCloud(slot, c.json);
                }
                else if (lt > ct && localJson != null)
                    PutSave(new PutSaveArgs { slot = slot, ticks = lt.ToString(), json = localJson });
            }
            SetState(CloudState.Ready);
        });
    }

    static void SetState(CloudState s)
    {
        State = s;
        try { StateChanged?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static void Fail(string message)
    {
        LastError = message;
        SetState(CloudState.Error);
    }

    // ---------- ponte JS ----------
    static void Call(string op, string argJson, Action<JsResult> cb)
    {
        int id = nextId++;
        if (cb != null) callbacks[id] = cb;
#if UNITY_WEBGL && !UNITY_EDITOR
        Zb_Call(id, op, argJson ?? "");
#else
        LocalBackend.Handle(id, op, argJson ?? "");
#endif
    }

    static string PollRaw()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return Zb_Poll();
#else
        return LocalBackend.Queue.Count > 0 ? LocalBackend.Queue.Dequeue() : null;
#endif
    }

    void Handle(string raw)
    {
        try
        {
            var res = JsonUtility.FromJson<JsResult>(raw);
            if (res == null) return;
            if (res.id == 0)
            {
                OnAuth(JsonUtility.FromJson<AuthInfo>(res.data));
                return;
            }
            if (callbacks.TryGetValue(res.id, out var cb))
            {
                callbacks.Remove(res.id);
                cb(res);
            }
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    static string Friendly(string code)
    {
        switch (code ?? "")
        {
            case "network": return "Sem conexão com a internet.";
            case "script-load-failed": return "Não foi possível carregar o Supabase (internet ou bloqueador de scripts).";
            case "nick-taken": return "Esse nick já está em uso. Escolha outro.";
            case "nick-invalid": return "Nick inválido. Use " + NickRule + ".";
            case "permission-denied": return "Sem permissão no banco. Rode o supabase_setup.sql no SQL Editor do Supabase.";
            case "tables-missing": return "Tabelas não encontradas. Rode o supabase_setup.sql no SQL Editor do Supabase.";
            default: return "Erro inesperado (" + code + ").";
        }
    }

    // ======================================================================================
    // Backend LOCAL: simula Auth + Firestore no Editor/Windows para testar o fluxo sem internet.
    // ======================================================================================
#if !(UNITY_WEBGL && !UNITY_EDITOR)
    static class LocalBackend
    {
        public static readonly Queue<string> Queue = new Queue<string>();
        const string UidKey = "zbl_uid";
        const string RegistryKey = "zbl_nicks";

        [Serializable] class NickDb { public List<string> keys = new List<string>(); public List<string> uids = new List<string>(); }

        static void Push(int id, bool ok, string data, string error = "")
            => Queue.Enqueue(JsonUtility.ToJson(new JsResult { id = id, ok = ok, data = data ?? "", error = error }));

        static void EmitAuth()
        {
            string uid = PlayerPrefs.GetString(UidKey, "");
            var a = new AuthInfo { signedIn = uid.Length > 0, uid = uid, email = uid.Length > 0 ? "teste@local" : "", name = uid.Length > 0 ? "Jogador Local" : "" };
            Push(0, true, JsonUtility.ToJson(a));
        }

        static string CloudDir()
        {
            string d = Path.Combine(Application.persistentDataPath, "cloudsim");
            Directory.CreateDirectory(d);
            return d;
        }

        public static void Handle(int id, string op, string arg)
        {
            string uid = PlayerPrefs.GetString(UidKey, "");
            try
            {
                switch (op)
                {
                    case "init": Push(id, true, ""); EmitAuth(); break;
                    case "signIn": PlayerPrefs.SetString(UidKey, "local-test"); PlayerPrefs.Save(); Push(id, true, ""); EmitAuth(); break;
                    case "signOut": PlayerPrefs.DeleteKey(UidKey); PlayerPrefs.Save(); Push(id, true, ""); EmitAuth(); break;
                    case "getProfile":
                    {
                        string n = PlayerPrefs.GetString("zbl_profile_" + uid, "");
                        Push(id, true, n.Length == 0 ? "" : JsonUtility.ToJson(new ProfileInfo { nick = n, nickKey = n.ToLowerInvariant() }));
                        break;
                    }
                    case "setNick":
                    {
                        string nick = (JsonUtility.FromJson<NickArgs>(arg).nick ?? "").Trim();
                        if (!ValidNick(nick)) { Push(id, false, "", "nick-invalid"); break; }
                        string key = nick.ToLowerInvariant();
                        var db = JsonUtility.FromJson<NickDb>(PlayerPrefs.GetString(RegistryKey, "{}")) ?? new NickDb();
                        int at = db.keys.IndexOf(key);
                        if (at >= 0 && db.uids[at] != uid) { Push(id, false, "", "nick-taken"); break; }
                        for (int i = db.keys.Count - 1; i >= 0; i--)
                            if (db.uids[i] == uid) { db.keys.RemoveAt(i); db.uids.RemoveAt(i); }
                        db.keys.Add(key); db.uids.Add(uid);
                        PlayerPrefs.SetString(RegistryKey, JsonUtility.ToJson(db));
                        PlayerPrefs.SetString("zbl_profile_" + uid, nick);
                        PlayerPrefs.Save();
                        Push(id, true, JsonUtility.ToJson(new ProfileInfo { nick = nick, nickKey = key }));
                        break;
                    }
                    case "putSave":
                    {
                        var a = JsonUtility.FromJson<PutSaveArgs>(arg);
                        File.WriteAllText(Path.Combine(CloudDir(), uid + "_" + a.slot + ".json"), arg);
                        Push(id, true, "");
                        break;
                    }
                    case "getSaves":
                    {
                        var list = new SaveList();
                        for (int s = 1; s <= SaveSystem.SlotCount; s++)
                        {
                            string p = Path.Combine(CloudDir(), uid + "_" + s + ".json");
                            if (!File.Exists(p)) continue;
                            var a = JsonUtility.FromJson<PutSaveArgs>(File.ReadAllText(p));
                            list.items.Add(new SaveItem { slot = a.slot, ticks = a.ticks, json = a.json, deleted = a.deleted });
                        }
                        Push(id, true, JsonUtility.ToJson(list));
                        break;
                    }
                    default: Push(id, false, "", "unknown-op"); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Zombit (backend local): " + e.Message);
                Push(id, false, "", "local-error");
            }
        }
    }
#endif
}
