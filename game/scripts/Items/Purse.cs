namespace LizardWizard.Items;

/// <summary>
/// The team's coins: one purse shared by everyone (like the keys), from loot and from the Thief's stealing.
/// Kept in the save file. Nothing to spend them on yet.
/// </summary>
public static class Purse
{
    public static int Coins { get; private set; }

    public static void Add(int coins) => Coins += coins;

    /// <summary>Loading a save, or a new game (0).</summary>
    public static void Restore(int coins) => Coins = coins;
}
