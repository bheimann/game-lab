using Godot;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>A button that steps another ability's choice to the next one, e.g. Y changes the bow's arrow type.</summary>
public partial class CycleOptionsAbility : Ability
{
    public CycleOptionsAbility()
    {
        Slot = AbilitySlot.Special;
        CooldownSeconds = 0.15f;
    }

    /// <summary>The sibling ability whose choice this changes.</summary>
    [Export] public NodePath? Target { get; set; }

    protected override void Use()
    {
        if (Target is not null && GetNodeOrNull(Target) is IHasOptions options)
        {
            options.Select(options.Selected + 1);
        }
    }
}
