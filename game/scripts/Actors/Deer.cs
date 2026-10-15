using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;
using LizardWizard.Util;

namespace LizardWizard.Actors;

/// <summary>
/// A deer tricked into fighting the lizards. Wanders until it spots a lizard, then chases it and kicks.
/// When its health runs out it falls asleep; once the area is clear it wakes up and says thanks.
/// </summary>
public partial class Deer : CharacterBody2D, IHittable, IEnemy, IPullable, IEnemyTarget, ICharmable
{
    private const string WalkStrip = "res://assets/sprites/deer/deer_walk.png";

    private static readonly string[] Thanks =
    {
        "Thank you!",
        "I feel like myself again!",
        "What happened? Thanks, lizards!",
        "That Mossimus tricked me!",
    };

    private readonly RandomNumberGenerator _rng = new();
    private AnimatedSprite2D _sprite = null!;
    private IEnemyTarget? _target;
    private float _charmLeft;
    private Hero? _charmedBy;
    private Vector2 _wanderDirection;
    private float _wanderTimer;
    private float _windUp = -1f;
    private float _kickCooldown;
    private float _hurtFlash;
    private float _zzzTimer;
    private Vector2 _knockback;
    private Vector2? _pull;
    private Vector2 _home;
    private readonly ElementEffects _effects = new();

    public enum Mood
    {
        Wandering,
        Chasing,
        Asleep,
        Thankful,
    }

    [Export] public float WanderSpeed { get; set; } = 18f;

    [Export] public float ChaseSpeed { get; set; } = 42f;

    /// <summary>Each deer gets a random health between MinHealth and MaxHealth.</summary>
    [Export] public int MinHealth { get; set; } = 10;

    [Export] public int MaxHealth { get; set; } = 15;

    /// <summary>How close a lizard has to be for the deer to notice it.</summary>
    [Export] public float SightRange { get; set; } = 90f;

    /// <summary>How far away a lizard has to get for the deer to give up the chase.</summary>
    [Export] public float LoseSightRange { get; set; } = 150f;

    [Export] public int KickDamage { get; set; } = 3;

    /// <summary>How long the deer flashes before it kicks, so players can dodge.</summary>
    [Export] public float WindUpSeconds { get; set; } = 0.4f;

    [Export] public float KickCooldownSeconds { get; set; } = 1f;

    public Health Health { get; private set; } = null!;

    public Mood CurrentMood { get; private set; } = Mood.Wandering;

    public bool IsHostile => CurrentMood is Mood.Wandering or Mood.Chasing;

    public bool IsAsleep => CurrentMood == Mood.Asleep;

    /// <summary>Charmed deer are on the heroes' side for a while, so the heroes' attacks pass them by.</summary>
    public bool CanBeHit => IsHostile && !IsCharmed;

    public bool IsCharmed => _charmLeft > 0;

    /// <summary>Charmed deer go after the other deer.</summary>
    public bool IsTargetable => IsHostile && !IsCharmed;

    public bool CanBeCharmed => IsHostile && !IsCharmed;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -16);

    public float HurtRadius => 11f;

    public bool CanBePulled => true;

    /// <summary>Flashing yellow, about to kick: the moment to dodge.</summary>
    public bool IsWindingUp => _windUp >= 0;

    public bool IsTargeting(Hero lizard) => CurrentMood == Mood.Chasing && !IsCharmed && ReferenceEquals(_target, lizard);

    public override void _Ready()
    {
        AddToGroup(Enemies.Group);
        _home = Position;
        _rng.Seed = (ulong)GetInstanceId() ^ Time.GetTicksUsec();
        Health = new Health(_rng.RandiRange(MinHealth, MaxHealth));
        Health.Changed += QueueRedraw;
        MotionMode = MotionModeEnum.Floating;
        CollisionMask |= Dungeons.Pit.PitLayer;

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = SpriteSheet.Frames("walk", WalkStrip, 40, 5),
            Offset = new Vector2(0, -21), // feet at the node's origin
        };
        AddChild(_sprite);
        _sprite.Play("walk");

        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(18, 6) }, Position = new Vector2(0, -3) });
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _kickCooldown -= dt;
        _hurtFlash -= dt;
        if (IsCharmed)
        {
            _charmLeft -= dt;
            if (!IsCharmed)
            {
                // The charm wore off.
                _target = null;
                _windUp = -1f;
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -38), "?", new Color(1f, 0.6f, 0.85f), 0.8f, 6f);
            }
        }

        if (_pull is { } pull)
        {
            // Caught by a tongue: get dragged along and do nothing else this frame.
            _pull = null;
            Velocity = pull;
            MoveAndSlide();
            UpdateLook();
            return;
        }

        // Poison and fire keep hurting for a while.
        var overTime = _effects.Tick(dt);
        if (overTime > 0 && IsHostile)
        {
            Hurt(overTime, _effects.IsBurning ? new Color(1f, 0.6f, 0.4f) : new Color(0.65f, 1f, 0.5f));
            if (!IsHostile)
            {
                return;
            }
        }

        var desired = CurrentMood switch
        {
            Mood.Wandering => Wander(dt, lookForLizards: true),
            Mood.Chasing => Chase(dt),
            Mood.Asleep => Sleep(dt),
            _ => Wander(dt, lookForLizards: false),
        };

        Velocity = desired * _effects.SpeedFactor + _knockback;
        _knockback = _knockback.MoveToward(Vector2.Zero, 500f * dt);
        MoveAndSlide();
        UpdateLook();
    }

    public override void _Draw()
    {
        if (IsCharmed)
        {
            // A little heart over its head.
            var pink = new Color(1f, 0.45f, 0.7f);
            DrawCircle(new Vector2(-2, -46), 2.2f, pink);
            DrawCircle(new Vector2(2, -46), 2.2f, pink);
            DrawColoredPolygon(new[] { new Vector2(-4, -45.5f), new Vector2(4, -45.5f), new Vector2(0, -41) }, pink);
        }

        // Health bar over the deer once it's been hurt.
        if (!IsHostile || Health.Current == Health.Max)
        {
            return;
        }

        var bar = new Rect2(-10, -40, 20, 3);
        DrawRect(bar.Grow(1), new Color(0.08f, 0.09f, 0.18f));
        DrawRect(new Rect2(bar.Position, bar.Size with { X = bar.Size.X * Health.Current / Health.Max }), new Color(0.39f, 0.67f, 0.25f));
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (!CanBeHit)
        {
            return;
        }

        var boosted = ElementEffects.Boost(damage, type);
        _effects.Hit(type);
        var dealt = Health.Damage(boosted);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -10), boosted > damage ? $"-{dealt}!" : $"-{dealt}", Colors.White);
        _hurtFlash = 0.15f;

        // A deer winding up a kick is braced: hits don't stop the kick or push it back. Dodge instead!
        if (_windUp < 0)
        {
            _knockback = (GlobalPosition - from).Normalized() * 110f;
        }

        if (Health.IsEmpty)
        {
            FallAsleep();
            return;
        }

        // Getting hit makes the deer notice whoever hit it.
        if (CurrentMood != Mood.Chasing)
        {
            var attacker = Lizards().OrderBy(l => l.GlobalPosition.DistanceTo(from)).FirstOrDefault();
            if (attacker is not null)
            {
                StartChasing(attacker);
            }
        }
    }

    public void Charm(float seconds, Hero by)
    {
        _charmLeft = seconds;
        _charmedBy = by;
        _target = null;
        _windUp = -1f;
        CurrentMood = Mood.Chasing;
        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -38), "Charmed!", new Color(1f, 0.6f, 0.85f), 1f, 8f);
        QueueRedraw();
    }

    public void Pull(Vector2 velocity)
    {
        // Being yanked off your feet spoils a kick.
        _pull = velocity;
        _windUp = -1f;
        _knockback = Vector2.Zero;
    }

    public void Respawn()
    {
        Position = _home;
        _effects.Clear();
        Health.Refill();
        CurrentMood = Mood.Wandering;
        _target = null;
        _charmLeft = 0;
        _windUp = -1f;
        _kickCooldown = 0;
        _knockback = Vector2.Zero;
        _pull = null;
        _wanderTimer = 0;
        ResetPhysicsInterpolation();
        QueueRedraw();
    }

    public void WakeUpAfter(double seconds)
    {
        GetTree().CreateTimer(seconds).Timeout += WakeUp;
    }

    private Vector2 Wander(float dt, bool lookForLizards)
    {
        if (lookForLizards && FindTarget() is { } seen)
        {
            StartChasing(seen);
            return Vector2.Zero;
        }

        _wanderTimer -= dt;
        if (_wanderTimer <= 0)
        {
            // Half the time stand and graze, half the time stroll somewhere.
            _wanderDirection = _rng.Randf() < 0.5f ? Vector2.Zero : Vector2.Right.Rotated(_rng.RandfRange(0, Mathf.Tau));
            _wanderTimer = _rng.RandfRange(1f, 3f);
        }

        return _wanderDirection * WanderSpeed;
    }

    private Vector2 Chase(float dt)
    {
        if (_target is GodotObject gone && !IsInstanceValid(gone))
        {
            // The player dropped out of the game, or a decoy vanished.
            _target = null;
        }

        // A decoy in sight is more interesting than a hero.
        if (!IsCharmed && _target is Hero && FindLure() is { } lure)
        {
            _target = lure;
        }

        if (IsCharmed && (_target is null || !_target.IsTargetable))
        {
            // Charmed: go after another deer, or tag along with the lizard who charmed it.
            _target = FindTarget();
            if (_target is null)
            {
                _windUp = -1f;
                var buddy = _charmedBy is not null && IsInstanceValid(_charmedBy) ? _charmedBy.GlobalPosition : GlobalPosition;
                return buddy.DistanceTo(GlobalPosition) > 30 ? (buddy - GlobalPosition).Normalized() * ChaseSpeed : Vector2.Zero;
            }
        }

        if (_target is null || !_target.IsTargetable || _target.GlobalPosition.DistanceTo(GlobalPosition) > LoseSightRange)
        {
            if (_target is Hero { IsDown: false, IsVisibleToEnemies: false })
            {
                // It vanished (camouflage)!
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -38), "?", new Color(1f, 0.85f, 0.3f), 0.8f, 6f);
            }

            _target = null;
            CurrentMood = IsCharmed ? Mood.Chasing : Mood.Wandering;
            _windUp = -1f;
            return Vector2.Zero;
        }

        var inReach = _target.HurtCenter.DistanceTo(HurtCenter) <= HurtRadius + _target.HurtRadius + 6f;

        if (_windUp >= 0)
        {
            _windUp += dt;
            if (_windUp >= WindUpSeconds)
            {
                if (inReach)
                {
                    _target.TakeHit(KickDamage, GlobalPosition);
                }

                _windUp = -1f;
                _kickCooldown = KickCooldownSeconds;
            }

            return Vector2.Zero;
        }

        if (inReach)
        {
            if (_kickCooldown <= 0)
            {
                _windUp = 0f;
            }

            return Vector2.Zero;
        }

        return (_target.GlobalPosition - GlobalPosition).Normalized() * ChaseSpeed;
    }

    private Vector2 Sleep(float dt)
    {
        _zzzTimer -= dt;
        if (_zzzTimer <= 0)
        {
            _zzzTimer = 1.2f;
            FloatingText.Spawn(this, GlobalPosition + new Vector2(8, -28), "z", new Color(0.6f, 0.75f, 1f), 1.2f, 10f);
        }

        return Vector2.Zero;
    }

    private void StartChasing(IEnemyTarget target)
    {
        _target = target;
        CurrentMood = Mood.Chasing;
        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -38), "!", new Color(1f, 0.85f, 0.3f), 0.6f, 6f);
    }

    /// <summary>Damage over time (poison, fire): no knockback, doesn't make the deer notice anyone.</summary>
    private void Hurt(int damage, Color colour)
    {
        var dealt = Health.Damage(damage);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -10), $"-{dealt}", colour, 0.6f, 8f);
        if (Health.IsEmpty)
        {
            FallAsleep();
        }
    }

    private void FallAsleep()
    {
        _effects.Clear();
        _charmLeft = 0;
        CurrentMood = Mood.Asleep;
        _target = null;
        _zzzTimer = 0.3f;
        QueueRedraw();
        Enemies.CheckCleared(this);
    }

    private void WakeUp()
    {
        if (CurrentMood != Mood.Asleep)
        {
            return; // respawned before it woke up
        }

        CurrentMood = Mood.Thankful;
        _wanderTimer = 2.5f; // stand still while talking
        _wanderDirection = Vector2.Zero;
        var line = Thanks[_rng.RandiRange(0, Thanks.Length - 1)];
        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -36), line, Colors.White, 2.5f, 6f);
    }

    private void UpdateLook()
    {
        var moving = Velocity.Length() > 2f && _knockback.Length() < 1f;
        if (moving && !_sprite.IsPlaying())
        {
            _sprite.Play("walk");
        }
        else if (!moving && _sprite.IsPlaying())
        {
            _sprite.Pause();
        }

        if (Mathf.Abs(Velocity.X) > 1f && _knockback.Length() < 1f)
        {
            _sprite.FlipH = Velocity.X < 0; // the art faces right
        }

        _sprite.Modulate = CurrentMood == Mood.Asleep ? new Color(0.7f, 0.75f, 1f)
            : _hurtFlash > 0 ? new Color(1f, 0.45f, 0.45f)
            : _windUp >= 0 ? new Color(1f, 1f, 0.55f)
            : IsCharmed ? new Color(1f, 0.7f, 0.9f)
            : _effects.Tint ?? Colors.White;
        _sprite.Scale = CurrentMood == Mood.Asleep ? new Vector2(1f, 0.85f) : Vector2.One;
    }

    /// <summary>What to go after: charmed, the nearest other deer; otherwise a decoy in sight, then the nearest hero
    /// or summoned creature in sight.</summary>
    private IEnemyTarget? FindTarget()
    {
        if (IsCharmed)
        {
            return GetTree().GetNodesInGroup(Enemies.Group).OfType<IEnemyTarget>()
                .Where(e => !ReferenceEquals(e, this) && e.IsTargetable && e.GlobalPosition.DistanceTo(GlobalPosition) <= LoseSightRange)
                .OrderBy(e => e.GlobalPosition.DistanceTo(GlobalPosition))
                .FirstOrDefault();
        }

        return FindLure() ?? Lizards()
            .Where(l => l.IsVisibleToEnemies).Cast<IEnemyTarget>()
            .Concat(Enemies.Summons(GetTree()))
            .Where(t => t.GlobalPosition.DistanceTo(GlobalPosition) <= SightRange)
            .OrderBy(t => t.GlobalPosition.DistanceTo(GlobalPosition))
            .FirstOrDefault();
    }

    private IEnemyTarget? FindLure() =>
        Enemies.Lures(GetTree())
            .Where(l => l.GlobalPosition.DistanceTo(GlobalPosition) <= SightRange)
            .OrderBy(l => l.GlobalPosition.DistanceTo(GlobalPosition))
            .FirstOrDefault();

    private Hero[] Lizards() =>
        GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(l => l.IsInGame && !l.IsDown).ToArray();
}
