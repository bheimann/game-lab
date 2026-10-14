using System;
using System.Linq;
using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// For classes that switch weapons (the Weapon Master): LB / RB or the ring menu pick the weapon in hand.
/// Each weapon is a set of abilities tagged with its name (see <see cref="Ability.Weapon"/>), e.g. the flail's
/// swing on A and spin attack on X.
/// </summary>
public partial class WeaponSelectorAbility : Ability, IHasOptions
{
    public WeaponSelectorAbility()
    {
        Slot = AbilitySlot.Passive;
    }

    /// <summary>The weapons to pick from, comma-separated, in order.</summary>
    [Export] public string Weapons { get; set; } = "";

    public string OptionsTitle => "Weapons";

    public MenuOption[] Options => WeaponList.Select(w => new MenuOption(w, "Weapon", null, new Color(0.8f, 0.82f, 0.9f))).ToArray();

    public int Selected => Math.Max(0, Array.IndexOf(WeaponList, Hero.CurrentWeapon));

    private string[] WeaponList => Weapons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Select(int index)
    {
        var weapons = WeaponList;
        if (weapons.Length == 0)
        {
            return;
        }

        var weapon = weapons[(index % weapons.Length + weapons.Length) % weapons.Length];
        Hero.SetWeapon(weapon);
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), weapon, Colors.White, 1f, 8f);
    }

    protected override void Use()
    {
    }
}
