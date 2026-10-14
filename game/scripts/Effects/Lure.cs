using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;

namespace LizardWizard.Effects;

public enum LureKind
{
    /// <summary>A copy of the hero that goes after the enemy the hero hit last (or wanders off). Pops when it's hit.</summary>
    Clone,

    /// <summary>A dropped tail that wriggles where it lands. Takes any number of hits.</summary>
    Tail,
}

/// <summary>
/// An Illusionist decoy. Enemies that see it go after it instead of the heroes, until it runs out of time.
/// </summary>
public partial class Lure : Node2D, IEnemyTarget
{
    private readonly RandomNumberGenerator _rng = new();
    private Sprite2D? _sprite;
    private float _left;
    private float _time;
    private Vector2 _drift;
    private float _punchCooldown;
    private float _punchFlash;

    public required Hero Caster { get; init; }

    public LureKind Kind { get; init; }

    /// <summary>How long it lasts.</summary>
    public float Seconds { get; init; } = 5f;

    /// <summary>Clones' punches: weaker than the real hero's.</summary>
    public int PunchDamage { get; init; } = 2;

    public float PunchSeconds { get; init; } = 0.8f;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, Kind == LureKind.Clone ? -12 : -3);

    public float HurtRadius => Kind == LureKind.Clone ? 7f : 5f;

    public bool IsTargetable => _left > 0;

    public override void _Ready()
    {
        AddToGroup(Enemies.LureGroup);
        _left = Seconds;
        _rng.Seed = (ulong)GetInstanceId();
        if (Kind == LureKind.Clone)
        {
            var hero = Caster.Sprite;
            _sprite = new Sprite2D
            {
                Texture = hero.SpriteFrames.GetFrameTexture(hero.Animation, hero.Frame),
                Offset = hero.Offset,
                FlipH = hero.FlipH,
                Material = hero.Material,
            };
            AddChild(_sprite);
            _drift = Vector2.Right.Rotated(_rng.RandfRange(0, Mathf.Tau)) * 22f;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _left -= dt;
        _time += dt;
        if (_left <= 0)
        {
            Vanish();
            return;
        }

        if (Kind == LureKind.Clone)
        {
            _punchCooldown -= dt;
            _punchFlash -= dt;
            var move = Attack() ?? Wander(dt);
            Position += move * dt;
            if (Mathf.Abs(move.X) > 1f)
            {
                _sprite!.FlipH = move.X < 0;
            }

            _sprite!.Modulate = new Color(0.85f, 0.75f, 1f, _left < 1f && (int)(_time * 10) % 2 == 0 ? 0.3f : 0.75f);
        }

        QueueRedraw();
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (!IsTargetable)
        {
            return;
        }

        if (Kind == LureKind.Clone)
        {
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -8), "Poof!", new Color(0.85f, 0.75f, 1f), 0.8f, 8f);
            Vanish();
        }
        else
        {
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -8), "wiggle", new Color(0.6f, 0.9f, 0.5f), 0.5f, 6f);
        }
    }

    public override void _Draw()
    {
        if (Kind == LureKind.Clone)
        {
            if (_punchFlash > 0 && Caster.LastHitEnemy is { } enemy)
            {
                // A little burst where the punch lands.
                var at = ToLocal(enemy.HurtCenter.MoveToward(HurtCenter, enemy.HurtRadius));
                DrawCircle(at, 3.5f, new Color(1, 1, 1, 0.8f));
                DrawArc(at, 5f, 0, Mathf.Tau, 12, new Color(0.8f, 0.6f, 1f), 1f);
            }

            return;
        }

        // A green tail curling and wriggling on the ground.
        var points = new Vector2[8];
        for (var i = 0; i < points.Length; i++)
        {
            var along = i / (float)(points.Length - 1);
            points[i] = new Vector2(-8 + 16 * along, -2 + Mathf.Sin(_time * 14f + along * 5f) * 2.5f * along);
        }

        var fade = _left < 1f && (int)(_time * 10) % 2 == 0 ? 0.4f : 1f;
        DrawPolyline(points, new Color(0.08f, 0.09f, 0.18f, fade), 4f);
        DrawPolyline(points, new Color(0.39f, 0.67f, 0.25f, fade), 2.5f);
    }

    /// <summary>
    /// Go after the enemy the hero hit last and punch it. Returns how to move, or null if there's nothing
    /// to attack (no hit yet, or that enemy is beaten, charmed or far away).
    /// </summary>
    private Vector2? Attack()
    {
        if (!IsInstanceValid(Caster) || Caster.LastHitEnemy is not { CanBeHit: true } enemy
            || enemy is GodotObject gone && !IsInstanceValid(gone)
            || enemy.HurtCenter.DistanceTo(HurtCenter) > 200)
        {
            return null;
        }

        var toEnemy = enemy.HurtCenter - HurtCenter;
        if (toEnemy.Length() > enemy.HurtRadius + 9f)
        {
            return toEnemy.Normalized() * 60f;
        }

        if (_punchCooldown <= 0)
        {
            _punchCooldown = PunchSeconds;
            _punchFlash = 0.1f;
            enemy.TakeHit(PunchDamage, GlobalPosition);
        }

        return toEnemy.Normalized() * 0.01f; // face it
    }

    /// <summary>Nothing to attack: wander about so enemies chase the clone away from the real hero.</summary>
    private Vector2 Wander(float dt)
    {
        if ((int)(_time * 1.5f) != (int)((_time - dt) * 1.5f))
        {
            _drift = Vector2.Right.Rotated(_rng.RandfRange(0, Mathf.Tau)) * 22f;
        }

        return _drift;
    }

    private void Vanish()
    {
        _left = 0;
        RemoveFromGroup(Enemies.LureGroup);
        QueueFree();
    }
}
