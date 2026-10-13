using Godot;

namespace LizardWizard.Combat;

public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Arcane,
    Poison,
}

/// <summary>
/// Something attacks can hit. Hits are worked out with simple circles rather than physics
/// shapes: an attack lands if it touches the target's hurt circle.
/// </summary>
public interface IHittable
{
    /// <summary>False while asleep, knocked out, invulnerable, etc.</summary>
    bool CanBeHit { get; }

    /// <summary>Centre of the hurt circle, in global coordinates.</summary>
    Vector2 HurtCenter { get; }

    float HurtRadius { get; }

    /// <param name="damage">Hit points to remove (before resistances).</param>
    /// <param name="from">Where the hit came from (global), for knockback.</param>
    /// <param name="type">What kind of damage, for resistances.</param>
    void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical);
}

public static class Hits
{
    /// <summary>Does a circle at <paramref name="point"/> with <paramref name="radius"/> touch the target?</summary>
    public static bool Touches(IHittable target, Vector2 point, float radius) =>
        target.HurtCenter.DistanceTo(point) <= target.HurtRadius + radius;

    /// <summary>Does the line from <paramref name="start"/> to <paramref name="end"/> touch the target?</summary>
    public static bool Touches(IHittable target, Vector2 start, Vector2 end) =>
        Geometry2D.GetClosestPointToSegment(target.HurtCenter, start, end).DistanceTo(target.HurtCenter) <= target.HurtRadius;
}
