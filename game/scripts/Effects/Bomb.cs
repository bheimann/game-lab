using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Players;
using LizardWizard.Util;

namespace LizardWizard.Effects;

/// <summary>The Bomb Master's bomb types.</summary>
public enum BombKind
{
    /// <summary>Short fuse, then a blast.</summary>
    Bomb,

    /// <summary>No damage: leaves a cloud that hides the heroes inside it.</summary>
    Smoke,

    /// <summary>Sticks to the enemy it hits and blows up on them.</summary>
    Sticky,

    /// <summary>Long fuse, bigger blast: for setting traps.</summary>
    Time,

    /// <summary>Waits until the thrower presses the bomb button again.</summary>
    Remote,

    /// <summary>Sits on the ground and goes off when an enemy walks near.</summary>
    Proximity,
}

/// <summary>A thrown bomb: flies in an arc, lands, then (depending on its kind) waits and explodes.</summary>
public partial class Bomb : Node2D
{
    private const string BombTexture = "res://assets/third_party/tiny_dungeon/Weapons/Bomb.png";
    private const string LitStrip = "res://assets/third_party/tiny_dungeon/Weapons/Bomb_lighted.png";
    private const float ArcHeight = 18f;
    private const float BlastSeconds = 0.35f;
    private const float ArmSeconds = 0.6f;
    private const float ProximityRange = 14f;
    private const float WaitingLifetime = 30f;

    private AnimatedSprite2D _sprite = null!;
    private float _time;
    private State _state = State.Flying;
    private Node2D? _stuckTo;
    private Vector2 _stuckOffset;

    private enum State
    {
        Flying,
        Fuse,
        Waiting,
        Blast,
    }

    public Hero? Thrower { get; init; }

    public BombKind Kind { get; init; } = BombKind.Bomb;

    public Vector2 From { get; init; }

    public Vector2 To { get; init; }

    public float FlightSeconds { get; init; } = 0.4f;

    public float FuseSeconds { get; init; } = 1f;

    public float Radius { get; init; } = 28f;

    public int Damage { get; init; } = 8;

    public DamageType DamageType { get; init; } = DamageType.Physical;

    /// <summary>Go off the moment it's added, with no flight or fuse (explosive arrows).</summary>
    public bool Instant { get; init; }

    /// <summary>A remote bomb that has landed and is waiting for the button.</summary>
    public bool IsWaitingForRemote => Kind == BombKind.Remote && _state == State.Waiting;

    /// <summary>Colour each kind is tinted, so you can tell them apart on the ground and in the menu.</summary>
    public static Color Tint(BombKind kind) => kind switch
    {
        BombKind.Smoke => new Color(0.65f, 0.65f, 0.75f),
        BombKind.Sticky => new Color(0.55f, 1f, 0.45f),
        BombKind.Time => new Color(0.55f, 0.75f, 1f),
        BombKind.Remote => new Color(1f, 0.5f, 0.5f),
        BombKind.Proximity => new Color(1f, 0.92f, 0.35f),
        _ => Colors.White,
    };

    public override void _Ready()
    {
        GlobalPosition = From;
        var frames = SpriteSheet.Frames("fly", BombTexture, 12, 1);
        SpriteSheet.AddStrip(frames, "fuse", GD.Load<Texture2D>(LitStrip), 16, 8);
        _sprite = new AnimatedSprite2D { SpriteFrames = frames, Offset = new Vector2(0, -7), Modulate = Tint(Kind) };
        AddChild(_sprite);
        _sprite.Play("fly");
        if (Instant)
        {
            Explode();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        switch (_state)
        {
            case State.Flying:
                var t = Mathf.Clamp(_time / FlightSeconds, 0, 1);
                GlobalPosition = From.Lerp(To, t);
                _sprite.Position = new Vector2(0, -4f * ArcHeight * t * (1 - t)); // parabola
                _sprite.Rotation = t * Mathf.Tau;
                if (Kind == BombKind.Sticky && EnemyTouching(GlobalPosition + _sprite.Position + new Vector2(0, -6), 4f) is { } hit)
                {
                    StickTo(hit);
                }
                else if (t >= 1)
                {
                    Land();
                }

                break;

            case State.Fuse:
                FollowStuckEnemy();

                // The fuse burns faster near the end.
                _sprite.SpeedScale = 1f + 3f * _time / FuseSeconds;
                QueueRedraw();
                if (_time >= FuseSeconds)
                {
                    Explode();
                }

                break;

            case State.Waiting:
                QueueRedraw();
                var armed = _time >= ArmSeconds;
                if ((Kind == BombKind.Proximity && armed && EnemyTouching(GlobalPosition + new Vector2(0, -6), ProximityRange) is not null)
                    || _time >= WaitingLifetime)
                {
                    Explode();
                }

                break;

            case State.Blast:
                QueueRedraw();
                if (_time >= BlastSeconds)
                {
                    QueueFree();
                }

                break;
        }
    }

    public override void _Draw()
    {
        var center = new Vector2(0, -6);
        switch (_state)
        {
            case State.Fuse when Kind == BombKind.Time:
                // A clock ring that empties as the fuse burns down.
                var left = 1f - _time / FuseSeconds;
                DrawArc(center, 9f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * left, 24, Tint(Kind), 1.5f);
                break;

            case State.Waiting when Kind == BombKind.Remote:
                // Blinking light: waiting for the button.
                if ((int)(_time * 4) % 2 == 0)
                {
                    DrawCircle(new Vector2(3, -13), 1.5f, new Color(1f, 0.2f, 0.2f));
                }

                break;

            case State.Waiting when Kind == BombKind.Proximity && _time >= ArmSeconds:
                // Faint trigger circle, so players can see the trap.
                DrawArc(center, ProximityRange + 4f, 0, Mathf.Tau, 24, Tint(Kind) with { A = 0.25f + 0.15f * Mathf.Sin(_time * 6f) }, 1f);
                break;

            case State.Blast when Kind != BombKind.Smoke:
                var t = _time / BlastSeconds;
                var radius = Radius * (0.4f + 0.6f * Mathf.Sqrt(t));
                var fade = 1f - t;
                DrawCircle(center, radius, new Color(1f, 0.53f, 0.2f, 0.55f * fade));
                DrawCircle(center, radius * 0.6f, new Color(1f, 0.93f, 0.5f, 0.8f * fade));
                DrawArc(center, radius, 0, Mathf.Tau, 32, new Color(1f, 1f, 1f, fade), 1.5f);
                break;
        }
    }

    /// <summary>Set off a waiting bomb now (remote bombs).</summary>
    public void Detonate()
    {
        if (_state is State.Waiting or State.Fuse)
        {
            Explode();
        }
    }

    private void Land()
    {
        _time = 0;
        _sprite.Position = Vector2.Zero;
        _sprite.Rotation = 0;

        if (Kind == BombKind.Sticky && EnemyTouching(GlobalPosition + new Vector2(0, -6), 8f) is { } hit)
        {
            StickTo(hit);
            return;
        }

        if (Kind is BombKind.Remote or BombKind.Proximity)
        {
            _state = State.Waiting;
            return;
        }

        _state = State.Fuse;
        _sprite.Play("fuse");
    }

    private void StickTo(Node2D enemy)
    {
        _stuckTo = enemy;
        _stuckOffset = GlobalPosition + _sprite.Position - enemy.GlobalPosition;
        _sprite.Position = Vector2.Zero;
        _sprite.Rotation = 0;
        _state = State.Fuse;
        _time = 0;
        _sprite.Play("fuse");
        ZIndex = 1; // on top of the enemy it's stuck to
        FollowStuckEnemy();
    }

    private void FollowStuckEnemy()
    {
        if (_stuckTo is not null && IsInstanceValid(_stuckTo))
        {
            GlobalPosition = _stuckTo.GlobalPosition + _stuckOffset;
        }
    }

    private Node2D? EnemyTouching(Vector2 point, float radius) =>
        GetTree().GetNodesInGroup(Enemies.Group).OfType<IHittable>()
            .FirstOrDefault(e => e.CanBeHit && Hits.Touches(e, point, radius)) as Node2D;

    private void Explode()
    {
        _state = State.Blast;
        _time = 0;
        _sprite.Visible = false;
        ZIndex = 10;

        if (Kind == BombKind.Smoke)
        {
            GetParent().AddChild(new SmokeCloud { Position = Position, Radius = Radius + 8f });
            return;
        }

        var center = GlobalPosition + new Vector2(0, -6);
        var targets = GetTree().GetNodesInGroup(Enemies.Group).OfType<IHittable>().Where(t => t.CanBeHit && Hits.Touches(t, center, Radius));
        foreach (var target in targets.ToList())
        {
            target.TakeHit(Damage, GlobalPosition, DamageType);
        }

        // Cracked rocks only break to bombs.
        foreach (var boulder in GetTree().GetNodesInGroup(Boulder.Group).OfType<Boulder>().Where(b => b.GlobalPosition.DistanceTo(GlobalPosition) <= Radius + 8f))
        {
            boulder.Break();
        }

        CoopCamera.Find(this)?.Shake(Kind == BombKind.Time ? 5f : 3f);
    }
}
