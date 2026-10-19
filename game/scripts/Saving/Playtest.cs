using System.Linq;
using Godot;
using LizardWizard.Items;

namespace LizardWizard.Saving;

/// <summary>
/// Playtest settings from <c>playtest.cfg</c> (next to project.godot), so things can be tried out quickly without
/// changing code. <c>[new_game]</c> values are used when a new game starts (no save yet): <c>coins</c>, the coins the
/// team starts with. <c>[always]</c> values are used every time the game starts: <c>unlock_all_teleports</c>, every
/// village's teleport awake.
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

    /// <summary>The game is starting (new or loaded): apply the [always] settings.</summary>
    public static void ApplyAlways(Node anyNode)
    {
        var config = new ConfigFile();
        if (config.Load(Path) != Error.Ok || !(bool)config.GetValue("always", "unlock_all_teleports", false))
        {
            return;
        }

        var villages = anyNode.GetTree().CurrentScene.FindChildren("*", "", true, false).OfType<Dungeons.TeleportPad>()
            .Select(p => p.Village).Distinct().ToList();
        foreach (var village in villages)
        {
            Teleports.Unlock(village);
        }

        GD.Print($"Playtest: every teleport is awake ({villages.Count} villages, see playtest.cfg)");
    }
}
