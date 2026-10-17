using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Combat;

namespace LizardWizard.Items;

/// <summary>
/// A piece of armor for the hero's one armor slot. Armor changes how a hero's powers work
/// (e.g. the shadow cloak lets a chameleon stay hidden while walking).
/// Everyone owns every piece that fits them, except crafted armor (a <see cref="Recipe"/>), which the moose Tinkerer
/// has to make from monster parts first; then the whole team has it.
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
        new("Hide Vest", "+1 heart", "res://assets/sprites/icons/hide_vest.png")
        {
            ExtraHealth = 2, Recipe = new() { [MonsterPart.Hide] = 2 },
        },
        new("Antler Helm", "+2 hearts", "res://assets/sprites/icons/antler_helm.png")
        {
            ExtraHealth = 4, Recipe = new() { [MonsterPart.Antler] = 2, [MonsterPart.Hide] = 1 },
        },
        new("Shell Plate", "+3 hearts", "res://assets/sprites/icons/shell_plate.png")
        {
            ExtraHealth = 6, Recipe = new() { [MonsterPart.Shell] = 1, [MonsterPart.Hide] = 2 },
        },
    };

    private static readonly HashSet<string> CraftedNames = new();

    /// <summary>Crafted armor the team has made so far.</summary>
    public static IEnumerable<string> Crafted => CraftedNames;

    /// <summary>Armor the moose Tinkerer can make.</summary>
    public static IEnumerable<Armor> Craftable => All.Where(a => a.Recipe is not null);

    /// <summary>Loading a save, or a new game (none).</summary>
    public static void RestoreCrafted(IEnumerable<string> names)
    {
        CraftedNames.Clear();
        CraftedNames.UnionWith(names);
    }

    /// <summary>Monster parts it takes to craft this (null = not crafted; everyone already has it).</summary>
    public Dictionary<MonsterPart, int>? Recipe { get; init; }

    public bool IsCrafted => CraftedNames.Contains(Name);

    /// <summary>"2 hide, 1 antlers" (what the recipe takes).</summary>
    public string RecipeText => Recipe is null ? "" : string.Join(", ", Recipe.Select(r => $"{r.Value} {r.Key.Plural}"));

    public bool CanCraft => Recipe is not null && !IsCrafted && Recipe.All(r => MonsterParts.Count(r.Key) >= r.Value);

    /// <summary>Use up the parts and make it (the whole team can wear it from now on).</summary>
    public bool Craft()
    {
        if (!CanCraft)
        {
            return false;
        }

        foreach (var (part, count) in Recipe!)
        {
            MonsterParts.Take(part, count);
        }

        CraftedNames.Add(Name);
        return true;
    }

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
        (Recipe is null || IsCrafted) &&
        (!NeedsCamouflage || hero.Abilities.OfType<CamouflageAbility>().Any()) &&
        (!NeedsBombs || hero.Abilities.OfType<BombAbility>().Any());

    public MenuOption ToOption() => new(Name, Description, GD.Load<Texture2D>(IconPath), Colors.White);
}
