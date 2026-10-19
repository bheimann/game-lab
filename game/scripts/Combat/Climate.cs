using Godot;
using LizardWizard.Dungeons;
using LizardWizard.Rooms;

namespace LizardWizard.Combat;

/// <summary>Where an animal lives, which decides the element it's weak to and the one it shrugs off.</summary>
public enum Climate
{
    /// <summary>Meadows and forests: no weakness.</summary>
    Normal,

    /// <summary>Snow and ice: weak to fire, resists ice.</summary>
    Cold,

    /// <summary>Hot rock and desert: weak to ice, resists fire.</summary>
    Hot,

    /// <summary>Swamps and lakes: weak to lightning, resists fire.</summary>
    Wet,
}

public static class Climates
{
    /// <summary>Hits of this element do double damage.</summary>
    public static DamageType? Weakness(Climate climate) => climate switch
    {
        Climate.Cold => DamageType.Fire,
        Climate.Hot => DamageType.Ice,
        Climate.Wet => DamageType.Lightning,
        _ => null,
    };

    /// <summary>Hits of this element do half damage, and don't burn or slow.</summary>
    public static DamageType? Resistance(Climate climate) => climate switch
    {
        Climate.Cold => DamageType.Ice,
        Climate.Hot or Climate.Wet => DamageType.Fire,
        _ => null,
    };

    /// <summary>The climate of the area <paramref name="node"/> is in: the tundra and Frostfang Fortress are cold, the
    /// canyon is hot, the swamp and the lake are wet.</summary>
    public static Climate Of(Node node)
    {
        if (DungeonRoom.Of(node) is not { } room)
        {
            return Climate.Normal;
        }

        if (room.Area == "Frostfang Fortress")
        {
            return Climate.Cold;
        }

        return room.GetNodeOrNull<OutdoorBuilder>("Layout")?.Ground switch
        {
            Ground.Snow => Climate.Cold,
            Ground.Canyon => Climate.Hot,
            Ground.Swamp or Ground.Sand => Climate.Wet,
            _ => Climate.Normal,
        };
    }
}
