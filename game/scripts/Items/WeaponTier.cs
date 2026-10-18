using Godot;

namespace LizardWizard.Items;

/// <summary>
/// The team's weapon material, bought at the Weapons &amp; Armor shop: wood, then copper, iron, steel and mithril. Each
/// one makes every hero's main attack (the A button) hit harder. One upgrade covers the whole team. Kept in the save.
/// </summary>
public sealed record WeaponTier(string Name, int Price, int ExtraDamage, Color Colour)
{
    public static readonly WeaponTier[] All =
    {
        new("Wood", 0, 0, new Color(0.75f, 0.55f, 0.35f)),
        new("Copper", 20, 1, new Color(0.95f, 0.55f, 0.3f)),
        new("Iron", 40, 2, new Color(0.75f, 0.78f, 0.85f)),
        new("Steel", 80, 3, new Color(0.65f, 0.8f, 0.95f)),
        new("Mithril", 160, 5, new Color(0.5f, 1f, 0.95f)),
    };

    private static int _level;

    /// <summary>The team's weapons right now.</summary>
    public static WeaponTier Current => All[_level];

    public static int Level => _level;

    /// <summary>The next upgrade (null once it's mithril).</summary>
    public static WeaponTier? Next => _level + 1 < All.Length ? All[_level + 1] : null;

    /// <summary>Buy the next upgrade with the team's coins. Returns false if it's maxed out or there aren't enough coins.</summary>
    public static bool Upgrade()
    {
        if (Next is not { } next || !Purse.Spend(next.Price))
        {
            return false;
        }

        _level++;
        return true;
    }

    /// <summary>Loading a save, or a new game (0 = wood).</summary>
    public static void Restore(int level) => _level = Mathf.Clamp(level, 0, All.Length - 1);
}
