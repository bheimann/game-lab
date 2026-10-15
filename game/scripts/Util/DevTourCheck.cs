using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the overworld's areas: <c>-- --autoplay-tour=&lt;folder&gt;</c>. P1 joins (as the lizard) and walks
/// out through every path between the areas, there and back (meadow, village, forest, tundra, swamp), checking each
/// one lands in the right area. Logs each trip and saves a screenshot on arrival.
/// </summary>
public partial class DevTourCheck : Node
{
    public const string Flag = "--autoplay-tour=";

    /// <summary>Each trip: the area to start in, the path to walk out of, the area it should lead to.</summary>
    private static readonly (string From, string Door, string To)[] Trips =
    {
        ("Overworld", "WestPath", "Village"),
        ("Village", "EastPath", "Overworld"),
        ("Overworld", "EastPath", "Forest"),
        ("Forest", "NorthPath", "Tundra"),
        ("Tundra", "SouthPath", "Forest"),
        ("Forest", "WestPath", "Overworld"),
        ("Overworld", "SouthPath", "Swamp"),
        ("Swamp", "NorthPath", "Overworld"),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private double _tripStart = -1;
    private int _trip;
    private int _passed;
    private int _shotNumber;

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
