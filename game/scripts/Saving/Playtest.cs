using System.Linq;
using Godot;
using LizardWizard.Items;

namespace LizardWizard.Saving;

/// <summary>
/// Playtest settings from <c>playtest.cfg</c> (next to project.godot), so things can be tried out quickly without
/// changing code. <c>[new_game]</c> values are used when a new game starts (no save yet): <c>coins</c>, the coins the
/// team starts with. <c>[always]</c> values are used every time the game starts: <c>unlock_all_teleports</c>, every
/// village's teleport awake; <c>unlock_final_dungeon</c>, Mossimus's dungeon open before the others are beaten.
/// </summary>
public static class Playtest
{
    private const string Path = "res://playtest.cfg";

    private static bool? _unlockFinalDungeon;

    /// <summary>The final dungeon's stairs are open even though the other dungeons aren't all beaten yet.</summary>
    public static bool UnlockFinalDungeon => _unlockFinalDungeon ??= Load() is { } config && (bool)config.GetValue("always", "unlock_final_dungeon", false);

    /// <summary>A new game is starting: apply the settings.</summary>
    public static void ApplyNewGame()
    {
        if (Load() is not { } config)
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
        if (UnlockFinalDungeon)
        {
            GD.Print("Playtest: the final dungeon is open (see playtest.cfg)");
        }

        if (Load() is not { } config || !(bool)config.GetValue("always", "unlock_all_teleports", false))
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

    private static ConfigFile? Load()
    {
        var config = new ConfigFile();
        return config.Load(Path) == Error.Ok ? config : null;
    }
}
