using System.Linq;
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

    /// <summary>B on a controller, I / M on the keyboard (normally "eat a herb"; a class that uses it eats herbs
    /// from the ring menu instead).</summary>
    Item,
}

/// <summary>
/// One thing a hero can do. Abilities are child nodes of a <see cref="Hero"/> scene, so a character is
/// just a hero plus a set of abilities, and every number can be tuned in the inspector.
/// A character scene holds the abilities of all its classes; <see cref="Classes"/> says which class each belongs
/// to, and getting painted switches which ones are on.
/// </summary>
public abstract partial class Ability : Node2D
{
    [Export] public AbilitySlot Slot { get; set; }

    /// <summary>Which classes have this ability, comma-separated (e.g. "Chameleon, Spitter"). Empty = every class.</summary>
    [Export] public string Classes { get; set; } = "";

    /// <summary>Which weapon this ability comes with (e.g. "Flail"), for classes that switch weapons
    /// (see <see cref="WeaponSelectorAbility"/>). Empty = whatever the weapon.</summary>
    [Export] public string Weapon { get; set; } = "";

    [Export] public float CooldownSeconds { get; set; } = 0.3f;

    public float CooldownLeft { get; protected set; }

    /// <summary>True while the ability is in progress; the hero can't start another one meanwhile.</summary>
    public virtual bool IsActive => false;

    /// <summary>How fast the hero can walk while this ability is active (0 = rooted, 1 = full speed).</summary>
    public virtual float MoveFactor => 1f;

    /// <summary>While active: the hero moves at this velocity (pixels per second) instead of walking (a roll, a grapple
    /// zip). Null = walk normally.</summary>
    public virtual Vector2? Motion => null;

    /// <summary>While active: the hero can pass over pits and water (flying along a grapple line).</summary>
    public virtual bool CrossesPits => false;

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

        CooldownLeft = CooldownSeconds * (Slot == AbilitySlot.Primary ? Hero.Fused?.Part.CooldownFactor ?? 1f : 1f);
        Hero.NotifyAction();
        Use();
        return true;
    }

    public bool BelongsTo(string className) =>
        string.IsNullOrWhiteSpace(Classes) || Classes.Split(',').Any(c => c.Trim() == className);

    public bool GoesWith(string weapon) => Weapon == "" || Weapon == weapon;

    /// <summary>Does this ability stop a hit coming from <paramref name="from"/> right now (a raised shield)?</summary>
    public virtual bool Blocks(Vector2 from) => false;

    /// <summary>The hero got painted into a class that has this ability.</summary>
    public virtual void Activate()
    {
    }

    /// <summary>The hero got painted into a class without this ability: undo any lasting effects (tints etc.).</summary>
    public virtual void Deactivate()
    {
    }

    /// <summary>Damage taken of this type is multiplied by this (resistances).</summary>
    public virtual float DamageMultiplier(DamageType type) => 1f;

    /// <summary>Stop immediately (the hero was hit or knocked out).</summary>
    public virtual void Cancel()
    {
    }

    /// <summary>Extra damage for the hero's main attack (only A-button abilities): the team's weapon material
    /// (<see cref="Items.WeaponTier"/>) plus any monster part fused onto it.</summary>
    protected int BonusDamage => Slot == AbilitySlot.Primary ? Items.WeaponTier.Current.ExtraDamage + (Hero.Fused?.Part.ExtraDamage ?? 0) : 0;

    /// <summary>The element hits do: a fused part's element (frog skin: poison) on A-button abilities, otherwise <paramref name="normal"/>.</summary>
    protected DamageType FusedElement(DamageType normal) =>
        Slot == AbilitySlot.Primary && Hero.Fused?.Part.Element is { } element ? element : normal;

    protected abstract void Use();

    protected virtual void Tick(float dt)
    {
    }
}
