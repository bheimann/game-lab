using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>
/// The beetle curls up into a ball and rolls: fast, steerable a little with the stick, bouncing off walls.
/// Every enemy it rolls into gets hit (once per roll) and knocked away. Hits taken while rolling hurt less.
/// </summary>
public partial class RollAbility : Ability
{
    private readonly HashSet<IHittable> _hit = new();
    private float _time = -1f;
    private Vector2 _direction;

    [Export] public int Damage { get; set; } = 5;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>Rolling speed in pixels per second.</summary>
    [Export] public float RollSpeed { get; set; } = 190f;

    [Export] public float RollSeconds { get; set; } = 0.7f;

    /// <summary>How fast the stick turns the roll, in degrees per second.</summary>
    [Export] public float TurnDegreesPerSecond { get; set; } = 240f;

    /// <summary>Damage taken while rolling is multiplied by this.</summary>
    [Export] public float DamageTakenWhileRolling { get; set; } = 0.5f;

    /// <summary>The ball's colour (the beetle's shell).</summary>
    [Export] public Color Colour { get; set; } = new(0.45f, 0.3f, 0.7f);

    public override bool IsActive => _time >= 0;

    public override Vector2? Motion => IsActive ? _direction * RollSpeed : null;

    public override float SpriteAlpha => IsActive ? 0f : 1f;

    public override float DamageMultiplier(DamageType type) => IsActive ? DamageTakenWhileRolling : 1f;

    protected override void Use()
    {
        _time = 0;
        _hit.Clear();
        _direction = Hero.Controls.Move.Length() > 0.2f ? Hero.Controls.Move.Normalized() : Hero.Facing;
    }

    public override void Cancel()
    {
        _time = -1f;
        QueueRedraw();
    }

    protected override void Tick(float dt)
    {
        if (_time < 0)
        {
            return;
        }

        _time += dt;

        // Bounce off whatever the hero bumped into while moving this frame.
        if (Hero.GetLastSlideCollision() is { } bump && bump.GetNormal().Dot(_direction) < -0.3f)
        {
            _direction = _direction.Bounce(bump.GetNormal()).Normalized();
        }

        // Steer a little with the stick.
        if (Hero.Controls.Move.Length() > 0.2f)
        {
            var turn = Mathf.Clamp(_direction.AngleTo(Hero.Controls.Move), -1f, 1f) * Mathf.DegToRad(TurnDegreesPerSecond) * dt;
            _direction = _direction.Rotated(turn);
        }

        Hero.Face(_direction);

        var ball = Hero.GlobalPosition + new Vector2(0, -7);
        foreach (var target in Hero.EnemyTargets().Where(t => Hits.Touches(t, ball, 8f) && !_hit.Contains(t)).ToList())
        {
            _hit.Add(target);
            Hero.Hit(target, Damage, ball - _direction * 8f, DamageType);
        }

        if (_time >= RollSeconds)
        {
            _time = -1f;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_time < 0)
        {
            return;
        }

        // A glossy shell ball with stripes that turn as it rolls, and a shadow under it.
        var center = new Vector2(0, -7);
        var outline = new Color(0.08f, 0.09f, 0.18f);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, 7f, new Color(0, 0, 0, 0.25f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawCircle(center, 7f, Colour);
        var spin = _time * RollSpeed / 7f * (_direction.X < 0 ? -1 : 1);
        for (var i = 0; i < 3; i++)
        {
            var angle = spin + i * Mathf.Tau / 3;
            DrawLine(center + Vector2.Right.Rotated(angle) * 2f, center + Vector2.Right.Rotated(angle) * 6.5f, Colour.Darkened(0.4f), 1.5f);
        }

        DrawCircle(center + new Vector2(-2.5f, -2.5f), 1.5f, Colour.Lightened(0.6f));
        DrawArc(center, 7f, 0, Mathf.Tau, 20, outline, 1f);
    }
}
