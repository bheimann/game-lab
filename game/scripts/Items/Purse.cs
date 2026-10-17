namespace LizardWizard.Items;

/// <summary>
/// The team's coins: one purse shared by everyone (like the keys), from loot and from the Thief's stealing.
/// Bosses also drop a special boss coin. Kept in the save file. Nothing to spend them on yet.
/// </summary>
public static class Purse
{
    public static int Coins { get; private set; }

    /// <summary>Special coins, one from each boss.</summary>
    public static int BossCoins { get; private set; }

    public static void Add(int coins) => Coins += coins;

    public static void AddBossCoin() => BossCoins++;

    /// <summary>Pay <paramref name="coins"/>, if there are enough. Returns false (and pays nothing) if not.</summary>
    public static bool Spend(int coins)
    {
        if (Coins < coins)
        {
            return false;
        }

        Coins -= coins;
        return true;
    }

    /// <summary>Loading a save, or a new game (0, 0).</summary>
    public static void Restore(int coins, int bossCoins)
    {
        Coins = coins;
        BossCoins = bossCoins;
    }
}
