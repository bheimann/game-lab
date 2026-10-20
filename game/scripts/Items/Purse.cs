namespace LizardWizard.Items;

/// <summary>
/// The team's coins, healing herbs, smelling salts and fairies: shared by everyone (like the keys), so nobody can grab
/// them all for themselves.
/// Coins come from loot and the Thief's stealing; bosses also drop a special boss coin. Kept in the save file.
/// </summary>
public static class Purse
{
    public static int Coins { get; private set; }

    /// <summary>Special coins, one from each boss.</summary>
    public static int BossCoins { get; private set; }

    /// <summary>The team's healing herbs: any hero can eat one.</summary>
    public static int Herbs { get; private set; }

    public static void Add(int coins) => Coins += coins;

    public static void AddHerbs(int count) => Herbs += count;

    /// <summary>Smelling salts: a hero standing next to a knocked-out teammate wakes them up (half hearts).</summary>
    public static int Salts { get; private set; }

    /// <summary>Fairies: a knocked-out hero lets one out and gets back up where they are (half hearts).</summary>
    public static int Fairies { get; private set; }

    public static void AddSalts(int count) => Salts += count;

    public static void AddFairies(int count) => Fairies += count;

    /// <summary>Use up one of the team's smelling salts, if there's one left.</summary>
    public static bool TakeSalts()
    {
        if (Salts <= 0)
        {
            return false;
        }

        Salts--;
        return true;
    }

    /// <summary>Use up one of the team's fairies, if there's one left.</summary>
    public static bool TakeFairy()
    {
        if (Fairies <= 0)
        {
            return false;
        }

        Fairies--;
        return true;
    }

    /// <summary>Loading a save, or a new game (0, 0).</summary>
    public static void RestoreRevivers(int salts, int fairies)
    {
        Salts = salts;
        Fairies = fairies;
    }

    /// <summary>Eat one of the team's herbs, if there's one left.</summary>
    public static bool TakeHerb()
    {
        if (Herbs <= 0)
        {
            return false;
        }

        Herbs--;
        return true;
    }

    /// <summary>Loading a save, or a new game (0).</summary>
    public static void RestoreHerbs(int herbs) => Herbs = herbs;

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
