using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Effects;

/// <summary>What the beetle Vehicle Master can build.</summary>
public enum VehicleKind
{
    /// <summary>Slow and tough; A fires an explosive cannon shell.</summary>
    Tank,

    /// <summary>Fast; rams enemies it drives into; A is a burst of speed.</summary>
    Cart,

    /// <summary>Sails across water (fast), crawls on land (slow). Can't go into holes.</summary>
    Boat,

    /// <summary>Flies over water and holes for a while, then glides down and lands.</summary>
    Glider,

    /// <summary>Very fast and flies over water and holes as long as it likes; only one passenger; A fires a machine gun.</summary>
    Plane,

    /// <summary>A hovercraft snowmobile: very fast on ice, normal speed over water, slow on land. Can't go over holes.
    /// Unlocked by beating Frostfang Fortress.</summary>
    Hoversled,
}

/// <summary>What's under a vehicle.</summary>
public enum Surface
{
    Ground,
    Water,
    Ice,
    Hole,
}

/// <summary>
/// A vehicle the beetle Vehicle Master built (see <see cref="Abilities.VehicleAbility"/>). The beetle drives it (the
/// stick drives, A does the vehicle's trick, X hops out); teammates climb on by walking up and pressing A, keep using
/// their own attacks from up there, and hop off with dodge (not over water or a hole). Hits on anyone riding hit the
/// vehicle instead; when it breaks, everyone tumbles off. The node's position is the deck (where riders stand), so riders
/// draw on top of it.
/// </summary>
public partial class Vehicle : CharacterBody2D
{
    public const string Group = "vehicles";

    private const float GliderSeconds = 8f;

    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static readonly Color Tread = new(0.3f, 0.3f, 0.36f);
    private static readonly Color Body = new(0.42f, 0.33f, 0.62f);
    private static readonly Color BodyLight = new(0.6f, 0.5f, 0.8f);
    private static readonly Color Wood = new(0.66f, 0.42f, 0.26f);
    private static readonly Color DarkWood = new(0.45f, 0.27f, 0.2f);
    private static readonly Color Cloth = new(0.95f, 0.85f, 0.55f);
    private static readonly Color Red = new(0.85f, 0.27f, 0.25f);

    /// <summary>Where each seat is on the deck: the driver first, then passengers.</summary>
    private static readonly Vector2[] FourSeats = { new(0, 2), new(-13, 4), new(13, 4), new(0, 10) };
    private static readonly Vector2[] TwoSeats = { new(0, 0), new(0, 9) };

    private readonly Dictionary<IHittable, float> _rammed = new();
    private Hero?[] _riders = System.Array.Empty<Hero?>();
    private float _cooldown;
    private float _boost;
    private float _hurtFlash;
    private float _time;
    private float _flightLeft = GliderSeconds;
    private Vector2 _aim = Vector2.Right;
    private Vector2 _lastGround;

    [Export] public VehicleKind Kind { get; set; } = VehicleKind.Tank;

    public Health Health { get; private set; } = null!;

    /// <summary>The beetle that built it.</summary>
    public Hero Builder { get; set; } = null!;

    public Hero? Driver => _riders.Length > 0 ? _riders[0] : null;

    public IEnumerable<Hero> Riders => _riders.OfType<Hero>();

    public bool HasFreeSeat => _riders.Skip(1).Any(r => r is null);

    public bool Flies => Kind is VehicleKind.Glider or VehicleKind.Plane;

    /// <summary>How high a flying vehicle is off the ground, in pixels.</summary>
    public float Lift => Flies ? (Kind == VehicleKind.Glider ? 10f + 6f * Mathf.Clamp(_flightLeft / 2f, 0, 1) : 16f) : 0f;

    /// <summary>Raised when it breaks (the Vehicle Master then waits before building another).</summary>
    public event System.Action? Broken;

    private Vector2[] SeatOffsets => Kind == VehicleKind.Plane ? TwoSeats : FourSeats;

    public static string Describe(VehicleKind kind) => kind switch
    {
        VehicleKind.Tank => "Slow and tough. A fires the cannon",
        VehicleKind.Cart => "Fast, rams enemies. A: speed boost",
        VehicleKind.Boat => "Sails across water",
        VehicleKind.Glider => "Flies over water and holes for a while",
        VehicleKind.Plane => "Fast, flies anywhere, one passenger. A: machine gun",
        _ => "Zooms over ice, crosses water, slow on land",
    };

    public override void _Ready()
    {
        AddToGroup(Group);
        Health = new Health(Kind switch
        {
            VehicleKind.Tank => 30,
            VehicleKind.Cart => 18,
            VehicleKind.Boat => 20,
            VehicleKind.Glider => 12,
            VehicleKind.Hoversled => 18,
            _ => 16,
        });
        _riders = new Hero?[SeatOffsets.Length];
        _lastGround = GlobalPosition;

        // Walls stop everything; holes and water stop wheels (the boat and hoversled check for themselves, flyers go over).
        CollisionLayer = 0;
        CollisionMask = Flies || Kind is VehicleKind.Boat or VehicleKind.Hoversled ? 1u : 1u | Pit.PitLayer;
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

    /// <summary>Hop off next to the vehicle. Not over water or a hole (returns false), unless <paramref name="anywhere"/>:
    /// then riders who'd land in one are put back where the vehicle last was on the ground.</summary>
    public bool Leave(Hero hero, bool anywhere = false)
    {
        var seat = System.Array.IndexOf(_riders, hero);
        if (seat < 0)
        {
            return false;
        }

        var spot = GlobalPosition + new Vector2(seat % 2 == 1 ? -26 : 26, 22);
        if (hero.OverPit(spot) || OverHole(GlobalPosition + new Vector2(0, 12), waterToo: true))
        {
            if (!anywhere)
            {
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), "Can't hop off here!", new Color(0.8f, 0.8f, 0.8f), 1f, 7f);
                return false;
            }

            spot = _lastGround + new Vector2(seat * 10 - 15, 22);
        }

        _riders[seat] = null;
        hero.StopRiding(spot);
        return true;
    }

    public Vector2 SeatFor(Hero hero)
    {
        var seat = System.Array.IndexOf(_riders, hero);
        return GlobalPosition + SeatOffsets[Mathf.Max(0, seat)] + new Vector2(0, -Lift);
    }

    /// <summary>A rider got hit: the vehicle takes it.</summary>
    public void TakeHit(int damage)
    {
        Health.Damage(damage);
        _hurtFlash = 0.15f;
        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -6 - Lift), $"-{damage}", new Color(1f, 0.6f, 0.4f), 0.8f, 8f);
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
            Leave(rider, anywhere: true);
        }

        if (!quietly)
        {
            FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -10), $"The {Kind.ToString().ToLowerInvariant()} broke!", new Color(1f, 0.6f, 0.4f), 1.6f, 9f);
            CoopCamera.ShakeAll(this, 4f);
            Broken?.Invoke();
        }

        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        _cooldown -= dt;
        _boost -= dt;
        _hurtFlash -= dt;
        for (var i = 0; i < _riders.Length; i++)
        {
            if (_riders[i] is { IsInGame: false })
            {
                _riders[i] = null;
            }
        }

        var driver = Driver;
        var move = driver?.Controls.Move ?? Vector2.Zero;
        if (move.Length() > 0.2f)
        {
            _aim = Vector2.Right.Rotated(Mathf.Snapped(move.Angle(), Mathf.Pi / 4));
        }

        var before = GlobalPosition;
        Velocity = move * Speed();
        MoveAndSlide();

        // A boat sails on water but not into holes (or onto thin ice); a hoversled glides over water and ice, not holes.
        var under = SurfaceAt(GlobalPosition + new Vector2(0, 12));
        if ((Kind == VehicleKind.Boat && under is Surface.Hole or Surface.Ice) || (Kind == VehicleKind.Hoversled && under == Surface.Hole))
        {
            GlobalPosition = before;
        }

        if (CoopCamera.Find(this) is { } camera)
        {
            var allowed = camera.AllowedRect;
            GlobalPosition = GlobalPosition.Clamp(allowed.Position, allowed.End);
        }

        if (!OverHole(GlobalPosition + new Vector2(0, 12), waterToo: true))
        {
            _lastGround = GlobalPosition;
        }

        if (driver is not null && driver.Controls.PrimaryPressed && !driver.IsMenuOpen)
        {
            Trick();
        }

        if (Kind == VehicleKind.Cart)
        {
            Ram(dt);
        }

        if (Kind == VehicleKind.Glider)
        {
            Glide(dt);
        }

        QueueRedraw();
    }

    /// <summary>The driver's A button: the tank's cannon, the cart's boost, the plane's machine gun.</summary>
    public void Trick()
    {
        if (_cooldown > 0 || Driver is not { } driver)
        {
            return;
        }

        switch (Kind)
        {
            case VehicleKind.Tank:
                _cooldown = 1.1f;
                Bullet.Fire(driver, GlobalPosition + new Vector2(0, -2) + _aim * 20f, _aim,
                    new Bullet.Shot(230f, 170f, 6, DamageType.Physical, new Color(1f, 0.6f, 0.2f), ShotLook.Rocket, 22f, 8));
                CoopCamera.ShakeAll(this, 1.5f);
                break;

            case VehicleKind.Cart:
                _cooldown = 2.5f;
                _boost = 0.6f;
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -10), "Zoom!", Colors.White, 0.6f, 7f);
                break;

            case VehicleKind.Plane:
                _cooldown = 0.18f;
                Bullet.Fire(driver, GlobalPosition + new Vector2(0, 4 - Lift) + _aim * 18f, _aim,
                    new Bullet.Shot(330f, 160f, 2, DamageType.Physical, new Color(1f, 0.9f, 0.4f), ShotLook.Pellet));
                break;
        }
    }

    private float Speed() => Kind switch
    {
        VehicleKind.Tank => 55f,
        VehicleKind.Cart => 105f * (_boost > 0 ? 1.8f : 1f),
        VehicleKind.Boat => OverHole(GlobalPosition + new Vector2(0, 12), waterToo: true) ? 85f : 35f,
        VehicleKind.Glider => 70f,
        VehicleKind.Hoversled => SurfaceAt(GlobalPosition + new Vector2(0, 12)) switch
        {
            Surface.Ice => 160f,
            Surface.Water => 85f,
            _ => 35f,
        },
        _ => 115f,
    };

    /// <summary>The cart hits every enemy it drives into (each one at most every 0.7 seconds) while it's moving.</summary>
    private void Ram(float dt)
    {
        foreach (var key in _rammed.Keys.ToList())
        {
            _rammed[key] -= dt;
        }

        if (Driver is not { } driver || Velocity.Length() < 40f)
        {
            return;
        }

        foreach (var enemy in driver.EnemyTargets().Where(e => Hits.Touches(e, GlobalPosition + new Vector2(0, 6), 16f)))
        {
            if (_rammed.GetValueOrDefault(enemy) <= 0)
            {
                _rammed[enemy] = 0.7f;
                driver.Hit(enemy, _boost > 0 ? 7 : 4, GlobalPosition - Velocity.Normalized() * 10f, DamageType.Physical);
            }
        }
    }

    /// <summary>The glider only stays up for a while; then it lands (back on solid ground if it ran out over a hole).</summary>
    private void Glide(float dt)
    {
        _flightLeft -= dt;
        if (_flightLeft > 0)
        {
            return;
        }

        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -12), "Landed!", Colors.White, 1f, 8f);
        foreach (var rider in Riders.ToList())
        {
            Leave(rider, anywhere: true);
        }

        QueueFree();
    }

    /// <summary>What's at <paramref name="point"/>: plain ground, water, ice (a frozen lake, or an icy floor), or a hole.</summary>
    private Surface SurfaceAt(Vector2 point)
    {
        var query = new PhysicsPointQueryParameters2D { Position = point, CollisionMask = Pit.PitLayer };
        var pits = GetWorld2D().DirectSpaceState.IntersectPoint(query, 4)
            .Select(hit => (hit["collider"].AsGodotObject() as Node)?.GetParent() as Pit).OfType<Pit>().ToList();
        if (pits.Any(p => p.Ice))
        {
            return Surface.Ice;
        }

        if (pits.Any(p => p.Water))
        {
            return Surface.Water;
        }

        if (pits.Count > 0)
        {
            return Surface.Hole;
        }

        return IceFloor.At(GetTree(), point) is not null ? Surface.Ice : Surface.Ground;
    }

    /// <summary>Is <paramref name="point"/> over a hole (or thin ice), or with <paramref name="waterToo"/> over water too?</summary>
    private bool OverHole(Vector2 point, bool waterToo)
    {
        var query = new PhysicsPointQueryParameters2D { Position = point, CollisionMask = Pit.PitLayer };
        return GetWorld2D().DirectSpaceState.IntersectPoint(query, 4)
            .Select(hit => (hit["collider"].AsGodotObject() as Node)?.GetParent() as Pit)
            .Any(pit => pit is not null && (waterToo || !pit.Water || pit.Ice));
    }

    public override void _Draw()
    {
        var hurt = _hurtFlash > 0;

        // Flyers cast a shadow on the ground and are drawn up in the air.
        if (Flies)
        {
            DrawSetTransform(new Vector2(0, 14), 0, new Vector2(1f, 0.35f));
            DrawCircle(Vector2.Zero, 18f, new Color(0, 0, 0, 0.25f));
            DrawSetTransform(new Vector2(0, -Lift), 0, Vector2.One);
        }

        switch (Kind)
        {
            case VehicleKind.Tank:
                DrawTank(hurt);
                break;
            case VehicleKind.Cart:
                DrawCart(hurt);
                break;
            case VehicleKind.Boat:
                DrawBoat(hurt);
                break;
            case VehicleKind.Glider:
                DrawGlider(hurt);
                break;
            case VehicleKind.Plane:
                DrawPlane(hurt);
                break;
            case VehicleKind.Hoversled:
                DrawHoversled(hurt);
                break;
        }

        // Health bar (and the glider's flight time under it).
        DrawSetTransform(new Vector2(0, -Lift), 0, Vector2.One);
        var bar = new Rect2(-16, -10, 32, 3);
        DrawRect(bar.Grow(1), Outline);
        DrawRect(new Rect2(bar.Position, bar.Size with { X = bar.Size.X * Health.Current / Health.Max }), new Color(0.39f, 0.67f, 0.25f));
        if (Kind == VehicleKind.Glider)
        {
            var time = new Rect2(-16, -6, 32, 2);
            DrawRect(time.Grow(1), Outline);
            DrawRect(new Rect2(time.Position, time.Size with { X = time.Size.X * Mathf.Clamp(_flightLeft / GliderSeconds, 0, 1) }), new Color(0.55f, 0.85f, 1f));
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void Box(Rect2 rect, Color colour)
    {
        DrawRect(rect.Grow(1), Outline);
        DrawRect(rect, colour);
    }

    private void DrawTank(bool hurt)
    {
        // Treads (the links scroll while driving), the hull, the turret and its barrel.
        foreach (var y in new[] { 4f, 18f })
        {
            Box(new Rect2(-19, y, 38, 6), Tread);
            for (var x = -18f + Mathf.PosMod(_time * (Velocity.Length() > 1 ? 30f : 0f), 5f); x < 18f; x += 5f)
            {
                DrawLine(new Vector2(x, y), new Vector2(x, y + 6), Outline, 1f);
            }
        }

        Box(new Rect2(-16, -2, 32, 21), hurt ? Colors.White : Body);
        DrawRect(new Rect2(-16, -2, 32, 4), hurt ? Colors.White : BodyLight);
        var turret = new Vector2(0, 6);
        DrawLine(turret, turret + _aim * 22f, Outline, 5f);
        DrawLine(turret, turret + _aim * 21f, Tread, 3f);
        DrawCircle(turret, 7f, Outline);
        DrawCircle(turret, 6f, BodyLight);
    }

    private void DrawCart(bool hurt)
    {
        // A wooden cart on four wheels that turn as it rolls.
        foreach (var at in new[] { new Vector2(-14, 18), new Vector2(14, 18), new Vector2(-14, 6), new Vector2(14, 6) })
        {
            DrawCircle(at, 5f, Outline);
            DrawCircle(at, 4f, DarkWood);
            var spoke = Vector2.Right.Rotated(_time * Velocity.Length() * 0.15f) * 3f;
            DrawLine(at - spoke, at + spoke, Wood, 1f);
        }

        Box(new Rect2(-17, -2, 34, 18), hurt ? Colors.White : Wood);
        for (var y = 3f; y < 16f; y += 5f)
        {
            DrawLine(new Vector2(-17, y), new Vector2(17, y), DarkWood, 1f);
        }

        if (_boost > 0)
        {
            for (var i = 1; i <= 3; i++)
            {
                DrawLine(new Vector2(-20 - i * 4, 4 + i * 4) - _aim * 6, new Vector2(-28 - i * 4, 4 + i * 4) - _aim * 6, Colors.White with { A = 0.6f }, 1f);
            }
        }
    }

    private void DrawBoat(bool hurt)
    {
        // A wooden boat with a little sail; ripples around it on water.
        var onWater = OverHole(GlobalPosition + new Vector2(0, 12), waterToo: true);
        if (onWater)
        {
            var ripple = 2f * Mathf.Sin(_time * 4f);
            DrawArc(new Vector2(0, 14), 24f + ripple, 0.2f, Mathf.Pi - 0.2f, 16, Colors.White with { A = 0.5f }, 1f);
        }

        DrawColoredPolygon(new[] { new Vector2(-22, 0), new Vector2(22, 0), new Vector2(16, 18), new Vector2(-16, 18) }, Outline);
        DrawColoredPolygon(new[] { new Vector2(-20, 1), new Vector2(20, 1), new Vector2(15, 17), new Vector2(-15, 17) }, hurt ? Colors.White : Wood);
        DrawLine(new Vector2(-18, 6), new Vector2(18, 6), DarkWood, 1f);
        DrawLine(new Vector2(8, 2), new Vector2(8, -22), Outline, 2f);
        DrawColoredPolygon(new[] { new Vector2(9, -21), new Vector2(22, -6), new Vector2(9, -6) }, Cloth);
    }

    private void DrawGlider(bool hurt)
    {
        // A big cloth wing over a light frame the riders stand on.
        Box(new Rect2(-14, 0, 28, 14), hurt ? Colors.White : DarkWood);
        DrawColoredPolygon(new[] { new Vector2(-34, -6), new Vector2(0, -16), new Vector2(34, -6), new Vector2(0, -2) }, Outline);
        DrawColoredPolygon(new[] { new Vector2(-31, -7), new Vector2(0, -15), new Vector2(31, -7), new Vector2(0, -4) }, hurt ? Colors.White : Cloth);
        DrawLine(new Vector2(0, -15), new Vector2(0, -3), Red, 2f);
    }

    private void DrawHoversled(bool hurt)
    {
        // A hovercraft snowmobile: a black rubber skirt puffing air, a blue body with a windscreen, skis at the front
        // and a big fan at the back. It bobs a little as it hovers.
        var bob = Mathf.Sin(_time * 8f) * 1f;
        DrawSetTransform(new Vector2(0, 14), 0, new Vector2(1f, 0.35f));
        DrawCircle(Vector2.Zero, 20f, new Color(0.9f, 0.95f, 1f, 0.35f)); // spray
        DrawSetTransform(new Vector2(0, bob), 0, Vector2.One);
        Box(new Rect2(-20, 8, 40, 9), new Color(0.15f, 0.15f, 0.2f));
        Box(new Rect2(-17, -2, 34, 13), hurt ? Colors.White : new Color(0.25f, 0.55f, 0.9f));
        DrawRect(new Rect2(-17, -2, 34, 3), new Color(0.6f, 0.82f, 1f));
        Box(new Rect2(8, -7, 8, 6), new Color(0.75f, 0.9f, 1f, 0.8f)); // windscreen
        DrawLine(new Vector2(14, 18), new Vector2(24, 18), Outline, 2f); // ski
        DrawLine(new Vector2(22, 18), new Vector2(25, 15), Outline, 2f);
        var fan = new Vector2(-15, 2);
        DrawCircle(fan, 7f, Outline);
        DrawCircle(fan, 6f, new Color(0.3f, 0.32f, 0.38f));
        var blade = Vector2.Right.Rotated(_time * 30f) * 5f;
        DrawLine(fan - blade, fan + blade, new Color(0.85f, 0.88f, 0.95f), 2f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawPlane(bool hurt)
    {
        // A little red plane: wings, body, tail and a spinning propeller at the front. It turns to face the way it flies
        // (drawn nose-up, then turned around the middle of the deck).
        var turn = _aim.Angle() + Mathf.Pi / 2;
        DrawSetTransform(new Vector2(0, 6 - Lift) - new Vector2(0, 6).Rotated(turn), turn, Vector2.One);
        DrawColoredPolygon(new[] { new Vector2(-30, 2), new Vector2(30, 2), new Vector2(30, 8), new Vector2(-30, 8) }, Outline);
        DrawRect(new Rect2(-29, 3, 58, 4), hurt ? Colors.White : Red);
        Box(new Rect2(-7, -6, 14, 24), hurt ? Colors.White : Red);
        Box(new Rect2(-12, 16, 24, 4), Red);
        var nose = new Vector2(0, -8);
        var blade = Vector2.Right.Rotated(_time * 40f) * 8f;
        DrawLine(nose - blade, nose + blade, new Color(0.9f, 0.9f, 0.9f, 0.7f), 2f);
        DrawCircle(nose, 2f, Outline);
    }
}
