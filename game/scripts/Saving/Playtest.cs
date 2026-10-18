using Godot;
using LizardWizard.Items;

namespace LizardWizard.Saving;

/// <summary>
/// Playtest settings from <c>playtest.cfg</c> (next to project.godot), so things can be tried out quickly without
/// changing code. They're used when a new game starts (no save yet). So far: <c>coins</c>, the coins the team starts with.
/// </summary>
public static class Playtest
{
    private const string Path = "res://playtest.cfg";

    /// <summary>A new game is starting: apply the settings.</summary>
    public static void ApplyNewGame()
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok)
        {
            return; // no settings file: the normal start
        }

        var coins = (int)config.GetValue("new_game", "coins", 0);
        if (coins > 0)
        {
            Purse.Restore(coins, Purse.BossCoins);
            GD.Print($"Playtest: starting with {coins} coins (see playtest.cfg)");
        }
    }
}
