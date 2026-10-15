using Godot;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>Always on: a hard shell (or thick hide) makes every hit hurt less.</summary>
public partial class ToughnessAbility : Ability
{
    public ToughnessAbility()
    {
        Slot = AbilitySlot.Passive;
    }

    /// <summary>Damage taken is multiplied by this (0.6 = 40% less).</summary>
    [Export] public float DamageTaken { get; set; } = 0.6f;

    public override float DamageMultiplier(DamageType type) => DamageTaken;

    protected override void Use()
    {
    }
}
