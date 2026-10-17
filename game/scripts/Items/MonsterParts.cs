using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LizardWizard.Items;

/// <summary>
/// A monster part: dropped by beaten enemies, kept in the team's bag (<see cref="MonsterParts"/>). The moose
/// Tinkerer fuses parts onto weapons, guns and armor (see <see cref="Fusion"/>) and crafts armor from them.
/// </summary>
/// <param name="ExtraDamage">Fused onto a weapon or gun: this much more damage per hit.</param>
/// <param name="CooldownFactor">Fused onto a weapon or gun: the wait between attacks is multiplied by this (less = faster).</param>
/// <param name="ExtraHealth">Fused onto armor: this much more health (2 = a heart).</param>
/// <param name="SpeedFactor">Fused onto armor: walking speed is multiplied by this.</param>
public sealed record MonsterPart(string Name, string Plural, string FuseName, string IconPath, string AttackDescription,
    string ArmorDescription, int ExtraDamage = 0, float CooldownFactor = 1f, int ExtraHealth = 0, float SpeedFactor = 1f)
{
    public static readonly MonsterPart Antler = new("Antler", "antlers", "Antler", "res://assets/sprites/icons/antler.png",
        "+2 damage", "+1 heart", ExtraDamage: 2, ExtraHealth: 2);
    public static readonly MonsterPart Hide = new("Hide", "hide", "Hide", "res://assets/sprites/icons/hide.png",
        "Faster attacks", "Walk faster", CooldownFactor: 0.65f, SpeedFactor: 1.15f);
    public static readonly MonsterPart Shell = new("Beetle Shell", "beetle shell", "Shell", "res://assets/sprites/icons/shell.png",
        "+4 damage", "+2 hearts", ExtraDamage: 4, ExtraHealth: 4);

    public static readonly MonsterPart[] All = { Antler, Hide, Shell };

    public static MonsterPart? Named(string name) => All.FirstOrDefault(p => p.Name == name);

    public Texture2D Icon => GD.Load<Texture2D>(IconPath);
}

/// <summary>The team's bag of monster parts (shared, like the coins). Kept in the save file.</summary>
public static class MonsterParts
{
    private static readonly Dictionary<string, int> Bag = new();

    public static int Count(MonsterPart part) => Bag.GetValueOrDefault(part.Name);

    public static void Add(MonsterPart part, int count = 1) => Bag[part.Name] = Count(part) + count;

    /// <summary>Use up parts, if there are enough. Returns false (and uses nothing) if not.</summary>
    public static bool Take(MonsterPart part, int count = 1)
    {
        if (Count(part) < count)
        {
            return false;
        }

        Bag[part.Name] = Count(part) - count;
        return true;
    }

    public static Dictionary<string, int> Snapshot() => new(Bag.Where(p => p.Value > 0));

    /// <summary>Loading a save, or a new game (empty).</summary>
    public static void Restore(IReadOnlyDictionary<string, int>? parts)
    {
        Bag.Clear();
        foreach (var (name, count) in parts ?? new Dictionary<string, int>())
        {
            Bag[name] = count;
        }
    }
}

/// <summary>What a monster part gets fused onto.</summary>
public enum FuseKind
{
    /// <summary>A main attack (A button) that hits up close: punches, bites, swords, antlers...</summary>
    Weapon,

    /// <summary>A main attack that shoots: guns, feathers, spit...</summary>
    Gun,

    /// <summary>The hero's armor (on top of whatever armor piece they wear).</summary>
    Armor,
}

/// <summary>
/// A monster part fused onto a hero's weapon, gun or armor, Tears of the Kingdom style (the moose Tinkerer does it):
/// weapons and guns hit harder or faster, armor gives hearts or speed. Each hero has slots for 3 of each kind, plus one
/// more per boss beaten, and picks which one to use in the ring menu (Start).
/// </summary>
public sealed record Fusion(MonsterPart Part, FuseKind Kind)
{
    /// <summary>How many fusions of each kind a hero can carry: 3, plus one for every boss beaten.</summary>
    public static int Slots => 3 + Unlocks.BeatenBosses.Count();

    public string Description => Kind == FuseKind.Armor ? Part.ArmorDescription : Part.AttackDescription;

    /// <summary>E.g. "Antler Punch" (for a hero whose main attack is called "Punch") or "Antler Armor".</summary>
    public string NameFor(string attack) => $"{Part.FuseName} {(Kind == FuseKind.Armor ? "Armor" : attack)}";

    /// <summary>For the save file: "Weapon:Antler".</summary>
    public override string ToString() => $"{Kind}:{Part.Name}";

    public static Fusion? Parse(string text) =>
        text.Split(':', 2) is [var kind, var part] && System.Enum.TryParse<FuseKind>(kind, out var k) && MonsterPart.Named(part) is { } p
            ? new Fusion(p, k)
            : null;
}
