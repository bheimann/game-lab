using System.Linq;
using Godot;

namespace LizardWizard.Actors;

/// <summary>An animal tricked by Mossimus Maximus. Beating it puts it to sleep instead of hurting it.</summary>
public interface IEnemy
{
    /// <summary>Still under the trick and willing to fight.</summary>
    bool IsHostile { get; }

    bool IsAsleep { get; }

    /// <summary>Can see this lizard and is going after it (the lizard is "in a fight").</summary>
    bool IsTargeting(Hero lizard);

    /// <summary>Wake up free of the trick and thank the heroes.</summary>
    void WakeUpAfter(double seconds);
}

/// <summary>A boss: gets a big health bar across the top of the screen while it's fighting.</summary>
public interface IBoss
{
    string BossName { get; }

    Combat.Health Health { get; }

    /// <summary>Has noticed the heroes and is fighting.</summary>
    bool IsFighting { get; }
}

public static class Enemies
{
    public const string Group = "enemies";

    /// <summary>
    /// Call when an enemy falls asleep. Once nobody in its room is left fighting, the sleepers wake up
    /// one after another and thank the heroes.
    /// </summary>
    public static void CheckCleared(Node enemy)
    {
        var room = Dungeons.DungeonRoom.Of(enemy);
        var enemies = room is not null
            ? room.Enemies.ToList()
            : enemy.GetTree().GetNodesInGroup(Group).OfType<IEnemy>().ToList();
        if (enemies.Any(e => e.IsHostile))
        {
            return;
        }

        var delay = 1.0;
        foreach (var sleeper in enemies.Where(e => e.IsAsleep))
        {
            sleeper.WakeUpAfter(delay);
            delay += 0.6;
        }
    }
}
