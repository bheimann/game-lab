using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Players;
using LizardWizard.Util;

namespace LizardWizard.Effects;

/// <summary>A thrown bomb: flies in an arc, sits with a burning fuse, then explodes.</summary>
public partial class Bomb : Node2D
{
    private const string BombTexture = "res://assets/third_party/tiny_dungeon/Weapons/Bomb.png";
    private const string LitStrip = "res://assets/third_party/tiny_dungeon/Weapons/Bomb_lighted.png";
    private const float ArcHeight = 18f;
    private const float BlastSeconds = 0.35f;

    private AnimatedSprite2D _sprite = null!;
    private float _time;
    private State _state = State.Flying;

    private enum State
    {
        Flying,
        Fuse,
        Blast,
    }

    public Hero? Thrower { get; init; }

    public Vector2 From { get; init; }

    public Vector2 To { get; init; }

    public float FlightSeconds { get; init; } = 0.4f;

    public float FuseSeconds { get; init; } = 1f;

    public float Radius { get; init; } = 28f;

    public int Damage { get; init; } = 8;

    public DamageType DamageType { get; init; } = DamageType.Physical;

    public override void _Ready()
    {
        GlobalPosition = From;
        var frames = SpriteSheet.Frames("fly", BombTexture, 12, 1);
        SpriteSheet.AddStrip(frames, "fuse", GD.Load<Texture2D>(LitStrip), 16, 8);
        _sprite = new AnimatedSprite2D { SpriteFrames = frames, Offset = new Vector2(0, -7) };
        AddChild(_sprite);
        _sprite.Play("fly");
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
                if (t >= 1)
                {
                    _state = State.Fuse;
                    _time = 0;
                    _sprite.Position = Vector2.Zero;
                    _sprite.Rotation = 0;
                    _sprite.Play("fuse");
                }

                break;

            case State.Fuse:
                // The fuse burns faster near the end.
                _sprite.SpeedScale = 1f + 3f * _time / FuseSeconds;
                if (_time >= FuseSeconds)
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
        if (_state != State.Blast)
        {
            return;
        }

        var t = _time / BlastSeconds;
        var radius = Radius * (0.4f + 0.6f * Mathf.Sqrt(t));
        var fade = 1f - t;
        DrawCircle(new Vector2(0, -6), radius, new Color(1f, 0.53f, 0.2f, 0.55f * fade));
        DrawCircle(new Vector2(0, -6), radius * 0.6f, new Color(1f, 0.93f, 0.5f, 0.8f * fade));
        DrawArc(new Vector2(0, -6), radius, 0, Mathf.Tau, 32, new Color(1f, 1f, 1f, fade), 1.5f);
    }

    private void Explode()
    {
        _state = State.Blast;
        _time = 0;
        _sprite.Visible = false;
        ZIndex = 10;

        var center = GlobalPosition + new Vector2(0, -6);
        var targets = GetTree().GetNodesInGroup(Enemies.Group).OfType<IHittable>().Where(t => t.CanBeHit && Hits.Touches(t, center, Radius));
        foreach (var target in targets.ToList())
        {
            target.TakeHit(Damage, GlobalPosition, DamageType);
        }

        CoopCamera.Find(this)?.Shake(3f);
    }
}
