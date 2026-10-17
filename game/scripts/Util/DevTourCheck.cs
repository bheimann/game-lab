using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the overworld's areas: <c>-- --autoplay-tour=&lt;folder&gt;</c>. P1 joins (as the lizard) in the village and
/// walks out through every path between the areas, there and back (village, meadow, forest, tundra, swamp), checking each
/// one lands in the right area, and finally goes in through the dungeon gate. In the village it also stands on the painter's mat and checks it gets painted,
/// goes into every shop (resting at the inn, buying a herb, getting painted in the paint shop) and through the castle (the armory door should stay barred).
/// Logs each trip and saves a screenshot on arrival.
/// </summary>
public partial class DevTourCheck : Node
{
    public const string Flag = "--autoplay-tour=";

    /// <summary>Each trip: the area to start in, the path to walk out of, the area it should lead to.</summary>
    private static readonly (string From, string Door, string To)[] Trips =
    {
        ("Village", "Painter", "Village"),
        ("Village", "InnDoor", "Inn"),
        ("Inn", "Counter", "Inn"),
        ("Inn", "DoorBottom", "Village"),
        ("Village", "HerbShopDoor", "HerbShop"),
        ("HerbShop", "Counter", "HerbShop"),
        ("HerbShop", "DoorBottom", "Village"),
        ("Village", "PaintShopDoor", "PaintShop"),
        ("PaintShop", "Painter", "PaintShop"),
        ("PaintShop", "DoorBottom", "Village"),
        ("Village", "GearShopDoor", "GearShop"),
        ("GearShop", "DoorBottom", "Village"),
        ("Village", "CastleDoor", "CastleHall"),
        ("CastleHall", "DoorTop", "ThroneRoom"),
        ("ThroneRoom", "DoorBottom", "CastleHall"),
        ("CastleHall", "DoorLeft", "CastleHall"),
        ("CastleHall", "DoorRight", "Kitchen"),
        ("Kitchen", "DoorLeft", "CastleHall"),
        ("CastleHall", "DoorBottom", "Village"),
        ("Village", "EastPath", "Overworld"),
        ("Overworld", "WestPath", "Village"),
        ("Village", "EastPath", "Overworld"),
        ("Overworld", "EastPath", "Forest"),
        ("Forest", "NorthPath", "Tundra"),
        ("Tundra", "SouthPath", "Forest"),
        ("Forest", "WestPath", "Overworld"),
        ("Overworld", "SouthPath", "Swamp"),
        ("Swamp", "NorthPath", "Overworld"),
        ("Overworld", "BurrowStairs", "Entrance"),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private double _tripStart = -1;
    private int _trip;
    private int _passed;
    private int _shotNumber;
    private string _classBefore = "";
    private string _shopBefore = "";

    public static DevTourCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var wanted = new HashSet<Key>();
        var hero = PlayerManager.Find(this)!.Players[0];
        var room = Dungeon.Find(this)!.CurrentRoom;

        if (hero is null)
        {
            // Join with J and pick the first character.
            if (_time is > 0.2 and < 0.3 || _time is > 0.8 and < 0.9)
            {
                wanted.Add(Key.J);
            }
        }
        else if (_trip < Trips.Length)
        {
            var (from, doorName, to) = Trips[_trip];
            if (_tripStart < 0)
            {
                _tripStart = _time;
            }

            var since = _time - _tripStart;
            if (doorName == "Painter")
            {
                Paint(hero, room, since);
                return;
            }

            if (doorName == "Counter")
            {
                Shop(hero, room, since);
                return;
            }

            if (from == to && room.Name == from && room.GetNodeOrNull<Door>(doorName) is { State: DoorState.Barred } barred)
            {
                // A barred door: walk into it, it shouldn't open.
                if (since < 0.05)
                {
                    hero.Teleport(barred.EntryPoint);
                }
                else if (since < 1.2)
                {
                    Push(barred.Outward, wanted);
                }
                else
                {
                    var stayed = room.Name == to && barred.State == DoorState.Barred;
                    _passed += stayed ? 1 : 0;
                    Log($"{doorName} is barred: {(stayed ? "stayed shut" : "OPENED")}");
                    Shot(0, $"_{room.Name}_barred");
                    Next();
                }

                foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.J })
                {
                    SetKey(key, wanted.Contains(key));
                }

                return;
            }

            var door = room.Name == from ? room.GetNodeOrNull<Door>(doorName) : null;
            if (room.Name == to && since > 0.5)
            {
                _passed++;
                Log($"{from} --{doorName}--> {room.Name} ({room.Title}), arrived at {hero.GlobalPosition - room.GlobalPosition:0}");
                Shot(0.6, $"_{room.Name}");
                Next();
            }
            else if (door is null && since > 0.5)
            {
                Log($"{from} --{doorName}--> expected {to} but in {room.Name}");
                Next();
            }
            else if (door is not null && since < 0.6)
            {
                // Stand just inside the path gap (held there until the camera catches up).
                hero.Teleport(door.EntryPoint);
                if (since < 0.02 && door.Stairs)
                {
                    Shot(0.55, $"_{door.Name}");
                }
            }
            else if (door is not null)
            {
                Push(door.Outward, wanted);
            }

            if (since > 5)
            {
                Log($"{from} --{doorName}--> timed out, still in {room.Name} at {hero.GlobalPosition - room.GlobalPosition:0}");
                Next();
            }
        }
        else
        {
            Log($"done: {_passed} of {Trips.Length} trips went to the right area");
            GetTree().CreateTimer(1.0).Timeout += () => GetTree().Quit();
            SetPhysicsProcess(false);
        }

        foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.J })
        {
            SetKey(key, wanted.Contains(key));
        }
    }

    /// <summary>Stand on the village painter's mat for a moment: the hero should get painted into its next class.</summary>
    private void Paint(Actors.Hero hero, DungeonRoom room, double since)
    {
        var painter = room.GetNode<Node2D>("Painter");
        if (since < 0.05)
        {
            _classBefore = hero.CurrentClass;
        }

        if (since < 1.5)
        {
            hero.Teleport(painter.GlobalPosition);
            return;
        }

        var painted = hero.CurrentClass != _classBefore;
        _passed += painted ? 1 : 0;
        Log($"village painter: {_classBefore} -> {hero.CurrentClass} ({(painted ? "painted" : "NOT painted")})");
        hero.Teleport(painter.GlobalPosition + new Vector2(0, 30));
        Next();
    }

    /// <summary>Stand at a shop's counter: buy what's on offer (the inn heals a hurt hero, the herb shop sells a herb).</summary>
    private void Shop(Actors.Hero hero, DungeonRoom room, double since)
    {
        var counter = room.GetNode<ShopCounter>("Counter");
        if (since < 0.05)
        {
            Items.Purse.Add(30 - Items.Purse.Coins); // 30 coins to shop with
            if (counter.Offer == ShopOffer.Rest)
            {
                hero.TakeHit(5, hero.GlobalPosition + Vector2.Up);
            }

            _shopBefore = $"coins {Items.Purse.Coins}, hp {hero.Health.Current}/{hero.Health.Max}, herbs {hero.Herbs}";
        }

        if (since < 1.6)
        {
            hero.Teleport(counter.GlobalPosition + new Vector2(0, 8));
            if (since is > 1.0 and < 1.02)
            {
                Shot(0, $"_{room.Name}_counter");
            }

            return;
        }

        var after = $"coins {Items.Purse.Coins}, hp {hero.Health.Current}/{hero.Health.Max}, herbs {hero.Herbs}";
        var bought = after != _shopBefore;
        _passed += bought ? 1 : 0;
        Log($"{room.Name} counter ({counter.Offer}, {counter.Price} coins): {_shopBefore} -> {after}");
        hero.Teleport(counter.GlobalPosition + new Vector2(0, 40));
        Next();
    }

    private void Next()
    {
        _trip++;
        _tripStart = -1;
    }

    private static void Push(Vector2 direction, HashSet<Key> wanted)
    {
        if (direction.X > 0.5f) wanted.Add(Key.D);
        if (direction.X < -0.5f) wanted.Add(Key.A);
        if (direction.Y > 0.5f) wanted.Add(Key.S);
        if (direction.Y < -0.5f) wanted.Add(Key.W);
    }

    private void Log(string text) => GD.Print($"[tour {_time:0.0}s] {text}");

    private void SetKey(Key key, bool down)
    {
        if (down == _held.Contains(key))
        {
            return;
        }

        if (down) _held.Add(key); else _held.Remove(key);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
    }

    private async void Shot(double after, string name)
    {
        var path = $"{_folder}/tour_{_shotNumber++:00}{name}.png";
        await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
