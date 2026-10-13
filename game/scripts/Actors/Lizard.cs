using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;
using LizardWizard.Players;
using LizardWizard.Util;

namespace LizardWizard.Actors;

/// <summary>
/// A player-controlled lizard: walks in 8 directions, punches, and flicks its tongue.
/// All the numbers are exported so they can be tuned in the Godot inspector.
/// </summary>
public partial class Lizard : CharacterBody2D, IHittable
{
    public const string Group = "players";

    private const string SpriteFolder = "res://assets/sprites/chameleon";
    private const double WalkFps = 8;
    private static readonly Vector2 SpriteOffset = new(0, -14); // feet at the node's origin

    // Where the tongue comes out, per view (side view is drawn facing right).
    private static readonly Vector2 MouthDown = new(0, -17);
    private static readonly Vector2 MouthUp = new(0, -19);
    private static readonly Vector2 MouthSide = new(6, -18);
    private static readonly Color TongueColor = new(1f, 0.32f, 0.47f);

    private const float PunchSeconds = 0.2f;
    private const float TongueOutSeconds = 0.1f;
    private const float TongueHoldSeconds = 0.05f;
    private const float TongueInSeconds = 0.15f;

    private readonly PlayerInput _input = new();
    private AnimatedSprite2D _sprite = null!;
    private Line2D _tongue = null!;
    private State _state = State.Normal;
    private float _stateTime;
    private float _cooldown;
    private float _invulnerable;
    private float _punchFlash;
    private bool _tongueHasHit;
    private Vector2 _knockback;
    private Vector2 _tongueDirection;
    private float _tongueLength;
    private Vector2 _punchPoint;
    private Vector2 _spawnPoint;

    private enum State
    {
        Normal,
        Punching,
        Tongue,
        Down,
    }

    /// <summary>Walking speed in pixels per second.</summary>
    [Export] public float Speed { get; set; } = 70f;

    [Export] public int MaxHealth { get; set; } = 20;

    [Export] public int PunchDamage { get; set; } = 5;

    /// <summary>Size of the punch's hit circle.</summary>
    [Export] public float PunchRadius { get; set; } = 8f;

    [Export] public int TongueDamage { get; set; } = 1;

    /// <summary>How far the tongue reaches, in pixels.</summary>
    [Export] public float TongueRange { get; set; } = 44f;

    /// <summary>Aim assist: the tongue snaps to an enemy within this many degrees of where you're facing.</summary>
    [Export] public float TongueAimAssistDegrees { get; set; } = 35f;

    /// <summary>How long the lizard can't be hurt again after being hit.</summary>
    [Export] public float InvulnerableSeconds { get; set; } = 1f;

    /// <summary>How long a knocked-out lizard waits before getting back up at the start point.</summary>
    [Export] public float RespawnSeconds { get; set; } = 2f;

    public Health Health { get; private set; } = null!;

    /// <summary>The direction the lizard last moved in (one of 8 directions).</summary>
    public Vector2 Facing { get; private set; } = Vector2.Down;

    public bool IsDown => _state == State.Down;

    /// <summary>Whether enemies can see this lizard. Camouflage (M3) will make this false while standing still.</summary>
    public bool IsVisibleToEnemies => !IsDown;

    /// <summary>"In a fight": an enemy can see this lizard and is targeting it.</summary>
    public bool IsInFight => GetTree().GetNodesInGroup(Enemies.Group).OfType<IEnemy>().Any(e => e.IsTargeting(this));

    public bool CanBeHit => !IsDown && _invulnerable <= 0;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -12);

    public float HurtRadius => 7f;

    public override void _Ready()
    {
        AddToGroup(Group);
        Health = new Health(MaxHealth);
        _spawnPoint = GlobalPosition;
        MotionMode = MotionModeEnum.Floating;

        _sprite = new AnimatedSprite2D { SpriteFrames = BuildAnimations(), Offset = SpriteOffset };
        AddChild(_sprite);
        _sprite.Play("idle_down");

        _tongue = new Line2D
        {
            Width = 2f,
            DefaultColor = TongueColor,
            EndCapMode = Line2D.LineCapMode.Round,
            Visible = false,
        };
        AddChild(_tongue);

        // Small box at the feet, so the lizard can walk right up to walls and overlap things above it.
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(10, 6) }, Position = new Vector2(0, -3) });
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _input.Update();
        _stateTime += dt;
        _cooldown -= dt;
        _invulnerable -= dt;
        _punchFlash -= dt;

        var moveFactor = 0f;
        switch (_state)
        {
            case State.Down:
                if (_stateTime >= RespawnSeconds)
                {
                    Respawn();
                }

                break;

            case State.Normal:
                moveFactor = 1f;
                if (_input.PunchPressed && _cooldown <= 0)
                {
                    StartPunch();
                }
                else if (_input.TonguePressed && _cooldown <= 0)
                {
                    StartTongue();
                }

                break;

            case State.Punching:
                moveFactor = 0.3f;
                if (_stateTime >= PunchSeconds)
                {
                    _state = State.Normal;
                }

                break;

            case State.Tongue:
                UpdateTongue();
                break;
        }

        var move = moveFactor > 0 ? _input.Move : Vector2.Zero;
        if (move.Length() > 0.2f && _state == State.Normal)
        {
            // Snap to 8 directions.
            Facing = Vector2.Right.Rotated(Mathf.Snapped(move.Angle(), Mathf.Pi / 4));
        }

        Velocity = move * Speed * moveFactor + _knockback;
        _knockback = _knockback.MoveToward(Vector2.Zero, 600f * dt);
        MoveAndSlide();

        UpdateLook(move);
    }

    public override void _Draw()
    {
        if (_punchFlash > 0)
        {
            var at = ToLocal(_punchPoint);
            DrawCircle(at, 5f, new Color(1, 1, 1, 0.8f));
            DrawArc(at, 7f, 0, Mathf.Tau, 16, new Color(1f, 0.93f, 0.5f), 1f);
        }
    }

    public void TakeHit(int damage, Vector2 from)
    {
        if (!CanBeHit)
        {
            return;
        }

        var dealt = Health.Damage(damage);
        FloatingText.Spawn(GetParent(), HurtCenter + new Vector2(0, -8), $"-{dealt}", new Color(1f, 0.35f, 0.35f));
        _knockback = (GlobalPosition - from).Normalized() * 140f;
        _invulnerable = InvulnerableSeconds;
        EndTongue();

        if (Health.IsEmpty)
        {
            GoDown();
        }
        else if (_state != State.Normal)
        {
            _state = State.Normal;
        }
    }

    private void StartPunch()
    {
        _state = State.Punching;
        _stateTime = 0;
        _cooldown = 0.35f;
        _punchFlash = 0.1f;
        _punchPoint = HurtCenter + Facing * 12f;

        foreach (var enemy in Targets().Where(t => Hits.Touches(t, _punchPoint, PunchRadius)))
        {
            enemy.TakeHit(PunchDamage, GlobalPosition);
        }

        // Little lunge so the punch has some weight.
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "offset", SpriteOffset + Facing * 3f, 0.05f);
        tween.TweenProperty(_sprite, "offset", SpriteOffset, 0.12f);
    }

    private void StartTongue()
    {
        _state = State.Tongue;
        _stateTime = 0;
        _cooldown = TongueOutSeconds + TongueHoldSeconds + TongueInSeconds + 0.1f;
        _tongueHasHit = false;
        _tongue.Visible = true;

        // Aim assist: snap to the closest enemy roughly in front of us.
        var mouth = ToGlobal(Mouth());
        var assisted = Targets()
            .Select(t => t.HurtCenter - mouth)
            .Where(d => d.Length() <= TongueRange + 8f && Mathf.RadToDeg(Mathf.Abs(Facing.AngleTo(d))) <= TongueAimAssistDegrees)
            .OrderBy(d => d.Length())
            .FirstOrDefault();
        _tongueDirection = assisted == Vector2.Zero ? Facing : assisted.Normalized();
        _tongueLength = assisted == Vector2.Zero ? TongueRange : Mathf.Min(TongueRange, assisted.Length());
        UpdateTongue();
    }

    private void UpdateTongue()
    {
        var t = _stateTime;
        float reach;
        if (t < TongueOutSeconds)
        {
            reach = t / TongueOutSeconds;
        }
        else if (t < TongueOutSeconds + TongueHoldSeconds)
        {
            reach = 1f;
        }
        else
        {
            reach = 1f - (t - TongueOutSeconds - TongueHoldSeconds) / TongueInSeconds;
        }

        if (reach <= 0 && t > TongueOutSeconds)
        {
            EndTongue();
            _state = State.Normal;
            return;
        }

        var mouth = Mouth();
        var tip = mouth + _tongueDirection * _tongueLength * Mathf.Clamp(reach, 0, 1);
        _tongue.Points = new[] { mouth, tip };

        if (!_tongueHasHit && reach >= 1f)
        {
            _tongueHasHit = true;
            var start = ToGlobal(mouth);
            var end = ToGlobal(tip);
            var target = Targets()
                .Where(e => Hits.Touches(e, start, end))
                .OrderBy(e => e.HurtCenter.DistanceTo(start))
                .FirstOrDefault();
            target?.TakeHit(TongueDamage, GlobalPosition);
        }
    }

    private void EndTongue()
    {
        _tongue.Visible = false;
    }

    private void GoDown()
    {
        _state = State.Down;
        _stateTime = 0;
        _sprite.RotationDegrees = 90;
        _sprite.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        FloatingText.Spawn(GetParent(), HurtCenter + new Vector2(0, -12), "Ouch!", Colors.White, 1.2f);
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPoint;
        Health.Refill();
        _state = State.Normal;
        _knockback = Vector2.Zero;
        _sprite.RotationDegrees = 0;
        _sprite.Modulate = Colors.White;
        _invulnerable = 1.5f;
    }

    private void UpdateLook(Vector2 move)
    {
        var view = View();
        var walking = move.Length() > 0.2f && _state is State.Normal or State.Punching;
        var animation = $"{(walking ? "walk" : "idle")}_{view}";
        if (!IsDown && _sprite.Animation != animation)
        {
            _sprite.Play(animation);
        }

        if (view == "side")
        {
            _sprite.FlipH = Facing.X < 0;
        }
        else
        {
            _sprite.FlipH = false;
        }

        // Facing away: the tongue starts behind the head.
        _tongue.ZIndex = view == "up" ? -1 : 0;

        // Flicker (faint / solid) while invulnerable, but never disappear completely.
        if (!IsDown)
        {
            var faint = _invulnerable > 0 && (int)(_invulnerable * 15) % 2 == 1;
            _sprite.Modulate = Colors.White with { A = faint ? 0.35f : 1f };
        }

        QueueRedraw();
    }

    /// <summary>Which drawing to use: "down", "up" or "side". Diagonals use the side view.</summary>
    private string View() =>
        Mathf.Abs(Facing.Y) > Mathf.Abs(Facing.X) + 0.01f ? (Facing.Y > 0 ? "down" : "up") : "side";

    private Vector2 Mouth() => View() switch
    {
        "down" => MouthDown,
        "up" => MouthUp,
        _ => MouthSide with { X = Facing.X < 0 ? -MouthSide.X : MouthSide.X },
    };

    private static SpriteFrames BuildAnimations()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var (view, width) in new[] { ("down", 16), ("up", 16), ("side", 20) })
        {
            // Each strip is: idle, step A, step B.
            var strip = GD.Load<Texture2D>($"{SpriteFolder}/chameleon_{view}.png");
            SpriteSheet.AddFrames(frames, $"idle_{view}", strip, width, 1, new[] { 0 });
            SpriteSheet.AddFrames(frames, $"walk_{view}", strip, width, WalkFps, new[] { 1, 0, 2, 0 });
        }

        return frames;
    }

    private IHittable[] Targets() =>
        GetTree().GetNodesInGroup(Enemies.Group).OfType<IHittable>().Where(t => t.CanBeHit).ToArray();
}
