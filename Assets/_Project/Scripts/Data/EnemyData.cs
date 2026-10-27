using UnityEngine;

/// <summary>Comportamento do zumbi. Auto = deduzido pelo nome (cao/corredor = Runner, gordo/blindado = Tank, vomitador/atirador = Spitter).</summary>
public enum EnemyKind { Auto, Walker, Runner, Tank, Spitter }

[CreateAssetMenu(menuName = "Zombit/Enemy")]
public class EnemyData : ScriptableObject
{
    public string displayName;

    [Header("Visual")]
    public Sprite sprite;
    public Tier tier;
    [Tooltip("Cenario a que pertence (escola, delegacia, hospital, base, centro)")] public string theme;
    [Tooltip("Altura FINAL do zumbi no jogo, em unidades (a mesma escala do heroi: 2.6). 0 = automatico pelo tier " +
             "(comum 2.4 / elite 2.75 / chefe 4.0). Use valores menores para caes e criaturas rasteiras.")]
    public float targetHeight = 0f;
    [Tooltip("Multiplicador fino sobre a altura (ex.: 1.1 deixa 10% maior)")] public float sizeMult = 1f;

    [Header("Comportamento")]
    [Tooltip("Auto = deduz pelo nome. Runner: ate +2 batem alem do limite da fila. Tank: -20% de dano recebido e resiste a empurrao. Spitter: ataca de longe e ignora a fila.")]
    public EnemyKind kind = EnemyKind.Auto;
    [Range(3f, 5.2f), Tooltip("Spitter: distancia do heroi de onde ele atira (a menor arma do jogo alcanca 6).")]
    public float attackRange = 4.8f;

    [Header("Atributos")]
    public float maxHealth = 30f;
    public float moveSpeed = 1.2f;
    public float damage = 5f;
    public float attackInterval = 1f;
    public int xp = 5;
    public int gold = 2;
    [Range(0f, 1f)] public float dropChance = 0.15f;
}
