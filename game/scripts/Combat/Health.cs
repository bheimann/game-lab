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

    public int Max { get; }

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

    public void Refill()
    {
        Current = Max;
        Changed?.Invoke();
    }
}
