using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Effects;

/// <summary>
/// The beetle Vehicle Master's tank (see <see cref="Abilities.VehicleAbility"/>). The beetle drives it (the stick
/// drives, A fires the cannon, X hops out); teammates climb on by walking up and pressing A, keep using their own
/// attacks from up there, and hop off with dodge. Hits on anyone riding hit the tank instead; when it breaks, everyone
/// tumbles off. The node's position is the deck (where riders stand), so riders draw on top of it.
/// </summary>
public partial class Tank : CharacterBody2D
{
    public const int Seats = 4;

    public const string Group = "tanks";

    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static readonly Color Tread = new(0.3f, 0.3f, 0.36f);
    private static readonly Color Body = new(0.42f, 0.33f, 0.62f);
    private static readonly Color BodyLight = new(0.6f, 0.5f, 0.8f);

    /// <summary>Where each seat is on the deck: the driver in the middle, passengers around.</summary>
    private static readonly Vector2[] SeatOffsets = { new(0, 2), new(-13, 4), new(13, 4), new(0, 10) };

    private readonly Hero?[] _riders = new Hero?[Seats];
    private float _cannonCooldown;
    private float _hurtFlash;
    private float _treadTime;
    private Vector2 _aim = Vector2.Right;

    [Export] public float Speed { get; set; } = 55f;

    [Export] public int MaxHealth { get; set; } = 30;

    [Export] public float CannonCooldownSeconds { get; set; } = 1.1f;

    public Health Health { get; private set; } = null!;

    /// <summary>The beetle that built it.</summary>
    public Hero Builder { get; set; } = null!;

    public Hero? Driver => _riders[0];

    public IEnumerable<Hero> Riders => _riders.OfType<Hero>();

    public bool HasFreeSeat => _riders.Skip(1).Any(r => r is null);

    /// <summary>Raised when it breaks (the Vehicle Master then waits before building another).</summary>
    public event System.Action? Broken;

    public override void _Ready()
    {
        AddToGroup(Group);
        Health = new Health(MaxHealth);
        CollisionLayer = 0;
        CollisionMask = 1 | Pit.PitLayer; // walls, rocks and water stop it
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(36, 10) }, Position = new Vector2(0, 12) });
    }

    /// <summary>Climb on: the builder takes the driver's seat, anyone else a free passenger seat.</summary>
    public bool Board(Hero hero)
    {
        if (Riders.Contains(hero))
        {
            return false;
        }

        var seat = hero == Builder ? 0 : System.Array.FindIndex(_riders, 1, r => r is null);
        if (seat < 0 || _riders[seat] is not null)
        {
            return false;
        }

        _riders[seat] = hero;
        hero.Ride(this);
        return true;
    }

    /// <summary>Hop off (next to the tank).</summary>
    public void Leave(Hero hero)
    {
        var seat = System.Array.IndexOf(_riders, hero);
        if (seat < 0)
        {
            return;
        }

        _riders[seat] = null;
        hero.StopRiding(GlobalPosition + new Vector2(seat == 1 ? -26 : 26, 22));
    }

    public Vector2 SeatFor(Hero hero)
    {
        var seat = System.Array.IndexOf(_riders, hero);
        return GlobalPosition + SeatOffsets[Mathf.Max(0, seat)];
    }

    /// <summary>A rider got hit: the tank takes it.</summary>
    public void TakeHit(int damage)
    {
        Health.Damage(damage);
        _hurtFlash = 0.15f;
        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -6), $"-{damage}", new Color(1f, 0.6f, 0.4f), 0.8f, 8f);
        if (Health.IsEmpty)
        {
            Break();
        }
    }

    /// <summary>Everyone off, and it's gone (broken, packed away, or left behind in another room).</summary>
    public void Break(bool quietly = false)
    {
        foreach (var rider in Riders.ToList())
        {
            Leave(rider);
        }

        if (!quietly)
        {
            FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -10), "The tank broke!", new Color(1f, 0.6f, 0.4f), 1.6f, 9f);
            CoopCamera.Find(this)?.Shake(4f);
            Broken?.Invoke();
        }

        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _cannonCooldown -= dt;
        _hurtFlash -= dt;
        foreach (var gone in Riders.Where(r => !r.IsInGame).ToList())
        {
            _riders[System.Array.IndexOf(_riders, gone)] = null;
        }

        var driver = Driver;
        var move = driver?.Controls.Move ?? Vector2.Zero;
        if (move.Length() > 0.2f)
        {
            _aim = Vector2.Right.Rotated(Mathf.Snapped(move.Angle(), Mathf.Pi / 4));
            _treadTime += dt;
        }

        Velocity = move * Speed;
        MoveAndSlide();
        if (CoopCamera.Find(this) is { } camera)
        {
            var allowed = camera.AllowedRect;
            GlobalPosition = GlobalPosition.Clamp(allowed.Position, allowed.End);
        }

        if (driver is not null && driver.Controls.PrimaryPressed && !driver.IsMenuOpen)
        {
            FireCannon();
        }

        QueueRedraw();
    }

    /// <summary>Boom: an explosive shell the way the tank is facing.</summary>
    public void FireCannon()
    {
        if (_cannonCooldown > 0 || Driver is not { } driver)
        {
            return;
        }

        _cannonCooldown = CannonCooldownSeconds;
        var muzzle = GlobalPosition + new Vector2(0, -2) + _aim * 20f;
        var shot = new Bullet.Shot(230f, 170f, 6, DamageType.Physical, new Color(1f, 0.6f, 0.2f), ShotLook.Rocket, 22f, 8);
        Bullet.Fire(driver, muzzle, _aim, shot);
        CoopCamera.Find(this)?.Shake(1.5f);
    }

    public override void _Draw()
    {
        var flash = _hurtFlash > 0 ? Colors.White : Body;

        // Treads (the links scroll while driving), the hull, the turret and its barrel.
        foreach (var y in new[] { 4f, 18f })
        {
            DrawRect(new Rect2(-20, y - 1, 40, 8), Outline);
            DrawRect(new Rect2(-19, y, 38, 6), Tread);
            for (var x = -18f + Mathf.PosMod(_treadTime * 30f, 5f); x < 18f; x += 5f)
            {
                DrawLine(new Vector2(x, y), new Vector2(x, y + 6), Outline, 1f);
            }
        }

        DrawRect(new Rect2(-17, -3, 34, 23), Outline);
        DrawRect(new Rect2(-16, -2, 32, 21), flash);
        DrawRect(new Rect2(-16, -2, 32, 4), _hurtFlash > 0 ? Colors.White : BodyLight);

        var turret = new Vector2(0, 6);
        DrawLine(turret, turret + _aim * 22f, Outline, 5f);
        DrawLine(turret, turret + _aim * 21f, Tread, 3f);
        DrawCircle(turret, 7f, Outline);
        DrawCircle(turret, 6f, BodyLight);

        // Health bar.
        var bar = new Rect2(-16, -10, 32, 3);
        DrawRect(bar.Grow(1), Outline);
        DrawRect(new Rect2(bar.Position, bar.Size with { X = bar.Size.X * Health.Current / Health.Max }), new Color(0.39f, 0.67f, 0.25f));
    }
}
