using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Effects;

namespace LizardWizard.Items;

/// <summary>
/// What the team has earned so far. The fox starts with its original bombs, weapons and guns; each boss
/// beaten unlocks new ones (the boss drops aren't designed yet). Shared by the whole team, and kept until
/// the game is closed; the save file keeps them between games.
/// </summary>
public static class Unlocks
{
    /// <summary>Stand-in for "a boss we haven't built yet": things unlocked by it stay locked for now.</summary>
    public const string LaterBoss = "a later boss";

    private static readonly HashSet<string> BossesBeaten = new();

    public static bool IsBeaten(string boss) => BossesBeaten.Contains(boss);

    public static IEnumerable<string> BeatenBosses => BossesBeaten;

    /// <summary>Set the bosses beaten (loading a save, or a new game with none).</summary>
    public static void Restore(IEnumerable<string> bosses)
    {
        BossesBeaten.Clear();
        BossesBeaten.UnionWith(bosses);
    }

    /// <summary>Call when a boss is beaten: unlocks its rewards and announces them over <paramref name="where"/>.</summary>
    public static void BossBeaten(Node2D where, string boss)
    {
        if (!BossesBeaten.Add(boss))
        {
            return;
        }

        if (boss == Actors.FrostMoose.Boss)
        {
            FloatingText.Spawn(where, where.GlobalPosition + new Vector2(0, -70), "The beetle can now build the Hoversled!",
                new Color(0.6f, 0.85f, 1f), 4f, 7f);
        }

        var rewards = BombAbility.UnlockedBy(boss).ToList();
        if (rewards.Count > 0)
        {
            FloatingText.Spawn(where, where.GlobalPosition + new Vector2(0, -56), $"The fox can now use: {string.Join(", ", rewards)}!",
                new Color(1f, 0.85f, 0.3f), 4f, 6f);
        }
    }
}
