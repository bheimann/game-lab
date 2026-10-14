using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Combat;

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

    /// <summary>The heroes came back into the room: start over, back at the starting spot with full health
    /// and tricked again. (Bosses stay beaten.)</summary>
    void Respawn();
}

/// <summary>
/// Something an enemy can go after: a hero, a decoy (clones, a dropped tail), or, for a charmed enemy,
/// another enemy.
/// </summary>
public interface IEnemyTarget
{
    Vector2 GlobalPosition { get; }

    Vector2 HurtCenter { get; }

    float HurtRadius { get; }

    /// <summary>Can enemies see it and go after it right now?</summary>
    bool IsTargetable { get; }

    void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical);
}

/// <summary>An enemy the Illusionist can charm: it fights for the heroes for a while. (Bosses can't be charmed.)</summary>
public interface ICharmable
{
    bool CanBeCharmed { get; }

    Vector2 HurtCenter { get; }

    void Charm(float seconds, Hero by);
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

    /// <summary>Decoys (clones, a dropped tail): enemies go after these before heroes.</summary>
    public const string LureGroup = "lures";

    public static IEnumerable<IEnemyTarget> Lures(SceneTree tree) =>
        tree.GetNodesInGroup(LureGroup).OfType<IEnemyTarget>().Where(l => l.IsTargetable);

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
