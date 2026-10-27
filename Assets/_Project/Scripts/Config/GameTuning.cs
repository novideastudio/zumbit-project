using UnityEngine;

/// <summary>
/// Ajustes de tamanho e posicao do combate, editaveis no Inspector sem mexer em codigo.
/// Asset: Assets/_Project/Resources/Config/GameTuning.asset (menu Zombit/Create Config Assets recria se faltar).
/// Unidades de mundo: o sobrevivente tem ~2 de altura; a camera mostra ~10 de altura.
/// </summary>
[CreateAssetMenu(menuName = "Zombit/Game Tuning", fileName = "GameTuning")]
public class GameTuning : ScriptableObject
{
    [Header("Zumbis (altura VISIVEL do sprite; sobrevivente = 2)")]
    [Min(0.1f), Tooltip("Multiplica TODOS os zumbis de uma vez (1 = padrao, 0.9 = 10% menores).")]
    public float zombieSizeMultiplier = 1f;
    [Min(0.1f)] public float commonHeight = 2.0f;
    [Min(0.1f)] public float eliteHeight = 2.2f;
    [Min(0.1f)] public float bossHeight = 3.2f;

    [Header("Horda continua (substitui as waves)")]
    [Tooltip("Ligado: a missao e uma horda continua (sem pausa entre waves). Desligado: volta ao modo antigo de waves.")]
    public bool hordeMode = true;
    [Min(10f), Tooltip("Duracao de cada FASE em segundos. A missao dura (waves do local) x isto. Ex.: 3 waves = 150 s.")]
    public float hordePhaseSeconds = 50f;
    [Min(0.1f), Tooltip("Zumbis por segundo no inicio (antes dos multiplicadores de local, nivel e esquadrao).")]
    public float hordeBaseRate = 0.9f;
    [Min(0f), Tooltip("Crescimento da taxa por segundo: taxa = base x (1 + isto x tempo). 0,012 => 2,8x aos 150 s.")]
    public float hordeRampPerSecond = 0.012f;
    [Min(10), Tooltip("Maximo de zumbis vivos (performance). A ameaca vem da vida/dano, nao de empilhar sprites.")]
    public int hordeMaxAlive = 55;

    [Header("Torreta do Engineer")]
    [Min(0.1f), Tooltip("Altura da torreta. 0.5 = do chao ate a canela do sobrevivente.")]
    public float turretHeight = 0.5f;
    [Tooltip("Posicao dos PES da torreta em relacao aos pes do Engineer (x negativo = atras/esquerda, y negativo = mais para baixo na rua).")]
    public Vector2 turretOffset = new Vector2(-0.65f, -0.12f);

    [Header("Formacao em cascata")]
    [Tooltip("Altura dos pes do PRIMEIRO sobrevivente em relacao a linha da rua (WaveBackground.GroundY). Mais negativo = mais para baixo.")]
    public float laneOffsetY = -0.55f;
    [Min(0f), Tooltip("Cada sobrevivente seguinte fica este tanto mais baixo que o anterior.")]
    public float cascadeStepY = 0.30f;
    [Tooltip("Dupla: X do primeiro / distancia entre eles.")]
    public float duoFrontX = -6.5f;
    [Min(0f)] public float duoStepX = 1.35f;
    [Tooltip("Trio: X do primeiro / distancia entre eles.")]
    public float trioFrontX = -5.8f;
    [Min(0f)] public float trioStepX = 1.15f;
    [Tooltip("Ajuste fino da pista dos zumbis (alem de centralizar na cascata). Negativo = mais para baixo.")]
    public float zombieLaneExtraY = 0f;

    /// <summary>Y dos pes do sobrevivente na posicao n da cascata (0 = frente).</summary>
    public float HeroFeetY(int slot) => WaveBackground.GroundY + laneOffsetY - cascadeStepY * slot;

    /// <summary>Y dos pes dos zumbis: no meio da cascata, para a horda ficar centrada em relacao ao grupo.</summary>
    public float ZombieFeetY(int squadSize) =>
        WaveBackground.GroundY + laneOffsetY - cascadeStepY * Mathf.Max(0, squadSize - 1) * 0.5f + zombieLaneExtraY;

    static GameTuning current;

    public static GameTuning Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<GameTuning>("Config/GameTuning");
                if (current == null)
                {
                    current = CreateInstance<GameTuning>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return current;
        }
    }

    public static void Reload() => current = null;
}
