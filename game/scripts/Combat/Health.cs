using System;

namespace LizardWizard.Combat;

/// <summary>Hit points for anything that can be hurt.</summary>
public sealed class Health
{
    public Health(int max)
    {
        Max = max;
        Current = max;
    }

    public event Action? Changed;

    public int Max { get; private set; }

    public int Current { get; private set; }

    public bool IsEmpty => Current <= 0;

    /// <summary>Removes up to <paramref name="amount"/> HP and returns how much was actually removed.</summary>
    public int Damage(int amount)
    {
        var dealt = Math.Clamp(amount, 0, Current);
        Current -= dealt;
        Changed?.Invoke();
        return dealt;
    }

    /// <summary>Adds up to <paramref name="amount"/> HP (never above Max) and returns how much was actually added.</summary>
    public int Heal(int amount)
    {
        var healed = Math.Clamp(amount, 0, Max - Current);
        Current += healed;
        Changed?.Invoke();
        return healed;
    }

    /// <summary>Changes the maximum (armor); current HP is kept, but never above the new maximum.</summary>
    public void SetMax(int max)
    {
        Max = Math.Max(1, max);
        Current = Math.Min(Current, Max);
        Changed?.Invoke();
    }

    public void Refill()
    {
        Current = Max;
        Changed?.Invoke();
    }
}
