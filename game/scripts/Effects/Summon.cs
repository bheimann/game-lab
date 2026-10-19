using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;

namespace LizardWizard.Effects;

/// <summary>The creatures the moose's Summoner class can call (the Stampede is separate: see <see cref="Stampede"/>).</summary>
public enum SummonKind
{
    Squirrels,
    SpiritWolf,
    Bees,
    Owl,
    Wisps,
    Totem,
    Treant,
}

/// <summary>
/// A creature fighting for the moose. It goes after the enemy the moose hit last (or the nearest enemy close by),
/// and otherwise follows the moose around. Enemies can attack it like a hero; it stays until it's beaten (or the
/// moose sends it away). It follows the moose through doors.
/// - Squirrel swarm: fast little nibbles. Spirit wolf: strong bites. Bee swarm: flies, stings with poison.
/// - Owl: circles the moose and swoops at enemies. Element wisps: zap enemies from a distance, a different element each time.
/// - Antler totem: stays where it's put and heals heroes standing near it.
/// - Treant: big, slow and tough; enemies go after it first (like a decoy).
/// </summary>
public partial class Summon : CharacterBody2D, IEnemyTarget
{
    private static readonly DamageType[] WispElements = { DamageType.Fire, DamageType.Ice, DamageType.Lightning, DamageType.Arcane };
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static ulong _spawnCounter;

    private readonly RandomNumberGenerator _rng = new();
    private Stats _stats;
    private int _health;
    private float _time;
    private float _attackCooldown;
    private float _attackFlash;
    private float _hurtCooldown;
    private float _hurtFlash;
    private float _swoopBack;
    private int _wispElement;
    private bool _faceLeft;
    private Vector2 _zapTo;
    private DungeonRoom? _room;

    /// <summary>Everything about one kind of summon.</summary>
    /// <param name="Reach">How close (past the target's size) it gets before attacking; for wisps, how far they zap.</param>
    public readonly record struct Stats(string Name, string Description, float SummonCooldown, int Health, float Speed,
        int Damage, float AttackSeconds, float Reach, bool Flies, float Radius, float Height);

    public static Stats StatsFor(SummonKind kind) => kind switch
    {
        SummonKind.Squirrels => new("Squirrel Swarm", "Fast little nibbles", 3f, 6, 95f, 1, 0.4f, 4f, false, 7f, 5f),
        SummonKind.SpiritWolf => new("Spirit Wolf", "Runs at enemies and bites hard", 4f, 10, 85f, 3, 0.9f, 5f, false, 7f, 7f),
        SummonKind.Bees => new("Bee Swarm", "Flies, stings with poison", 3f, 4, 80f, 1, 1f, 2f, true, 6f, 14f),
        SummonKind.Owl => new("Owl", "Circles you and swoops at enemies", 4f, 6, 70f, 3, 1.2f, 3f, true, 6f, 20f),
        SummonKind.Wisps => new("Element Wisps", "Zap enemies with fire, ice, lightning and arcane", 4f, 5, 70f, 2, 1.3f, 80f, true, 6f, 16f),
        SummonKind.Totem => new("Antler Totem", "Stays put and heals heroes next to it", 10f, 12, 0f, 1, 2f, 40f, false, 6f, 10f),
        _ => new("Treant", "A walking tree: big, tough, and enemies go for it first", 15f, 30, 28f, 5, 1.5f, 6f, false, 10f, 14f),
    };

    public required Hero Caster { get; init; }

    public SummonKind Kind { get; init; }

    /// <summary>Goes up with every summon, so the oldest can be sent away when there are too many.</summary>
    public ulong Order { get; private set; }

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -_stats.Height);

    public float HurtRadius => _stats.Radius;

    public int HealthLeft => _health;

    public bool IsTargetable => _health > 0;

    /// <summary>The summons this hero has out, oldest first.</summary>
    public static List<Summon> Of(Hero caster) =>
        caster.GetTree().GetNodesInGroup(Enemies.SummonGroup).OfType<Summon>()
            .Where(s => s.Caster == caster && s._health > 0).OrderBy(s => s.Order).ToList();

    public override void _Ready()
    {
        _stats = StatsFor(Kind);
        _health = _stats.Health;
        Order = ++_spawnCounter;
        _rng.Seed = GetInstanceId();
        AddToGroup(Enemies.SummonGroup);
        if (Kind == SummonKind.Treant)
        {
            AddToGroup(Enemies.LureGroup); // enemies go for the treant first
        }

        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = 0;
        CollisionMask = _stats.Flies ? 0 : 1 | Pit.PitLayer;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8, 4) }, Position = new Vector2(0, -2) });
        _room = Dungeon.Find(this)?.RoomOf(Caster);

        // Pop in with a little puff.
        Scale = new Vector2(0.2f, 0.2f);
        CreateTween().TweenProperty(this, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (!IsInstanceValid(Caster) || !Caster.IsInGame)
        {
            Dismiss();
            return;
        }

        _time += dt;
        _attackCooldown -= dt;
        _attackFlash -= dt;
        _hurtCooldown -= dt;
        _hurtFlash -= dt;
        _swoopBack -= dt;
        FollowThroughDoors();

        Velocity = Kind switch
        {
            SummonKind.Totem => Heal(),
            SummonKind.Owl => Owl(),
            SummonKind.Wisps => Wisps(),
            _ => Fight(),
        };

        if (Mathf.Abs(Velocity.X) > 2f)
        {
            _faceLeft = Velocity.X < 0;
        }

        MoveAndSlide();
        QueueRedraw();
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (!IsTargetable || _hurtCooldown > 0)
        {
            return;
        }

        _health -= damage;
        _hurtCooldown = 0.4f;
        _hurtFlash = 0.15f;
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -8), $"-{damage}", new Color(1f, 0.6f, 0.4f), 0.6f, 6f);
        if (_health <= 0)
        {
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -12), "Poof!", new Color(0.8f, 1f, 0.8f), 0.8f, 8f);
            Dismiss();
        }
        else if (Kind != SummonKind.Totem)
        {
            GlobalPosition += (GlobalPosition - from).Normalized() * 4f; // a little knockback
        }
    }

    /// <summary>Go away (beaten, too many summons, or the moose changed class).</summary>
    public void Dismiss()
    {
        _health = 0;
        RemoveFromGroup(Enemies.SummonGroup);
        RemoveFromGroup(Enemies.LureGroup);
        QueueFree();
    }

    /// <summary>The moose went through a door: turn up next to it in the new room.</summary>
    private void FollowThroughDoors()
    {
        var room = Dungeon.Find(this)?.RoomOf(Caster);
        if (room is not null && room != _room)
        {
            _room = room;
            GlobalPosition = Caster.GlobalPosition + new Vector2(_rng.RandfRange(-14, 14), _rng.RandfRange(4, 10));
            ResetPhysicsInterpolation();
        }
    }

    /// <summary>The enemy to go after: the one the moose hit last, if it's close, or else the nearest one close by.</summary>
    private IHittable? FindEnemy(float range)
    {
        if (Caster.LastHitEnemy is { CanBeHit: true } last && (last is not GodotObject o || IsInstanceValid(o))
            && last.HurtCenter.DistanceTo(HurtCenter) <= range * 1.5f)
        {
            return last;
        }

        return GetTree().GetNodesInGroup(Enemies.Group).OfType<IHittable>()
            .Where(e => e.CanBeHit && e.HurtCenter.DistanceTo(HurtCenter) <= range)
            .OrderBy(e => e.HurtCenter.DistanceTo(HurtCenter))
            .FirstOrDefault();
    }

    /// <summary>Melee summons: run at the enemy and attack it, or stay near the moose.</summary>
    private Vector2 Fight()
    {
        var enemy = FindEnemy(110f);
        if (enemy is null)
        {
            return Follow();
        }

        var toEnemy = enemy.HurtCenter - HurtCenter;
        if (toEnemy.Length() > enemy.HurtRadius + HurtRadius + _stats.Reach)
        {
            return toEnemy.Normalized() * _stats.Speed;
        }

        if (_attackCooldown <= 0)
        {
            _attackCooldown = _stats.AttackSeconds;
            _attackFlash = 0.12f;
            enemy.TakeHit(_stats.Damage, GlobalPosition, Kind == SummonKind.Bees ? DamageType.Poison : DamageType.Physical);
        }

        return toEnemy.Normalized() * 0.01f; // face it
    }

    /// <summary>Stay close to the moose (each summon in its own spot round it), bobbing about.</summary>
    private Vector2 Follow()
    {
        var spot = Caster.GlobalPosition + Vector2.Right.Rotated(Order * 1.9f) * 20f;
        var toSpot = spot - GlobalPosition;
        return toSpot.Length() > 6f ? toSpot.Normalized() * Mathf.Min(_stats.Speed, toSpot.Length() * 4f) : Vector2.Zero;
    }

    /// <summary>Circle the moose; swoop at an enemy that comes close, then fly back.</summary>
    private Vector2 Owl()
    {
        var enemy = _swoopBack > 0 ? null : FindEnemy(120f);
        if (enemy is not null)
        {
            var toEnemy = enemy.HurtCenter - HurtCenter;
            if (toEnemy.Length() > enemy.HurtRadius + HurtRadius + _stats.Reach)
            {
                return toEnemy.Normalized() * _stats.Speed * 2.2f; // swoop
            }

            if (_attackCooldown <= 0)
            {
                _attackCooldown = _stats.AttackSeconds;
                _attackFlash = 0.12f;
                enemy.TakeHit(_stats.Damage, GlobalPosition);
                _swoopBack = 0.8f;
            }

            return Vector2.Zero;
        }

        var spot = Caster.GlobalPosition + Vector2.Right.Rotated(_time * 2.5f + Order) * 24f;
        var toSpot = spot - GlobalPosition;
        return toSpot.Normalized() * Mathf.Min(_stats.Speed * 2f, toSpot.Length() * 6f);
    }

    /// <summary>Hover near the moose and zap enemies in range, cycling fire, ice, lightning, arcane.</summary>
    private Vector2 Wisps()
    {
        var enemy = FindEnemy(_stats.Reach);
        if (enemy is not null && enemy.HurtCenter.DistanceTo(HurtCenter) <= _stats.Reach && _attackCooldown <= 0)
        {
            _attackCooldown = _stats.AttackSeconds;
            _attackFlash = 0.15f;
            _zapTo = enemy.HurtCenter;
            enemy.TakeHit(_stats.Damage, GlobalPosition, WispElements[_wispElement]);
            _wispElement = (_wispElement + 1) % WispElements.Length;
        }

        return Follow();
    }

    /// <summary>The totem doesn't move: it heals every hero standing near it now and then.</summary>
    private Vector2 Heal()
    {
        if (_attackCooldown <= 0)
        {
            _attackCooldown = _stats.AttackSeconds;
            foreach (var hero in GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
                         .Where(h => h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) <= _stats.Reach && h.Health.Current < h.Health.Max))
            {
                var healed = hero.Health.Heal(_stats.Damage);
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -10), $"+{healed}", new Color(0.45f, 1f, 0.45f), 0.8f, 6f);
                _attackFlash = 0.3f;
            }
        }

        return Vector2.Zero;
    }

    public override void _Draw()
    {
        // A soft shadow on the ground (higher fliers get a smaller one).
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, _stats.Flies ? 4f : _stats.Radius, new Color(0, 0, 0, 0.2f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        var flash = _hurtFlash > 0;
        var bob = _stats.Flies ? Mathf.Sin(_time * 6f) * 1.5f : 0f;
        DrawSetTransform(new Vector2(0, bob), 0, new Vector2(_faceLeft ? -1 : 1, 1));
        switch (Kind)
        {
            case SummonKind.Squirrels: DrawSquirrels(flash); break;
            case SummonKind.SpiritWolf: DrawWolf(flash); break;
            case SummonKind.Bees: DrawBees(); break;
            case SummonKind.Owl: DrawOwl(flash); break;
            case SummonKind.Wisps: DrawWisps(); break;
            case SummonKind.Totem: DrawTotem(flash); break;
            case SummonKind.Treant: DrawTreant(flash); break;
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        if (Kind == SummonKind.Wisps && _attackFlash > 0)
        {
            // The zap: a jagged line to the enemy in the element's colour.
            var colour = Abilities.GunAbility.ElementColour(WispElements[(_wispElement + WispElements.Length - 1) % WispElements.Length]);
            var from = new Vector2(0, -_stats.Height + bob);
            var to = ToLocal(_zapTo);
            var points = new List<Vector2> { from };
            for (var i = 1; i < 5; i++)
            {
                points.Add(from.Lerp(to, i / 5f) + new Vector2(_rng.RandfRange(-3, 3), _rng.RandfRange(-3, 3)));
            }

            points.Add(to);
            DrawPolyline(points.ToArray(), colour, 1.5f);
        }

        if (Kind == SummonKind.Totem)
        {
            // The healing ring.
            DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.5f));
            DrawArc(Vector2.Zero, _stats.Reach, 0, Mathf.Tau, 32, new Color(0.45f, 1f, 0.45f, 0.2f + 0.15f * Mathf.Sin(_time * 3f) + (_attackFlash > 0 ? 0.4f : 0f)), 1f);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        // A small health bar once it's been hurt.
        if (_health < _stats.Health)
        {
            var top = -_stats.Height - _stats.Radius - 8f;
            DrawRect(new Rect2(-7, top, 14, 2), Outline);
            DrawRect(new Rect2(-7, top, 14f * _health / _stats.Health, 2), new Color(0.45f, 1f, 0.45f));
        }
    }

    private Color Tint(Color colour, bool flash) => flash ? new Color(1f, 0.5f, 0.5f) : colour;

    private void DrawSquirrels(bool flash)
    {
        var fur = Tint(new Color(0.72f, 0.42f, 0.2f), flash);
        for (var i = 0; i < 3; i++)
        {
            // Three squirrels scurrying round each other.
            var at = new Vector2(-5 + i * 5, -2 - (i % 2) * 3) + new Vector2(Mathf.Sin(_time * 9f + i * 2f), Mathf.Cos(_time * 7f + i)) * 1.5f;
            DrawCircle(at + new Vector2(-3, -3), 2.5f, fur.Darkened(0.15f)); // bushy tail
            DrawCircle(at, 2f, fur);
            DrawCircle(at + new Vector2(2, -1.5f), 1.3f, fur.Lightened(0.15f));
            DrawCircle(at + new Vector2(2.5f, -2f), 0.5f, Outline);
        }
    }

    private void DrawWolf(bool flash)
    {
        // A glowing, see-through blue wolf.
        var body = Tint(new Color(0.7f, 0.85f, 1f, 0.85f), flash);
        var step = Mathf.Sin(_time * 14f) * (Velocity.Length() > 5 ? 1.5f : 0f);
        DrawCircle(new Vector2(0, -6), 9f, new Color(0.6f, 0.8f, 1f, 0.15f));
        DrawLine(new Vector2(-4, -4), new Vector2(-4 + step, 0), body, 1.5f);
        DrawLine(new Vector2(3, -4), new Vector2(3 - step, 0), body, 1.5f);
        DrawRect(new Rect2(-6, -8, 11, 5), body);
        DrawLine(new Vector2(-6, -7), new Vector2(-9, -10), body, 1.5f); // tail
        DrawCircle(new Vector2(6, -9), 3f, body);
        DrawColoredPolygon(new[] { new Vector2(4, -11), new Vector2(5, -14), new Vector2(7, -11) }, body);
        DrawRect(new Rect2(8, -9, 3, 2), body); // snout
        DrawCircle(new Vector2(7, -10), 0.7f, new Color(0.2f, 0.4f, 0.9f));
        if (_attackFlash > 0)
        {
            DrawArc(new Vector2(12, -8), 3f, -1f, 1f, 8, Colors.White, 1f);
        }
    }

    private void DrawBees()
    {
        for (var i = 0; i < 5; i++)
        {
            var at = new Vector2(0, -_stats.Height) + new Vector2(Mathf.Sin(_time * 11f + i * 1.3f) * 5f, Mathf.Cos(_time * 13f + i * 2.1f) * 3f);
            DrawCircle(at + new Vector2(0, -1.2f), 1f, new Color(1f, 1f, 1f, 0.7f)); // wing
            DrawCircle(at, 1.2f, new Color(1f, 0.85f, 0.2f));
            DrawLine(at + new Vector2(-0.3f, -1), at + new Vector2(-0.3f, 1), Outline, 0.6f);
        }
    }

    private void DrawOwl(bool flash)
    {
        var y = -_stats.Height;
        var feather = Tint(new Color(0.55f, 0.4f, 0.28f), flash);
        var flap = Mathf.Sin(_time * 12f) * 3f;
        DrawColoredPolygon(new[] { new Vector2(-3, y), new Vector2(-9, y - 2 + flap), new Vector2(-4, y + 3) }, feather.Darkened(0.2f));
        DrawColoredPolygon(new[] { new Vector2(3, y), new Vector2(9, y - 2 + flap), new Vector2(4, y + 3) }, feather.Darkened(0.2f));
        DrawCircle(new Vector2(0, y), 5f, feather);
        DrawCircle(new Vector2(0, y - 1), 3.5f, new Color(0.9f, 0.82f, 0.65f));
        DrawCircle(new Vector2(-1.5f, y - 1.5f), 1.3f, Colors.White);
        DrawCircle(new Vector2(1.5f, y - 1.5f), 1.3f, Colors.White);
        DrawCircle(new Vector2(-1.5f, y - 1.5f), 0.6f, Outline);
        DrawCircle(new Vector2(1.5f, y - 1.5f), 0.6f, Outline);
        DrawColoredPolygon(new[] { new Vector2(-0.8f, y), new Vector2(0.8f, y), new Vector2(0, y + 1.5f) }, new Color(0.95f, 0.7f, 0.2f));
    }

    private void DrawWisps()
    {
        for (var i = 0; i < WispElements.Length; i++)
        {
            var colour = Abilities.GunAbility.ElementColour(WispElements[i]);
            var at = new Vector2(0, -_stats.Height) + Vector2.Right.Rotated(_time * 3f + i * Mathf.Tau / WispElements.Length) * 5f;
            DrawCircle(at, 3f, colour with { A = 0.3f });
            DrawCircle(at, 1.5f, colour);
            DrawCircle(at, 0.7f, Colors.White);
        }
    }

    private void DrawTotem(bool flash)
    {
        // A carved wooden pole with a face and antlers on top.
        var wood = Tint(new Color(0.55f, 0.36f, 0.22f), flash);
        DrawRect(new Rect2(-3, -18, 6, 18), wood);
        DrawRect(new Rect2(-3, -18, 6, 18), Outline, false, 1f);
        DrawLine(new Vector2(-3, -6), new Vector2(3, -6), wood.Darkened(0.35f), 1f);
        DrawLine(new Vector2(-3, -12), new Vector2(3, -12), wood.Darkened(0.35f), 1f);
        DrawCircle(new Vector2(-1.2f, -15), 0.7f, Outline);
        DrawCircle(new Vector2(1.2f, -15), 0.7f, Outline);
        var antler = new Color(0.93f, 0.85f, 0.6f);
        DrawPolyline(new[] { new Vector2(-2, -18), new Vector2(-6, -22), new Vector2(-8, -21) }, antler, 1.2f);
        DrawLine(new Vector2(-5, -21), new Vector2(-5, -25), antler, 1.2f);
        DrawPolyline(new[] { new Vector2(2, -18), new Vector2(6, -22), new Vector2(8, -21) }, antler, 1.2f);
        DrawLine(new Vector2(5, -21), new Vector2(5, -25), antler, 1.2f);
    }

    private void DrawTreant(bool flash)
    {
        // A walking tree: a trunk with a face, branch arms (swinging when it hits) and a leafy top.
        var bark = Tint(new Color(0.48f, 0.32f, 0.2f), flash);
        var leaves = Tint(new Color(0.35f, 0.62f, 0.25f), flash);
        var step = Mathf.Sin(_time * 6f) * (Velocity.Length() > 5 ? 1.5f : 0f);
        var swing = _attackFlash > 0 ? 5f : 0f;
        DrawRect(new Rect2(-5 + step, -4, 3, 4), bark.Darkened(0.2f));
        DrawRect(new Rect2(2 - step, -4, 3, 4), bark.Darkened(0.2f));
        DrawRect(new Rect2(-6, -20, 12, 17), bark);
        DrawRect(new Rect2(-6, -20, 12, 17), Outline, false, 1f);
        DrawLine(new Vector2(6, -15), new Vector2(12 + swing, -19 + swing), bark, 2f);
        DrawLine(new Vector2(-6, -15), new Vector2(-11, -19), bark, 2f);
        DrawCircle(new Vector2(-2, -14), 1.2f, new Color(1f, 0.9f, 0.4f));
        DrawCircle(new Vector2(2.5f, -14), 1.2f, new Color(1f, 0.9f, 0.4f));
        DrawLine(new Vector2(-2, -10), new Vector2(3, -10), Outline, 1f);
        DrawCircle(new Vector2(0, -26), 10f, leaves);
        DrawCircle(new Vector2(-6, -22), 6f, leaves.Darkened(0.1f));
        DrawCircle(new Vector2(6, -23), 6f, leaves.Darkened(0.1f));
        DrawCircle(new Vector2(-3, -30), 3f, leaves.Lightened(0.2f));
    }
}
