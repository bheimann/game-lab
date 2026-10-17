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
    Feather,
    Rocket,
}

/// <summary>
/// A shot: a bullet, spit, a javelin, an arrow, a feather or a rocket. Flies straight, hits the first enemy (or lever) it touches,
/// and stops at walls. It flies over pits, like the tongue. Explosive shots blow up where they stop.
/// Lobbed shots (<see cref="Shot.ArcHeight"/>, the javelin) fly in a high arc over whatever is close, can only hit on the way
/// down, and stick in the ground for a moment if they miss.
/// </summary>
public partial class Bullet : Node2D
{
    private Vector2 _velocity;
    private float _travelled;
    private Hero _shooter = null!;
    private Shot _shot;
    private float _stuckFor = -1f;

    /// <summary>How long a lobbed shot that missed stays stuck in the ground.</summary>
    private const float StuckSeconds = 0.6f;

    /// <summary>A lobbed shot can only hit once it's this far along its flight (coming back down).</summary>
    private const float LobHitsFrom = 0.55f;

    /// <summary>Everything about one shot.</summary>
    /// <param name="ExplosionRadius">0 = not explosive.</param>
    /// <param name="ArcHeight">0 = flies straight; otherwise how high (in pixels) a lobbed shot goes. It lands after <paramref name="Range"/>.</param>
    public readonly record struct Shot(float Speed, float Range, int Damage, DamageType Type, Color Colour, ShotLook Look,
        float ExplosionRadius = 0, int ExplosionDamage = 0, float ArcHeight = 0);

    private bool IsLob => _shot.ArcHeight > 0;

    /// <summary>How far through its flight the shot is (0 = just thrown, 1 = landed).</summary>
    private float Progress => _shot.Range <= 0 ? 1 : Mathf.Clamp(_travelled / _shot.Range, 0, 1);

    /// <summary>A lobbed shot's height above its shadow.</summary>
    private float Height => IsLob ? 4 * _shot.ArcHeight * Progress * (1 - Progress) : 0;

    /// <param name="from">Global start point (the end of the barrel, the hand).</param>
    public static Bullet Fire(Hero shooter, Vector2 from, Vector2 direction, Shot shot)
    {
        var bullet = new Bullet
        {
            _shooter = shooter,
            _velocity = direction.Normalized() * shot.Speed,
            _shot = shot,
            Rotation = shot.ArcHeight > 0 ? 0 : direction.Angle(), // lobs turn as they fly (see _Draw)
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

        if (_stuckFor >= 0)
        {
            // A javelin that missed, stuck in the ground.
            _stuckFor += (float)delta;
            Modulate = Colors.White with { A = Mathf.Clamp((StuckSeconds - _stuckFor) / 0.2f, 0, 1) };
            if (_stuckFor >= StuckSeconds)
            {
                QueueFree();
            }

            return;
        }

        var start = GlobalPosition;
        var end = start + _velocity * (float)delta;

        var wall = GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(start, end, collisionMask: 1));
        if (wall.Count > 0)
        {
            end = (Vector2)wall["position"];
        }

        var target = IsLob && Progress < LobHitsFrom ? null : _shooter.EnemyTargets()
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
            if (IsLob && _shot.ExplosionRadius <= 0)
            {
                // Missed: it sticks in the ground for a moment.
                _stuckFor = 0;
                QueueRedraw();
                return;
            }

            Stop(end);
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IsLob)
        {
            // Shadow on the ground (shots fly at chest height, so it's a bit lower), and the shot up in the air pointing
            // the way it's moving (up, then down).
            var shadow = Mathf.Lerp(1f, 0.5f, Height / Mathf.Max(1, _shot.ArcHeight));
            DrawSetTransform(new Vector2(0, 14), 0, new Vector2(1f, 0.4f));
            DrawCircle(Vector2.Zero, 4f * shadow, new Color(0, 0, 0, 0.3f));
            var climb = _stuckFor >= 0 ? -1.2f : 4 * _shot.ArcHeight * (1 - 2 * Progress) / Mathf.Max(1, _shot.Range);
            var screen = _velocity.Normalized() + new Vector2(0, -climb);
            DrawSetTransform(new Vector2(0, _stuckFor >= 0 ? 10 : -Height), screen.Angle(), Vector2.One);
        }

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

            case ShotLook.Feather:
                // A peacock feather: a quill, a vane in the element's colour, and the "eye" near the tip.
                DrawLine(new Vector2(-9, 0), new Vector2(1, 0), new Color(0.9f, 0.88f, 0.8f), 1f);
                DrawColoredPolygon(new[] { new Vector2(-7, 0), new Vector2(-3, -2.5f), new Vector2(1, -1.5f), new Vector2(2, 0), new Vector2(1, 1.5f), new Vector2(-3, 2.5f) }, _shot.Colour);
                DrawCircle(new Vector2(-1, 0), 1.5f, new Color(0.15f, 0.35f, 0.75f));
                DrawCircle(new Vector2(-1, 0), 0.7f, new Color(0.1f, 0.15f, 0.2f));
                break;

            case ShotLook.Rocket:
                // A rocket: a grey body with a red nose and fins, and a flickering flame out the back.
                var flame = 3f + 2f * Mathf.Sin(_travelled * 0.8f);
                DrawColoredPolygon(new[] { new Vector2(-6, -1.5f), new Vector2(-6 - flame, 0), new Vector2(-6, 1.5f) }, new Color(1f, 0.6f, 0.2f));
                DrawRect(new Rect2(-6, -2, 8, 4), new Color(0.6f, 0.65f, 0.6f));
                DrawColoredPolygon(new[] { new Vector2(2, -2), new Vector2(5, 0), new Vector2(2, 2) }, new Color(0.85f, 0.25f, 0.2f));
                DrawColoredPolygon(new[] { new Vector2(-6, -2), new Vector2(-8, -4), new Vector2(-4, -2) }, new Color(0.85f, 0.25f, 0.2f));
                DrawColoredPolygon(new[] { new Vector2(-6, 2), new Vector2(-8, 4), new Vector2(-4, 2) }, new Color(0.85f, 0.25f, 0.2f));
                break;

            default:
                // A bright pellet with a short streak behind it.
                DrawLine(new Vector2(-Mathf.Min(6f, _travelled), 0), Vector2.Zero, _shot.Colour with { A = 0.5f }, 1.5f);
                DrawCircle(Vector2.Zero, 1.5f, Colors.White);
                break;
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>The shot is done: explosive shots blow up here.</summary>
    private void Stop(Vector2 at)
    {
        if (_shot.ExplosionRadius > 0)
        {
            // Bombs measure from their feet, 6 px below the middle of the blast.
            GetParent().AddChild(new Bomb
            {
                Thrower = _shooter,
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
