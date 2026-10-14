using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;

namespace LizardWizard.Effects;

/// <summary>What a shot looks like.</summary>
public enum ShotLook
{
    Pellet,
    Javelin,
    Arrow,
}

/// <summary>
/// A shot: a bullet, spit, a javelin or an arrow. Flies straight, hits the first enemy (or lever) it touches,
/// and stops at walls. It flies over pits, like the tongue. Explosive shots blow up where they stop.
/// </summary>
public partial class Bullet : Node2D
{
    private Vector2 _velocity;
    private float _travelled;
    private Hero _shooter = null!;
    private Shot _shot;

    /// <summary>Everything about one shot.</summary>
    /// <param name="ExplosionRadius">0 = not explosive.</param>
    public readonly record struct Shot(float Speed, float Range, int Damage, DamageType Type, Color Colour, ShotLook Look,
        float ExplosionRadius = 0, int ExplosionDamage = 0);

    /// <param name="from">Global start point (the end of the barrel, the hand).</param>
    public static Bullet Fire(Hero shooter, Vector2 from, Vector2 direction, Shot shot)
    {
        var bullet = new Bullet
        {
            _shooter = shooter,
            _velocity = direction.Normalized() * shot.Speed,
            _shot = shot,
            Rotation = direction.Angle(),
        };
        shooter.GetParent().AddChild(bullet);
        bullet.GlobalPosition = from;
        return bullet;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_shooter))
        {
            QueueFree();
            return;
        }

        var start = GlobalPosition;
        var end = start + _velocity * (float)delta;

        var wall = GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(start, end, collisionMask: 1));
        if (wall.Count > 0)
        {
            end = (Vector2)wall["position"];
        }

        var target = _shooter.EnemyTargets()
            .Where(e => Hits.Touches(e, start, end))
            .OrderBy(e => e.HurtCenter.DistanceTo(start))
            .FirstOrDefault();
        if (target is not null)
        {
            _shooter.Hit(target, _shot.Damage, start - _velocity.Normalized() * 8f, _shot.Type);
            Stop(target.HurtCenter);
            return;
        }

        GlobalPosition = end;
        _travelled += start.DistanceTo(end);
        if (wall.Count > 0 || _travelled >= _shot.Range)
        {
            Stop(end);
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        switch (_shot.Look)
        {
            case ShotLook.Javelin:
                // A wooden shaft with a grey point.
                DrawLine(new Vector2(-12, 0), new Vector2(0, 0), new Color(0.6f, 0.4f, 0.25f), 1.5f);
                DrawColoredPolygon(new[] { new Vector2(0, -1.5f), new Vector2(4, 0), new Vector2(0, 1.5f) }, new Color(0.8f, 0.82f, 0.9f));
                break;

            case ShotLook.Arrow:
                // A thin shaft, a point, and feathers in the arrow type's colour.
                DrawLine(new Vector2(-8, 0), new Vector2(0, 0), new Color(0.7f, 0.55f, 0.35f), 1f);
                DrawColoredPolygon(new[] { new Vector2(0, -1.5f), new Vector2(3, 0), new Vector2(0, 1.5f) }, new Color(0.85f, 0.87f, 0.95f));
                DrawLine(new Vector2(-9, -2), new Vector2(-7, 0), _shot.Colour, 1.5f);
                DrawLine(new Vector2(-9, 2), new Vector2(-7, 0), _shot.Colour, 1.5f);
                break;

            default:
                // A bright pellet with a short streak behind it.
                DrawLine(new Vector2(-Mathf.Min(6f, _travelled), 0), Vector2.Zero, _shot.Colour with { A = 0.5f }, 1.5f);
                DrawCircle(Vector2.Zero, 1.5f, Colors.White);
                break;
        }
    }

    /// <summary>The shot is done: explosive shots blow up here.</summary>
    private void Stop(Vector2 at)
    {
        if (_shot.ExplosionRadius > 0)
        {
            // Bombs measure from their feet, 6 px below the middle of the blast.
            GetParent().AddChild(new Bomb
            {
                Instant = true,
                From = at + new Vector2(0, 6),
                To = at + new Vector2(0, 6),
                Radius = _shot.ExplosionRadius,
                Damage = _shot.ExplosionDamage,
            });
        }

        QueueFree();
    }
}
