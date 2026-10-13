using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>Which button uses an ability.</summary>
public enum AbilitySlot
{
    /// <summary>A on a controller, J on the keyboard.</summary>
    Primary,

    /// <summary>X on a controller, K on the keyboard.</summary>
    Secondary,

    /// <summary>Y on a controller, L on the keyboard.</summary>
    Special,

    /// <summary>No button: always on (e.g. camouflage).</summary>
    Passive,
}

/// <summary>
/// One thing a hero can do. Abilities are child nodes of a <see cref="Hero"/> scene, so a character is
/// just a hero plus a set of abilities, and every number can be tuned in the inspector.
/// Changing class (painting, later) means swapping the ability nodes.
/// </summary>
public abstract partial class Ability : Node2D
{
    [Export] public AbilitySlot Slot { get; set; }

    [Export] public float CooldownSeconds { get; set; } = 0.3f;

    public float CooldownLeft { get; protected set; }

    /// <summary>True while the ability is in progress; the hero can't start another one meanwhile.</summary>
    public virtual bool IsActive => false;

    /// <summary>How fast the hero can walk while this ability is active (0 = rooted, 1 = full speed).</summary>
    public virtual float MoveFactor => 1f;

    /// <summary>Hides the hero from enemies (camouflage).</summary>
    public virtual bool HidesHero => false;

    /// <summary>Multiplied into the hero's sprite alpha (camouflage fades the hero out).</summary>
    public virtual float SpriteAlpha => 1f;

    protected Hero Hero { get; private set; } = null!;

    public override void _Ready()
    {
        Hero = GetParent<Hero>();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        CooldownLeft = Mathf.Max(0, CooldownLeft - dt);
        Tick(dt);
    }

    /// <summary>Called when the ability's button is pressed. Returns false if it's cooling down or the hero is busy.</summary>
    public bool TryUse()
    {
        if (Slot == AbilitySlot.Passive || CooldownLeft > 0 || !Hero.CanStartAbility)
        {
            return false;
        }

        CooldownLeft = CooldownSeconds;
        Hero.NotifyAction();
        Use();
        return true;
    }

    /// <summary>Damage taken of this type is multiplied by this (resistances).</summary>
    public virtual float DamageMultiplier(DamageType type) => 1f;

    /// <summary>Stop immediately (the hero was hit or knocked out).</summary>
    public virtual void Cancel()
    {
    }

    protected abstract void Use();

    protected virtual void Tick(float dt)
    {
    }
}
