using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThemedSpawner : MonoBehaviour
{
    public Transform hero;
    public Transform[] spawnPoints;
    public string theme;
    public int waveCount = 3;
    public float countMultiplier = 1f;
    public bool hasBoss;
    public float timeBetweenWaves = 4f;
    [Tooltip("Maximo de zumbis vivos ao mesmo tempo. O spawn espera abrir vaga (protege o desempenho).")]
    public int maxAlive = 45;

    [Header("Dificuldade")]
    [Range(0f, 1f), Tooltip("Desativado: hordas vem so da direita. Campo mantido para nao quebrar cenas salvas.")]
    public float flankChance;
    [Tooltip("Elites recebem modificadores (blindado, regenerante, explosivo).")]
    public bool eliteMods;
    [Tooltip("A proxima onda comeca apos N segundos mesmo com zumbis vivos (0 = espera limpar). A ultima onda sempre exige limpar.")]
    public float waveTimeLimit;

    Roster roster;

    IEnumerator Start()
    {
        roster = EnemyRoster.Get(theme);
        if (roster == null || roster.commons.Count == 0)
        {
            Debug.LogError($"Zombit: não há inimigos comuns configurados para o tema '{theme}'.");
            yield break;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("Zombit: ThemedSpawner não tem pontos de nascimento configurados.");
            yield break;
        }
        if (hero == null)
        {
            Debug.LogError("Zombit: ThemedSpawner não recebeu o alvo (herói).");
            yield break;
        }

        for (int w = 0; w < waveCount; w++)
        {
            GameEvents.WaveChanged?.Invoke(w + 1, waveCount);
            float waveStart = Time.time;
            bool lastWave = w == waveCount - 1;

            int commons = Mathf.CeilToInt((8 + 5 * w) * countMultiplier);
            int elites = (w >= 1 && roster.elites.Count > 0) ? Mathf.CeilToInt((0.8f + 0.8f * w) * countMultiplier) : 0;

            var queue = new List<EnemyEntry>();
            for (int i = 0; i < commons; i++) queue.Add(roster.commons[Random.Range(0, roster.commons.Count)]);
            for (int i = 0; i < elites; i++) queue.Add(roster.elites[Random.Range(0, roster.elites.Count)]);
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = queue[i]; queue[i] = queue[j]; queue[j] = tmp;
            }

            float interval = Mathf.Max(0.25f, 0.7f - 0.03f * w);
            foreach (var e in queue)
            {
                // Se a horda ja esta enorme, espera abrir vaga em vez de empilhar mais sprites/scripts.
                while (Enemy.Alive.Count >= maxAlive) yield return null;
                Spawn(e);
                yield return new WaitForSeconds(interval);
            }

            if (hasBoss && roster.boss != null && lastWave)
            {
                yield return new WaitForSeconds(1.5f);
                Spawn(roster.boss);
            }

            // Pressao de tempo: ondas intermediarias avancam apos N s mesmo com zumbis vivos.
            bool timed = waveTimeLimit > 0f && !lastWave;
            while (Enemy.Alive.Count > 0)
            {
                if (timed)
                {
                    float left = waveTimeLimit - (Time.time - waveStart);
                    GameEvents.WaveTimeLeft = Mathf.Max(0f, left);
                    if (left <= 0f) break;
                }
                yield return null;
            }
            GameEvents.WaveTimeLeft = -1f;

            if (!lastWave) yield return new WaitForSeconds(timeBetweenWaves);
        }
        yield return new WaitForSeconds(0.8f);
        GameEvents.MissionComplete?.Invoke();
    }

    void OnDisable() => GameEvents.WaveTimeLeft = -1f;

    void Spawn(EnemyEntry e)
    {
        if (e == null || e.sprite == null || e.data == null) return;
        var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return;

        // Preserve the road's Y baseline. Spread enemies horizontally only; no random vertical offset.
        Vector3 pos = point.position;
        // Nasce sempre logo FORA da borda direita da tela, qualquer que seja a resolucao/proporcao.
        pos.x = Mathf.Max(pos.x, CameraFramer.RightSpawnX(pos.x));
        pos.x += Random.Range(-0.15f, 0.15f);

        // Hordas vem SEMPRE da direita para a esquerda (pontos de spawn em x = 9).
        // O antigo "flanco esquerdo" foi removido: nenhum zumbi nasce atras do heroi.

        var mod = (eliteMods && e.tier == Tier.Elite) ? EnemyFactory.RandomMod() : EliteMod.None;
        EnemyFactory.Spawn(e, pos, hero, mod);
    }
}
