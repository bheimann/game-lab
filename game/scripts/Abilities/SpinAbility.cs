using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>Spin attack (the flail): whirl round once, hitting every enemy close by.</summary>
public partial class SpinAbility : Ability
{
    private readonly HashSet<IHittable> _hit = new();
    private float _time = -1f;

    [Export] public int Damage { get; set; } = 5;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>How far the spin reaches, in pixels.</summary>
    [Export] public float Radius { get; set; } = 24f;

    [Export] public float SpinSeconds { get; set; } = 0.45f;

    [Export] public float MoveFactorWhileSpinning { get; set; } = 0.6f;

    [Export] public Color Colour { get; set; } = new(0.8f, 0.82f, 0.9f);

    public override bool IsActive => _time >= 0;

    public override float MoveFactor => MoveFactorWhileSpinning;

    protected override void Use()
    {
        _time = 0;
        _hit.Clear();
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
        foreach (var target in Hero.EnemyTargets().Where(t => Hits.Touches(t, Hero.HurtCenter, Radius) && !_hit.Contains(t)).ToList())
        {
            _hit.Add(target);
            Hero.Hit(target, Damage, Hero.GlobalPosition, DamageType);
        }

        if (_time >= SpinSeconds)
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

        // The ball on its chain sweeping round, with a fading trail.
        var center = Hero.HurtCenter - Hero.GlobalPosition;
        var angle = Hero.Facing.Angle() + Mathf.Tau * _time / SpinSeconds;
        DrawArc(center, Radius - 4f, angle - 2.2f, angle, 16, Colour with { A = 0.4f }, 3f);
        var ball = center + Vector2.Right.Rotated(angle) * (Radius - 4f);
        DrawLine(center, ball, new Color(0.45f, 0.45f, 0.5f), 1f);
        DrawCircle(ball, 3f, Colour);
        DrawArc(ball, 3f, 0, Mathf.Tau, 12, new Color(0.08f, 0.09f, 0.18f), 1f);
    }
}
