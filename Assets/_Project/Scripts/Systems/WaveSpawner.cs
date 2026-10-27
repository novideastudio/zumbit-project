using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    public WaveData[] waves;
    public Transform hero;
    public Transform[] spawnPoints;
    public float timeBetweenWaves = 4f;
    public int waveCount = 0;
    public float countMultiplier = 1f;
    [Tooltip("Desativado: hordas vem so da direita. Campo mantido para nao quebrar cenas salvas.")]
    [Range(0f, 1f)] public float flankChance;
    [Tooltip("A proxima onda comeca apos N segundos mesmo com zumbis vivos (0 = espera limpar).")]
    public float waveTimeLimit;

    IEnumerator Start()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("Zombit: WaveSpawner não tem waves configuradas.");
            yield break;
        }
        if (spawnPoints == null || spawnPoints.Length == 0 || hero == null)
        {
            Debug.LogError("Zombit: WaveSpawner precisa de um herói e pelo menos um ponto de nascimento.");
            yield break;
        }

        int total = waveCount > 0 ? waveCount : waves.Length;
        for (int w = 0; w < total; w++)
        {
            GameEvents.WaveChanged?.Invoke(w + 1, total);
            float waveStart = Time.time;
            var wave = waves[w % waves.Length];
            float ramp = 1f + 0.12f * w;
            foreach (var g in wave.groups)
            {
                int count = Mathf.CeilToInt(g.count * countMultiplier * ramp);
                for (int i = 0; i < count; i++)
                {
                    Spawn(g.enemyPrefab);
                    yield return new WaitForSeconds(g.interval);
                }
            }
            bool timed = waveTimeLimit > 0f && w < total - 1;
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
            if (w < total - 1) yield return new WaitForSeconds(timeBetweenWaves);
        }
        GameEvents.MissionComplete?.Invoke();
    }

    void Spawn(GameObject prefab)
    {
        if (prefab == null) return;
        var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (p == null) return;

        Vector3 pos = p.position;
        pos.x = Mathf.Max(pos.x, CameraFramer.RightSpawnX(pos.x)); // logo fora da borda direita
        pos.x += Random.Range(-0.15f, 0.15f); // never randomize lane height
        // Hordas vem sempre da direita (sem flanco esquerdo).
        var go = Instantiate(prefab, pos, Quaternion.identity);
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.target = hero;
            // Do not depend on an Enemy helper method here: this keeps the spawner
            // compatible with projects where Unity has not refreshed the Enemy script yet.
            AlignSpriteFeetToGround(go.transform, p.position.y);
        }
    }

    static void AlignSpriteFeetToGround(Transform character, float groundY)
    {
        if (character == null) return;
        var renderer = character.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        Vector3 position = character.position;
        position.y += groundY - renderer.bounds.min.y;
        character.position = position;
    }
}
