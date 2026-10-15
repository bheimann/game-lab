using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Players;
using LizardWizard.Util;

namespace LizardWizard.Effects;

/// <summary>
/// The moose's big summon: a herd of ghost deer appears behind it and charges the way it's facing, hitting every
/// enemy in the way once and knocking it aside, then fades out. Spirits: they run through walls and can't be hurt.
/// </summary>
public partial class Stampede : Node2D
{
    private const string WalkStrip = "res://assets/sprites/deer/deer_walk.png";

    private readonly List<AnimatedSprite2D> _deer = new();
    private readonly HashSet<IHittable> _hit = new();
    private float _time;

    public required Hero Caster { get; init; }

    public Vector2 Direction { get; init; } = Vector2.Right;

    public int Count { get; init; } = 5;

    public int Damage { get; init; } = 6;

    public float Speed { get; init; } = 230f;

    public float Seconds { get; init; } = 1.5f;

    public override void _Ready()
    {
        ZIndex = 5;
        var frames = SpriteSheet.Frames("walk", WalkStrip, 40, 10);
        var across = new Vector2(-Direction.Y, Direction.X);
        for (var i = 0; i < Count; i++)
        {
            // A ragged line across the charge, starting behind the moose.
            var offset = across * (i - (Count - 1) / 2f) * 16f - Direction * (40f + (i % 2) * 14f);
            var deer = new AnimatedSprite2D
            {
                SpriteFrames = frames,
                Offset = new Vector2(0, -21),
                Position = offset,
                FlipH = Direction.X < 0,
                Modulate = new Color(0.7f, 0.85f, 1f, 0f),
            };
            AddChild(deer);
            deer.Play("walk");
            _deer.Add(deer);
        }

        CoopCamera.Find(this)?.Shake(3f);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        Position += Direction * Speed * dt;

        // Fade in, then out at the end.
        var alpha = Mathf.Min(1f, _time * 6f) * Mathf.Clamp((Seconds - _time) * 3f, 0, 1) * 0.7f;
        foreach (var deer in _deer)
        {
            deer.Modulate = deer.Modulate with { A = alpha };
            var hoof = deer.GlobalPosition + new Vector2(0, -10);
            foreach (var enemy in GetTree().GetNodesInGroup(Enemies.Group).OfType<IHittable>()
                         .Where(e => e.CanBeHit && !_hit.Contains(e) && Hits.Touches(e, hoof, 12f)).ToList())
            {
                _hit.Add(enemy);
                if (IsInstanceValid(Caster))
                {
                    Caster.Hit(enemy, Damage, hoof - Direction * 12f, DamageType.Physical);
                }
                else
                {
                    enemy.TakeHit(Damage, hoof - Direction * 12f);
                }
            }
        }

        if (_time >= Seconds)
        {
            QueueFree();
        }
    }
}
