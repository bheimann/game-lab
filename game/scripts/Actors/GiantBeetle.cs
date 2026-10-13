using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Effects;
using LizardWizard.Players;
using LizardWizard.Util;

namespace LizardWizard.Actors;

/// <summary>
/// Boss: a giant beetle tricked by Mossimus Maximus. It plods after the nearest hero and pinches,
/// and every few seconds it flashes, then charges in a straight line. Its hard shell shrugs off hits (1 damage),
/// but if the charge ends in a wall it's dizzy for a moment and takes full damage: dodge the charge, then hit it hard.
/// </summary>
public partial class GiantBeetle : CharacterBody2D, IHittable, IEnemy, IBoss
{
    private const string Strip = "res://assets/sprites/beetle/giant_beetle.png";

    private readonly RandomNumberGenerator _rng = new();
    private AnimatedSprite2D _sprite = null!;
    private State _state = State.Waiting;
    private Hero? _target;
    private float _time;
    private float _untilCharge;
    private float _pinchCooldown;
    private float _hurtFlash;
    private Vector2 _chargeDirection;
    private Vector2 _facing = Vector2.Down;
    private bool _pinchHit;
    private float _zzz;

    private enum State
    {
        Waiting,
        Walking,
        Pinching,
        WindUp,
        Charging,
        Dizzy,
        Asleep,
        Thankful,
    }

    [Export] public string BossName { get; set; } = "Giant Beetle";

    /// <summary>Health with one player; each extra player adds <see cref="HealthPerExtraPlayer"/>.</summary>
    [Export] public int BaseHealth { get; set; } = 60;

    [Export] public int HealthPerExtraPlayer { get; set; } = 30;

    [Export] public float WalkSpeed { get; set; } = 26f;

    [Export] public float ChargeSpeed { get; set; } = 150f;

    [Export] public float WindUpSeconds { get; set; } = 0.8f;

    [Export] public float DizzySeconds { get; set; } = 2.2f;

    /// <summary>Seconds of walking between charges (picked at random in this range).</summary>
    [Export] public Vector2 SecondsBetweenCharges { get; set; } = new(3f, 4.5f);

    [Export] public int ChargeDamage { get; set; } = 4;

    [Export] public int PinchDamage { get; set; } = 3;

    public Health Health { get; private set; } = null!;

    public bool IsFighting => _state is State.Walking or State.Pinching or State.WindUp or State.Charging or State.Dizzy;

    public bool IsDizzy => _state == State.Dizzy;

    public bool IsHostile => _state is not (State.Asleep or State.Thankful);

    public bool IsAsleep => _state == State.Asleep;

    public bool CanBeHit => IsFighting;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -12);

    public float HurtRadius => 14f;

    public bool IsTargeting(Hero lizard) => IsFighting && _target == lizard;

    public override void _Ready()
    {
        AddToGroup(Enemies.Group);
        _rng.Seed = (ulong)GetInstanceId() ^ Time.GetTicksUsec();
        Health = new Health(BaseHealth);
        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = 4;
        CollisionMask = 1 | Pit.PitLayer;

        _sprite = new AnimatedSprite2D { SpriteFrames = SpriteSheet.Frames("walk", Strip, 26, 4), Position = new Vector2(0, -12) };
        AddChild(_sprite);
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 11f }, Position = new Vector2(0, -10) });
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        _pinchCooldown -= dt;
        _hurtFlash -= dt;

        Velocity = _state switch
        {
            State.Waiting => Wait(),
            State.Walking => Walk(dt),
            State.Pinching => Pinch(),
            State.WindUp => WindUp(),
            State.Charging => Charge(),
            State.Dizzy => Dizzy(),
            State.Asleep => Sleep(dt),
            _ => Vector2.Zero,
        };

        MoveAndSlide();

        if (_state == State.Charging && GetSlideCollisionCount() > 0 && _time > 0.1f)
        {
            // Smashed into a wall: dizzy and open to attack.
            Enter(State.Dizzy);
            CoopCamera.Find(this)?.Shake(5f);
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -18), "Dizzy!", new Color(1f, 0.9f, 0.4f), 1.2f, 8f);
        }

        UpdateLook();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_state == State.Dizzy)
        {
            // Stars circling its head.
            for (var i = 0; i < 3; i++)
            {
                var at = new Vector2(0, -30) + Vector2.Right.Rotated(_time * 5f + i * Mathf.Tau / 3) * new Vector2(10, 4);
                DrawCircle(at, 1.5f, new Color(1f, 0.95f, 0.5f));
            }
        }
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (!CanBeHit)
        {
            return;
        }

        // The shell shrugs off hits, unless it's dizzy and wide open.
        var dealt = Health.Damage(IsDizzy ? damage : 1);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -16), IsDizzy ? $"-{dealt}!" : "clank",
            IsDizzy ? new Color(1f, 0.9f, 0.4f) : new Color(0.75f, 0.78f, 0.85f), 0.6f, 8f);
        _hurtFlash = 0.15f;

        if (Health.IsEmpty)
        {
            Enter(State.Asleep);
            _target = null;
            Enemies.CheckCleared(this);
        }
    }

    public void WakeUpAfter(double seconds)
    {
        GetTree().CreateTimer(seconds).Timeout += () =>
        {
            Enter(State.Thankful);
            FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -40), "Mossimus Maximus tricked me! Sorry, lizards!", Colors.White, 3.5f, 6f);
        };
    }

    private Vector2 Wait()
    {
        // Wakes up when a hero comes into its room.
        var seen = Heroes().OrderBy(h => h.GlobalPosition.DistanceTo(GlobalPosition)).FirstOrDefault();
        if (seen is null)
        {
            return Vector2.Zero;
        }

        // Tougher with more players.
        var players = PlayerManager.Find(this)?.Players.Count(p => p is not null) ?? 1;
        Health = new Health(BaseHealth + HealthPerExtraPlayer * Mathf.Max(0, players - 1));
        _target = seen;
        Enter(State.Walking);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -20), "CLACK CLACK!", new Color(1f, 0.6f, 0.4f), 1.5f, 8f);
        return Vector2.Zero;
    }

    private Vector2 Walk(float dt)
    {
        // Follow the closest hero it can see (camouflage and smoke work on bosses too).
        _target = Heroes().Where(h => h.IsVisibleToEnemies).OrderBy(h => h.GlobalPosition.DistanceTo(GlobalPosition)).FirstOrDefault();
        if (_target is null)
        {
            return Vector2.Zero;
        }

        var toTarget = _target.GlobalPosition - GlobalPosition;
        _untilCharge -= dt;
        if (_untilCharge <= 0)
        {
            _chargeDirection = toTarget.Normalized();
            Enter(State.WindUp);
            return Vector2.Zero;
        }

        if (toTarget.Length() < 26f && _pinchCooldown <= 0)
        {
            Enter(State.Pinching);
            return Vector2.Zero;
        }

        return toTarget.Normalized() * WalkSpeed;
    }

    private Vector2 Pinch()
    {
        // Short wind-up (flashing), then a snap of the jaws.
        if (_time >= 0.45f && !_pinchHit)
        {
            _pinchHit = true;
            foreach (var hero in Heroes().Where(h => h.HurtCenter.DistanceTo(HurtCenter) < HurtRadius + h.HurtRadius + 8f))
            {
                hero.TakeHit(PinchDamage, GlobalPosition);
            }
        }

        if (_time >= 0.7f)
        {
            _pinchCooldown = 1.2f;
            Enter(State.Walking);
        }

        return Vector2.Zero;
    }

    private Vector2 WindUp()
    {
        if (_time >= WindUpSeconds)
        {
            Enter(State.Charging);
        }

        return Vector2.Zero;
    }

    private Vector2 Charge()
    {
        foreach (var hero in Heroes().Where(h => h.CanBeHit && h.HurtCenter.DistanceTo(HurtCenter) < HurtRadius + h.HurtRadius))
        {
            hero.TakeHit(ChargeDamage, GlobalPosition - _chargeDirection * 10f);
        }

        if (_time > 1.6f)
        {
            Enter(State.Walking); // ran out of steam without hitting a wall
        }

        return _chargeDirection * ChargeSpeed;
    }

    private Vector2 Dizzy()
    {
        if (_time >= DizzySeconds)
        {
            Enter(State.Walking);
        }

        return Vector2.Zero;
    }

    private Vector2 Sleep(float dt)
    {
        _zzz -= dt;
        if (_zzz <= 0)
        {
            _zzz = 1.2f;
            FloatingText.Spawn(this, GlobalPosition + new Vector2(12, -34), "z", new Color(0.6f, 0.75f, 1f), 1.2f, 10f);
        }

        return Vector2.Zero;
    }

    private void Enter(State state)
    {
        _state = state;
        _time = 0;
        _pinchHit = false;
        if (state == State.Walking)
        {
            _untilCharge = _rng.RandfRange(SecondsBetweenCharges.X, SecondsBetweenCharges.Y);
        }
    }

    private void UpdateLook()
    {
        var moving = Velocity.Length() > 2f;
        if (moving)
        {
            _facing = Velocity;
        }
        else if (_state == State.WindUp)
        {
            _facing = _chargeDirection;
        }

        // The art faces up; turn it to the nearest of the 4 directions (exact 90° turns keep the pixels crisp).
        var quarter = Mathf.RoundToInt(_facing.Angle() / (Mathf.Pi / 2));
        _sprite.Rotation = quarter * Mathf.Pi / 2 + Mathf.Pi / 2;

        _sprite.SpeedScale = _state == State.Charging ? 4f : 1f;
        if (moving && !_sprite.IsPlaying())
        {
            _sprite.Play("walk");
        }
        else if (!moving && _sprite.IsPlaying())
        {
            _sprite.Pause();
        }

        var flashing = _state is State.WindUp or State.Pinching && (int)(_time * 12) % 2 == 0;
        _sprite.Modulate = _state == State.Asleep ? new Color(0.7f, 0.75f, 1f)
            : _hurtFlash > 0 ? new Color(1f, 0.45f, 0.45f)
            : flashing ? new Color(1f, 1f, 0.55f)
            : _state == State.Dizzy ? new Color(0.85f, 0.85f, 1f)
            : Colors.White;
    }

    private Hero[] Heroes() =>
        GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame && !h.IsDown).ToArray();
}
