using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;
using LizardWizard.Util;

namespace LizardWizard.Actors;

/// <summary>
/// An animal tricked into fighting the heroes: deer, boars, arctic foxes, frogs, snakes. Everything they share lives
/// here: wandering until it spots a hero, chasing, getting hurt, charms, tongue pulls, the Thief's stealing, elements,
/// falling asleep (and dropping loot), waking up thankful, and coming back when the heroes return. How each one
/// actually fights is <see cref="Fight"/>. Their art is a side view facing right (mirrored for left).
/// </summary>
public abstract partial class Beast : CharacterBody2D, IHittable, IEnemy, IPullable, IEnemyTarget, ICharmable, IStealable
{
    private static readonly string[] Thanks =
    {
        "Thank you!",
        "I feel like myself again!",
        "What happened? Thanks, heroes!",
        "That Mossimus tricked me!",
    };

    protected readonly RandomNumberGenerator Rng = new();
    private AnimatedSprite2D _sprite = null!;
    private IEnemyTarget? _target;
    private float _charmLeft;
    private Hero? _charmedBy;
    private Vector2 _wanderDirection;
    private float _wanderTimer;
    private float _hurtFlash;
    private float _zzzTimer;
    private Vector2 _knockback;
    private Vector2? _pull;
    private Vector2 _home;
    private readonly ElementEffects _effects = new();
    private System.Collections.Generic.List<Vector2> _path = new();
    private float _pathLeft;
    private float _stuckFor;
    private readonly Pockets _pockets = new();

    public enum Mood
    {
        Wandering,
        Chasing,
        Asleep,
        Thankful,
    }

    [Export] public float WanderSpeed { get; set; } = 18f;

    [Export] public float ChaseSpeed { get; set; } = 42f;

    /// <summary>Each one gets a random health between MinHealth and MaxHealth.</summary>
    [Export] public int MinHealth { get; set; } = 10;

    [Export] public int MaxHealth { get; set; } = 15;

    /// <summary>How close a hero has to be for it to notice.</summary>
    [Export] public float SightRange { get; set; } = 90f;

    /// <summary>How far away a hero has to get for it to give up the chase.</summary>
    [Export] public float LoseSightRange { get; set; } = 150f;

    /// <summary>How long it can be stuck (behind a tree, a rock, water) before it looks for a way round.</summary>
    [Export] public float StuckSeconds { get; set; } = 0.75f;

    public Health Health { get; private set; } = null!;

    /// <summary>Where it lives: decides the element it's weak to and the one it resists (see <see cref="Climates"/>).</summary>
    public Climate Climate { get; private set; }

    /// <summary>Animals that always come from somewhere (arctic foxes are cold wherever they are); null = the area's.</summary>
    protected virtual Climate? NativeClimate => null;

    public Mood CurrentMood { get; private set; } = Mood.Wandering;

    public bool IsHostile => CurrentMood is Mood.Wandering or Mood.Chasing;

    public bool IsAsleep => CurrentMood == Mood.Asleep;

    /// <summary>Charmed ones are on the heroes' side for a while, so the heroes' attacks pass them by.</summary>
    public bool CanBeHit => IsHostile && !IsCharmed;

    public bool IsCharmed => _charmLeft > 0;

    /// <summary>Charmed ones go after the other enemies.</summary>
    public bool IsTargetable => IsHostile && !IsCharmed;

    public virtual bool CanBeCharmed => IsHostile && !IsCharmed;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -BodyHeight / 2);

    public virtual float HurtRadius => 11f;

    public virtual bool CanBePulled => true;

    /// <summary>Flashing yellow, about to attack: the moment to dodge.</summary>
    public bool IsWindingUp => WindUp >= 0;

    /// <summary>Seconds into an attack's wind-up (-1 = not winding up). Fight code sets and uses it.</summary>
    protected float WindUp { get; set; } = -1f;

    /// <summary>The waiting time before it can attack again. Fight code sets it; it counts down by itself.</summary>
    protected float AttackCooldown { get; set; }

    /// <summary>How tall it is, in pixels (hurt box, health bar, the "!" and "z"s).</summary>
    protected abstract float BodyHeight { get; }

    /// <summary>The walk strip: a side view facing right, <see cref="FrameSize"/> per frame.</summary>
    protected abstract string WalkStrip { get; }

    protected abstract Vector2I FrameSize { get; }

    /// <summary>The monster parts it drops when beaten (one of each).</summary>
    protected abstract Items.MonsterPart[] Parts { get; }

    public abstract string StealableMove { get; }

    public virtual int StealableCoins => 3;

    /// <summary>Faint while hidden (a snake in the grass).</summary>
    protected virtual float Alpha => 1f;

    /// <summary>How big it's drawn (bosses are bigger), and its colour when nothing else is tinting it.</summary>
    protected virtual Vector2 SpriteScale => Vector2.One;

    protected virtual Color BaseTint => Colors.White;

    /// <summary>What it might say when it wakes up after being beaten (one picked at random).</summary>
    protected virtual string[] ThankLines => Thanks;

    /// <summary>Beaten: what it drops (one of each of its parts, and coins or a herb). Bosses drop more.</summary>
    protected virtual void DropLoot() => Loot.DropFrom(this, parts: Parts);

    /// <summary>Loading a save: it was already beaten, so it's back to its friendly self.</summary>
    protected void SetBeaten()
    {
        CurrentMood = Mood.Thankful;
        StopAttack();
        QueueRedraw();
    }

    /// <summary>The attack: with a hero (or decoy) to go after, return how to move this frame.</summary>
    protected abstract Vector2 Fight(float dt, IEnemyTarget target);

    /// <summary>Called when it loses its target or gets interrupted: stop any attack in progress.</summary>
    protected virtual void StopAttack() => WindUp = -1f;

    public bool IsTargeting(Hero hero) => CurrentMood == Mood.Chasing && !IsCharmed && ReferenceEquals(_target, hero);

    public override void _Ready()
    {
        AddToGroup(Enemies.Group);
        _home = Position;
        Rng.Seed = (ulong)GetInstanceId() ^ Time.GetTicksUsec();
        Health = new Health(Rng.RandiRange(MinHealth, MaxHealth));
        Health.Changed += QueueRedraw;
        Climate = NativeClimate ?? Climates.Of(this);
        MotionMode = MotionModeEnum.Floating;
        CollisionMask |= Dungeons.Pit.PitLayer | Rooms.OutdoorBuilder.EnemyWallLayer; // stay out of pits and in their area

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = SpriteSheet.Frames("walk", WalkStrip, FrameSize.X, 5),
            Offset = new Vector2(0, -FrameSize.Y / 2f), // feet at the node's origin
        };
        AddChild(_sprite);
        _sprite.Play("walk");

        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Mathf.Min(18, FrameSize.X - 4), 6) }, Position = new Vector2(0, -3) });
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        AttackCooldown -= dt;
        _hurtFlash -= dt;
        if (IsCharmed)
        {
            _charmLeft -= dt;
            if (!IsCharmed)
            {
                // The charm wore off.
                _target = null;
                StopAttack();
                FloatingText.Spawn(this, TextAt, "?", new Color(1f, 0.6f, 0.85f), 0.8f, 6f);
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
            Mood.Wandering => Wander(dt, lookForHeroes: true),
            Mood.Chasing => Chase(dt),
            Mood.Asleep => Sleep(dt),
            _ => Wander(dt, lookForHeroes: false),
        };

        desired = FollowPath(dt, desired);
        var before = GlobalPosition;
        Velocity = desired * _effects.SpeedFactor + _knockback;
        _knockback = _knockback.MoveToward(Vector2.Zero, 500f * dt);
        MoveAndSlide();
        CheckStuck(dt, desired, GlobalPosition - before);
        UpdateLook();
    }

    /// <summary>
    /// While it has a way round something, walking toward the target goes along it instead (only plain walking toward
    /// the target: charges, hops and dashes are left alone). The way is dropped after a few seconds, or at its end.
    /// </summary>
    private Vector2 FollowPath(float dt, Vector2 desired)
    {
        _pathLeft -= dt;
        if (_path.Count == 0 || _pathLeft <= 0 || CurrentMood != Mood.Chasing || _target is null)
        {
            _path.Clear();
            return desired;
        }

        var toTarget = _target.GlobalPosition - GlobalPosition;
        var walkingAtIt = desired.Length() > 1f && desired.Normalized().Dot(toTarget.Normalized()) > 0.7f;
        if (!walkingAtIt)
        {
            return desired;
        }

        while (_path.Count > 0 && _path[0].DistanceTo(GlobalPosition) < 6f)
        {
            _path.RemoveAt(0);
        }

        return _path.Count == 0 ? desired : (_path[0] - GlobalPosition).Normalized() * desired.Length();
    }

    /// <summary>Trying to move but hardly getting anywhere for <see cref="StuckSeconds"/>: chasing, find a way round;
    /// wandering, pick another direction.</summary>
    private void CheckStuck(float dt, Vector2 desired, Vector2 moved)
    {
        var trying = desired.Length() > 5f && _knockback.Length() < 1f;
        _stuckFor = trying && moved.Length() < desired.Length() * dt * 0.3f ? _stuckFor + dt : 0f;
        if (_stuckFor < StuckSeconds)
        {
            return;
        }

        _stuckFor = 0f;
        if (CurrentMood == Mood.Chasing && _target is not null)
        {
            var feet = new Vector2(Mathf.Min(18, FrameSize.X - 4), 6);
            _path = GridPath.Find(this, GlobalPosition, _target.GlobalPosition, feet, CollisionMask);
            _pathLeft = 4f;
        }
        else if (CurrentMood == Mood.Wandering)
        {
            _wanderTimer = 0f; // try somewhere else
        }
    }

    /// <summary>Where floating text over its head goes.</summary>
    protected Vector2 TextAt => GlobalPosition + new Vector2(0, -BodyHeight - 12);

    public override void _Draw()
    {
        var top = -BodyHeight - 6;
        if (IsCharmed)
        {
            // A little heart over its head.
            var pink = new Color(1f, 0.45f, 0.7f);
            DrawCircle(new Vector2(-2, top - 6), 2.2f, pink);
            DrawCircle(new Vector2(2, top - 6), 2.2f, pink);
            DrawColoredPolygon(new[] { new Vector2(-4, top - 5.5f), new Vector2(4, top - 5.5f), new Vector2(0, top - 1) }, pink);
        }

        // Health bar once it's been hurt.
        if (!IsHostile || Health.Current == Health.Max)
        {
            return;
        }

        var bar = new Rect2(-10, top, 20, 3);
        DrawRect(bar.Grow(1), new Color(0.08f, 0.09f, 0.18f));
        DrawRect(new Rect2(bar.Position, bar.Size with { X = bar.Size.X * Health.Current / Health.Max }), new Color(0.39f, 0.67f, 0.25f));
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (!CanBeHit)
        {
            return;
        }

        // Its weak element hurts double; the one it resists hurts half and doesn't burn or slow it.
        var boosted = ElementEffects.Boost(damage, type);
        var weak = type == Climates.Weakness(Climate);
        var resists = type == Climates.Resistance(Climate);
        if (weak)
        {
            boosted *= 2;
        }
        else if (resists)
        {
            boosted = Mathf.Max(1, boosted / 2);
        }

        if (!resists)
        {
            _effects.Hit(type);
        }

        var dealt = Health.Damage(boosted);
        var text = weak ? $"Weak! -{dealt}" : resists ? $"Resist -{dealt}" : boosted > damage ? $"-{dealt}!" : $"-{dealt}";
        var colour = weak ? new Color(1f, 0.6f, 0.2f) : resists ? new Color(0.7f, 0.7f, 0.75f) : Colors.White;
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -10), text, colour);
        _hurtFlash = 0.15f;

        // Winding up an attack, it's braced: hits don't stop it or push it back. Dodge instead!
        if (WindUp < 0)
        {
            Knock((GlobalPosition - from).Normalized() * 110f);
        }

        if (Health.IsEmpty)
        {
            FallAsleep();
            return;
        }

        // Getting hit makes it notice whoever hit it.
        if (CurrentMood != Mood.Chasing)
        {
            var attacker = Heroes().OrderBy(l => l.GlobalPosition.DistanceTo(from)).FirstOrDefault();
            if (attacker is not null)
            {
                StartChasing(attacker);
            }
        }
    }

    public bool TrySteal(StealKind kind) => _pockets.TryTake(kind);

    public void Charm(float seconds, Hero by)
    {
        _charmLeft = seconds;
        _charmedBy = by;
        _target = null;
        StopAttack();
        CurrentMood = Mood.Chasing;
        FloatingText.Spawn(this, TextAt, "Charmed!", new Color(1f, 0.6f, 0.85f), 1f, 8f);
        QueueRedraw();
    }

    public void Pull(Vector2 velocity)
    {
        // Being yanked off its feet spoils an attack.
        _pull = velocity;
        StopAttack();
        _knockback = Vector2.Zero;
    }

    public virtual void Respawn()
    {
        Position = _home;
        _pockets.Refill();
        _effects.Clear();
        Health.Refill();
        CurrentMood = Mood.Wandering;
        _target = null;
        _charmLeft = 0;
        StopAttack();
        AttackCooldown = 0;
        _knockback = Vector2.Zero;
        _pull = null;
        _wanderTimer = 0;
        _path.Clear();
        _stuckFor = 0;
        ResetPhysicsInterpolation();
        QueueRedraw();
    }

    public void WakeUpAfter(double seconds)
    {
        GetTree().CreateTimer(seconds).Timeout += WakeUp;
    }

    /// <summary>Get pushed (hits, bumping into things).</summary>
    protected void Knock(Vector2 velocity) => _knockback = velocity;

    /// <summary>Is <paramref name="target"/> close enough to hit, with <paramref name="extra"/> pixels to spare?</summary>
    protected bool InReach(IEnemyTarget target, float extra = 6f) =>
        target.HurtCenter.DistanceTo(HurtCenter) <= HurtRadius + target.HurtRadius + extra;

    /// <summary>Things this one's attacks can hit at <paramref name="point"/>: its target's side (heroes, summons, decoys;
    /// for a charmed one, other enemies).</summary>
    protected System.Collections.Generic.IEnumerable<IEnemyTarget> VictimsNear(Vector2 point, float radius)
    {
        var pool = IsCharmed
            ? GetTree().GetNodesInGroup(Enemies.Group).OfType<IEnemyTarget>().Where(e => !ReferenceEquals(e, this))
            : Heroes().Cast<IEnemyTarget>().Concat(Enemies.Summons(GetTree())).Concat(Enemies.Lures(GetTree()));
        return pool.Where(t => t.IsTargetable && t.HurtCenter.DistanceTo(point) <= t.HurtRadius + radius);
    }

    private Vector2 Wander(float dt, bool lookForHeroes)
    {
        if (lookForHeroes && FindTarget() is { } seen)
        {
            StartChasing(seen);
            return Vector2.Zero;
        }

        _wanderTimer -= dt;
        if (_wanderTimer <= 0)
        {
            // Half the time stand still, half the time stroll somewhere.
            _wanderDirection = Rng.Randf() < 0.5f ? Vector2.Zero : Vector2.Right.Rotated(Rng.RandfRange(0, Mathf.Tau));
            _wanderTimer = Rng.RandfRange(1f, 3f);
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
            // Charmed: go after another enemy, or tag along with the hero who charmed it.
            _target = FindTarget();
            if (_target is null)
            {
                StopAttack();
                var buddy = _charmedBy is not null && IsInstanceValid(_charmedBy) ? _charmedBy.GlobalPosition : GlobalPosition;
                return buddy.DistanceTo(GlobalPosition) > 30 ? (buddy - GlobalPosition).Normalized() * ChaseSpeed : Vector2.Zero;
            }
        }

        if (_target is null || !_target.IsTargetable || _target.GlobalPosition.DistanceTo(GlobalPosition) > LoseSightRange)
        {
            if (_target is Hero { IsDown: false, IsVisibleToEnemies: false })
            {
                // It vanished (camouflage)!
                FloatingText.Spawn(this, TextAt, "?", new Color(1f, 0.85f, 0.3f), 0.8f, 6f);
            }

            _target = null;
            CurrentMood = IsCharmed ? Mood.Chasing : Mood.Wandering;
            StopAttack();
            return Vector2.Zero;
        }

        return Fight(dt, _target);
    }

    private Vector2 Sleep(float dt)
    {
        _zzzTimer -= dt;
        if (_zzzTimer <= 0)
        {
            _zzzTimer = 1.2f;
            FloatingText.Spawn(this, GlobalPosition + new Vector2(8, -BodyHeight - 4), "z", new Color(0.6f, 0.75f, 1f), 1.2f, 10f);
        }

        return Vector2.Zero;
    }

    private void StartChasing(IEnemyTarget target)
    {
        _target = target;
        CurrentMood = Mood.Chasing;
        FloatingText.Spawn(this, TextAt, "!", new Color(1f, 0.85f, 0.3f), 0.6f, 6f);
    }

    /// <summary>Damage over time (poison, fire): no knockback, doesn't make it notice anyone.</summary>
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
        DropLoot();
        _effects.Clear();
        _charmLeft = 0;
        StopAttack();
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
        var lines = ThankLines;
        var line = lines[Rng.RandiRange(0, lines.Length - 1)];
        FloatingText.Spawn(this, TextAt, line, Colors.White, 2.5f, 6f);
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

        _sprite.Modulate = (CurrentMood == Mood.Asleep ? new Color(0.7f, 0.75f, 1f)
            : _hurtFlash > 0 ? new Color(1f, 0.45f, 0.45f)
            : WindUp >= 0 ? new Color(1f, 1f, 0.55f)
            : IsCharmed ? new Color(1f, 0.7f, 0.9f)
            : _effects.Tint ?? BaseTint) with { A = IsHostile ? Alpha : 1f };
        _sprite.Scale = SpriteScale * (CurrentMood == Mood.Asleep ? new Vector2(1f, 0.85f) : Vector2.One);
    }

    /// <summary>What to go after: charmed, the nearest other enemy; otherwise a decoy in sight, then the nearest hero
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

        return FindLure() ?? Heroes()
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

    protected Hero[] Heroes() =>
        GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(l => l.IsInGame && !l.IsDown).ToArray();
}
