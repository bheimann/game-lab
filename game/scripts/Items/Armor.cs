using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Combat;

namespace LizardWizard.Items;

/// <summary>
/// A piece of armor for the hero's one armor slot. Armor changes how a hero's powers work
/// (e.g. the shadow cloak lets a chameleon stay hidden while walking).
/// For now everyone owns every piece that fits them; shops and chests come later.
/// </summary>
public sealed record Armor(string Name, string Description, string IconPath)
{
    public static readonly Armor[] All =
    {
        new("Shadow Cloak", "Stay hidden while you walk", "res://assets/sprites/icons/shadow_cloak.png")
        {
            HideWhileMoving = true, NeedsCamouflage = true,
        },
        new("Ember Scales", "Resist fire while hidden", "res://assets/sprites/icons/ember_scales.png")
        {
            ResistWhileHidden = DamageType.Fire, NeedsCamouflage = true,
        },
        new("Bomb Vest", "One more bomb out at a time", "res://assets/sprites/icons/bomb_vest.png")
        {
            ExtraBombs = 1, NeedsBombs = true,
        },
        new("Sturdy Vest", "+2 hearts", "res://assets/sprites/icons/sturdy_vest.png")
        {
            ExtraHealth = 4,
        },
    };

    /// <summary>Camouflage doesn't break when walking (attacking or getting hit still does).</summary>
    public bool HideWhileMoving { get; init; }

    /// <summary>Half damage of this type while camouflaged.</summary>
    public DamageType? ResistWhileHidden { get; init; }

    public int ExtraBombs { get; init; }

    public int ExtraHealth { get; init; }

    /// <summary>Only for heroes with camouflage.</summary>
    public bool NeedsCamouflage { get; init; }

    /// <summary>Only for heroes who throw bombs.</summary>
    public bool NeedsBombs { get; init; }

    public bool Fits(Hero hero) =>
        (!NeedsCamouflage || hero.Abilities.OfType<CamouflageAbility>().Any()) &&
        (!NeedsBombs || hero.Abilities.OfType<BombAbility>().Any());

    public MenuOption ToOption() => new(Name, Description, GD.Load<Texture2D>(IconPath), Colors.White);
}
