using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;

namespace LizardWizard.Effects;

/// <summary>
/// The frost moose's stomp: a ring of frost that spreads out across the floor. Any hero (or summon, or decoy) the ring
/// passes over takes a hit and, if it's a hero, is chilled (walks at half speed for a while). Dodge through it!
/// </summary>
public partial class IceWave : Node2D
{
    private readonly HashSet<IEnemyTarget> _hit = new();
    private float _time;

    [Export] public float MaxRadius { get; set; } = 100f;

    [Export] public float Seconds { get; set; } = 0.7f;

    [Export] public int Damage { get; set; } = 3;

    [Export] public float ChillSeconds { get; set; } = 2.5f;

    private float Radius => MaxRadius * Mathf.Min(1f, _time / Seconds);

    public override void _Ready() => ZIndex = -1; // on the floor

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        if (_time >= Seconds + 0.2f)
        {
            QueueFree();
            return;
        }

        // Things whose feet are right at the ring's edge (within a band) get hit, once.
        var victims = GetTree().GetNodesInGroup(Hero.Group).OfType<IEnemyTarget>()
            .Concat(Enemies.Summons(GetTree())).Concat(Enemies.Lures(GetTree()));
        foreach (var victim in victims.Where(v => v.IsTargetable && !_hit.Contains(v)))
        {
            var d = Squash(victim.GlobalPosition - GlobalPosition).Length();
            if (Mathf.Abs(d - Radius) < 10f && _time <= Seconds)
            {
                _hit.Add(victim);
                victim.TakeHit(Damage, GlobalPosition);
                if (victim is Hero hero)
                {
                    hero.Chill(ChillSeconds);
                }
            }
        }

        QueueRedraw();
    }

    /// <summary>The ring is flattened like the floor, so measure distances the same way.</summary>
    private static Vector2 Squash(Vector2 v) => new(v.X, v.Y / 0.55f);

    public override void _Draw()
    {
        var fade = Mathf.Clamp(1f - (_time - Seconds) / 0.2f, 0, 1);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.55f));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 48, new Color(0.85f, 0.95f, 1f, 0.9f * fade), 4f);
        DrawArc(Vector2.Zero, Mathf.Max(0, Radius - 6f), 0, Mathf.Tau, 48, new Color(0.55f, 0.8f, 1f, 0.6f * fade), 2f);
        for (var i = 0; i < 12; i++)
        {
            // Little ice spikes around the ring.
            var dir = Vector2.Right.Rotated(i * Mathf.Tau / 12 + _time);
            DrawLine(dir * Radius, dir * (Radius + 6), Colors.White with { A = fade }, 1.5f);
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
