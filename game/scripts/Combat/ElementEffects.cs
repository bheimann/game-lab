using Godot;

namespace LizardWizard.Combat;

/// <summary>
/// What elemental hits do to an enemy, besides their damage:
/// poison and fire hurt over time, ice slows it down, and lightning and arcane hit much harder.
/// Each enemy keeps one of these and ticks it every physics frame.
/// </summary>
public sealed class ElementEffects
{
    /// <summary>Lightning and arcane hits do this many times the damage.</summary>
    public const float StrongMultiplier = 2f;

    private const float PoisonSeconds = 3f;
    private const float BurnSeconds = 2f;
    private const float ChillSeconds = 2f;
    private const float TickSeconds = 0.5f;
    private const float ChillSpeed = 0.5f;

    private float _poisonLeft;
    private float _burnLeft;
    private float _chillLeft;
    private float _tickTimer;

    public bool IsPoisoned => _poisonLeft > 0;

    public bool IsBurning => _burnLeft > 0;

    public bool IsChilled => _chillLeft > 0;

    /// <summary>Multiply the enemy's walking speed by this (ice slows it).</summary>
    public float SpeedFactor => IsChilled ? ChillSpeed : 1f;

    /// <summary>A colour to tint the enemy with while an effect is on it, or null.</summary>
    public Color? Tint =>
        IsBurning ? new Color(1f, 0.6f, 0.4f)
        : IsPoisoned ? new Color(0.65f, 1f, 0.5f)
        : IsChilled ? new Color(0.6f, 0.85f, 1f)
        : null;

    /// <summary>The damage a hit of this type really does (lightning and arcane hit harder).</summary>
    public static int Boost(int damage, DamageType type) =>
        type is DamageType.Lightning or DamageType.Arcane ? Mathf.RoundToInt(damage * StrongMultiplier) : damage;

    /// <summary>Start (or restart) the effect of a hit of this type.</summary>
    public void Hit(DamageType type)
    {
        switch (type)
        {
            case DamageType.Poison:
                _poisonLeft = PoisonSeconds;
                break;
            case DamageType.Fire:
                _burnLeft = BurnSeconds;
                break;
            case DamageType.Ice:
                _chillLeft = ChillSeconds;
                break;
        }
    }

    /// <summary>Advance the effects. Returns the damage-over-time to deal this frame (usually 0).</summary>
    public int Tick(float dt)
    {
        _chillLeft -= dt;
        if (!IsPoisoned && !IsBurning)
        {
            _tickTimer = 0;
            return 0;
        }

        _tickTimer += dt;
        var damage = 0;
        if (_tickTimer >= TickSeconds)
        {
            _tickTimer -= TickSeconds;
            damage = (IsPoisoned ? 1 : 0) + (IsBurning ? 1 : 0);
        }

        _poisonLeft -= dt;
        _burnLeft -= dt;
        return damage;
    }

    /// <summary>Remove every effect (the enemy fell asleep or started over).</summary>
    public void Clear()
    {
        _poisonLeft = _burnLeft = _chillLeft = _tickTimer = 0;
    }
}
