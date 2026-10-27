// Ponte Unity WebGL <-> Supabase (Auth com Google + Postgres), usando supabase-js via CDN.
// Comunicacao: C# chama Zb_Call(id, op, argJson) e le as respostas com Zb_Poll() (fila de mensagens JSON).
// Escrito em ES5 de proposito (o pre-processador de jslib do Emscripten e sensivel a sintaxe nova).
var ZombitSupabasePlugin = {
  $ZB: {
    q: [],
    started: false,
    sb: null,
    user: null,
    SDK_URL: 'https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2.45.4/dist/umd/supabase.js',

    // A regex fica dentro de uma funcao: o Emscripten serializa o objeto $ZB como texto, e valores RegExp se perdem.
    validNick: function (n) { return /^[A-Za-z0-9_.\-]{3,18}$/.test(n); },

    push: function (id, ok, data, error) {
      var d = '';
      if (data !== undefined && data !== null) d = (typeof data === 'string') ? data : JSON.stringify(data);
      ZB.q.push(JSON.stringify({ id: id, ok: ok, data: d, error: error || '' }));
    },

    // Converte erros do supabase-js / Postgres em codigos curtos que o C# traduz.
    fail: function (id, e) {
      var code = '';
      if (e) {
        if (e.code === '23505') code = 'nick-taken';                         // violacao de unique (nick_key)
        else if (e.code === '23514') code = 'nick-invalid';                  // check constraint
        else if (e.code === '42501') code = 'permission-denied';             // RLS
        else if (e.code === 'PGRST205' || e.code === '42P01') code = 'tables-missing';
        else if (e.message && /failed to fetch|networkerror|load failed/i.test(e.message)) code = 'network';
        else code = e.code || e.message || String(e);
      } else code = 'unknown';
      console.warn('[Zombit] ' + code + (e && e.message ? ' - ' + e.message : ''));
      ZB.push(id, false, '', String(code));
    },

    emitAuth: function () {
      var u = ZB.user;
      ZB.push(0, true, {
        signedIn: !!u,
        uid: u ? u.id : '',
        email: u ? (u.email || '') : '',
        name: u && u.user_metadata ? (u.user_metadata.full_name || u.user_metadata.name || '') : ''
      });
    },

    loadScript: function (url) {
      return new Promise(function (resolve, reject) {
        var s = document.createElement('script');
        s.src = url;
        s.async = false;
        s.onload = function () { resolve(); };
        s.onerror = function () { reject({ code: 'script-load-failed', message: url }); };
        document.head.appendChild(s);
      });
    },

    init: function (id, cfg) {
      if (ZB.sb) { ZB.push(id, true, ''); ZB.emitAuth(); return; }
      if (ZB.started) { ZB.fail(id, { code: 'busy' }); return; }
      ZB.started = true;
      ZB.loadScript(ZB.SDK_URL)
        .then(function () {
          ZB.sb = window.supabase.createClient(cfg.url, cfg.anonKey, {
            auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true, flowType: 'implicit' }
          });
          // Dispara tambem no INITIAL_SESSION (sessao restaurada ou vinda do retorno do Google).
          ZB.sb.auth.onAuthStateChange(function (event, session) {
            ZB.user = session ? session.user : null;
            ZB.emitAuth();
          });
          ZB.push(id, true, '');
        })
        .catch(function (e) { ZB.started = false; ZB.fail(id, e); });
    },

    // O Google abre na mesma aba e volta para esta pagina (a sessao e lida na volta). Pagina recarrega.
    signIn: function (id) {
      ZB.sb.auth.signInWithOAuth({
        provider: 'google',
        options: {
          redirectTo: window.location.origin + window.location.pathname,
          queryParams: { prompt: 'select_account' }
        }
      }).then(function (r) {
        if (r && r.error) ZB.fail(id, r.error); else ZB.push(id, true, '');
      }).catch(function (e) { ZB.fail(id, e); });
    },

    uid: function () { return ZB.user ? ZB.user.id : null; },

    getProfile: function (id) {
      var uid = ZB.uid();
      if (!uid) { ZB.fail(id, { code: 'not-signed-in' }); return; }
      ZB.sb.from('profiles').select('nick,nick_key').eq('uid', uid).maybeSingle()
        .then(function (r) {
          if (r.error) { ZB.fail(id, r.error); return; }
          if (!r.data) { ZB.push(id, true, ''); return; }
          ZB.push(id, true, { nick: r.data.nick || '', nickKey: r.data.nick_key || '' });
        })
        .catch(function (e) { ZB.fail(id, e); });
    },

    // Registra ou troca o nick. A unicidade e garantida pela constraint UNIQUE em profiles.nick_key (lower(nick)).
    setNick: function (id, arg) {
      var uid = ZB.uid();
      if (!uid) { ZB.fail(id, { code: 'not-signed-in' }); return; }
      var nick = (arg && arg.nick ? String(arg.nick) : '').trim();
      if (!ZB.validNick(nick)) { ZB.fail(id, { code: '23514' }); return; }
      ZB.sb.from('profiles')
        .upsert({ uid: uid, nick: nick, updated_at: new Date().toISOString() }, { onConflict: 'uid' })
        .select('nick,nick_key').single()
        .then(function (r) {
          if (r.error) { ZB.fail(id, r.error); return; }
          ZB.push(id, true, { nick: r.data.nick, nickKey: r.data.nick_key });
        })
        .catch(function (e) { ZB.fail(id, e); });
    },

    putSave: function (id, arg) {
      var uid = ZB.uid();
      if (!uid) { ZB.fail(id, { code: 'not-signed-in' }); return; }
      ZB.sb.from('saves')
        .upsert({
          uid: uid, slot: arg.slot, ticks: String(arg.ticks), json: arg.json || '',
          deleted: !!arg.deleted, updated_at: new Date().toISOString()
        }, { onConflict: 'uid,slot' })
        .then(function (r) {
          if (r.error) ZB.fail(id, r.error); else ZB.push(id, true, '');
        })
        .catch(function (e) { ZB.fail(id, e); });
    },

    getSaves: function (id) {
      var uid = ZB.uid();
      if (!uid) { ZB.fail(id, { code: 'not-signed-in' }); return; }
      ZB.sb.from('saves').select('slot,ticks,json,deleted').eq('uid', uid)
        .then(function (r) {
          if (r.error) { ZB.fail(id, r.error); return; }
          var items = [];
          for (var i = 0; i < r.data.length; i++) {
            var d = r.data[i];
            items.push({ slot: d.slot, ticks: String(d.ticks || '0'), json: d.json || '', deleted: !!d.deleted });
          }
          ZB.push(id, true, { items: items });
        })
        .catch(function (e) { ZB.fail(id, e); });
    },

    dispatch: function (id, op, argStr) {
      var arg = {};
      try { arg = argStr ? JSON.parse(argStr) : {}; } catch (e) { arg = {}; }
      try {
        if (op !== 'init' && !ZB.sb) { ZB.fail(id, { code: 'not-ready' }); return; }
        switch (op) {
          case 'init': ZB.init(id, arg); break;
          case 'signIn': ZB.signIn(id); break;
          case 'signOut':
            ZB.sb.auth.signOut().then(function () { ZB.push(id, true, ''); }).catch(function (e) { ZB.fail(id, e); });
            break;
          case 'getProfile': ZB.getProfile(id); break;
          case 'setNick': ZB.setNick(id, arg); break;
          case 'putSave': ZB.putSave(id, arg); break;
          case 'getSaves': ZB.getSaves(id); break;
          default: ZB.fail(id, { code: 'unknown-op' });
        }
      } catch (e) { ZB.fail(id, e); }
    }
  },

  Zb_Call: function (id, opPtr, argPtr) {
    ZB.dispatch(id, UTF8ToString(opPtr), UTF8ToString(argPtr));
  },

  // Devolve a proxima mensagem da fila (string JSON) ou 0 (null) se estiver vazia.
  Zb_Poll: function () {
    if (ZB.q.length === 0) return 0;
    var s = ZB.q.shift();
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  }
};

autoAddDeps(ZombitSupabasePlugin, '$ZB');
mergeInto(LibraryManager.library, ZombitSupabasePlugin);
