using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    // Defesa com retorno decrescente: reducao = defesa / (defesa + DefenseHalfPoint).
    // Antes era subtracao fixa (dano - defesa, minimo 1), o que tornava o heroi quase imune
    // com poucos itens. Ponha UsePercentDefense = false para voltar ao comportamento antigo.
    public static bool UsePercentDefense = true;
    public const float DefenseHalfPoint = 40f; // defesa necessaria para reduzir 50% do dano

    public float max = 100f;
    public float defense;
    public float Current { get; private set; }
    public event Action<float, float> Changed;
    public event Action Died;

    void Awake() => Current = max;

    public void Init(float maxHealth)
    {
        max = maxHealth;
        Current = max;
        Changed?.Invoke(Current, max);
    }

    /// <summary>
    /// Ajusta vida maxima E atual sem forcar cura total. Usado ao recalcular atributos (equipar item,
    /// subir de nivel). Antes isso era feito com Init(max * razao), que REDUZIA a vida maxima.
    /// </summary>
    public void SetValues(float maxHealth, float current)
    {
        max = Mathf.Max(1f, maxHealth);
        Current = Mathf.Clamp(current, 0f, max);
        Changed?.Invoke(Current, max);
    }

    public void Heal(float amount)
    {
        if (Current <= 0f || Current >= max) return;
        Current = Mathf.Min(max, Current + amount);
        Changed?.Invoke(Current, max);
    }

    public void Take(float amount)
    {
        if (Current <= 0f) return;

        float reduced = UsePercentDefense
            ? amount * (1f - Mathf.Max(0f, defense) / (Mathf.Max(0f, defense) + DefenseHalfPoint))
            : amount - defense;
        float appliedDamage = Mathf.Min(Current, Mathf.Max(1f, reduced));
        Current = Mathf.Max(0f, Current - appliedDamage);

        Changed?.Invoke(Current, max);
        if (Current <= 0f) Died?.Invoke();
    }
}
