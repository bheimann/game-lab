using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the maps: <c>-- --autoplay-map=&lt;folder&gt;</c>. P1 (lizard) and P2 (fox) join in the village; most of
/// the overworld and the first rooms of The Old Burrow are marked as visited, and P2 goes into the dungeon. P1 opens
/// the map from the Start menu (the overworld page, P2 at the dungeon's stairs), P2 opens it in the dungeon (its page)
/// and flips to the overworld. Logs the overworld's layout and PASS / FAIL for each check, and saves a screenshot of each map.
/// </summary>
public partial class DevMapCheck : Node
{
    public const string Flag = "--autoplay-map=";

    private readonly AutoplayBot[] _bots =
    {
        new(0, Key.W, Key.A, Key.S, Key.D, Key.J, Key.K, Key.L, Key.Backspace, Key.I, Key.Q, Key.E, Key.Tab, Key.Shift, isFox: false),
        new(1, Key.Up, Key.Left, Key.Down, Key.Right, Key.Period, Key.Slash, Key.Comma, Key.Delete, Key.M, Key.Semicolon, Key.Apostrophe, Key.Enter, Key.Ctrl, isFox: true),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private int _shot;
    private int _passed;
    private int _failed;

    public static DevMapCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _Ready() => Run();

    private async void Run()
    {
        await Seconds(0.3);
        var manager = PlayerManager.Find(this)!;
        var dungeon = Dungeon.Find(this)!;
        var p1 = manager.JoinForDev(InputDevice.KeyboardLeft, 0, "")!;
        var p2 = manager.JoinForDev(InputDevice.KeyboardRight, 1, "")!;
        await Seconds(0.5);

        // The overworld's layout (each area's cell), and any two areas on the same cell.
        var overworld = WorldMap.Overworld(dungeon.HomeVillage);
        foreach (var (room, cell) in overworld.OrderBy(c => c.Value.Y).ThenBy(c => c.Value.X))
        {
            Log($"overworld {cell}: {room.Name} ({room.Title})");
        }

        var outdoors = dungeon.Rooms.Where(WorldMap.IsOutdoors).ToList();
        Check($"every outdoor area is on the map ({overworld.Count} of {outdoors.Count})", outdoors.All(overworld.ContainsKey));
        Check("no two areas share a spot", overworld.Values.Distinct().Count() == overworld.Count);

        // Most of the world has been explored; a couple of areas haven't.
        foreach (var room in outdoors.Take(outdoors.Count - 2))
        {
            room.Visited = true;
        }

        var burrow = dungeon.FoyerOf("The Old Burrow")!;
        var rooms = WorldMap.Dungeon(burrow);
        foreach (var name in new[] { "BurrowFoyer", "Entrance", "KeyHall" })
        {
            rooms.Keys.First(r => r.Name == name).Visited = true;
        }

        // P2 goes into the dungeon (the Key Hall).
        var keyHall = rooms.Keys.First(r => r.Name == "KeyHall");
        dungeon.Travel(keyHall.Doors.First(d => d.Side == DoorSide.Bottom), new[] { p2 });
        await Seconds(1.0);
        Check("P2 is in the Key Hall", dungeon.RoomOf(p2) == keyHall);

        // P1 opens the map from the Start menu: the Map ring comes first.
        await Tap(_bots[0].Menu);
        await Seconds(0.3);
        await Tap(_bots[0].Primary);
        await Seconds(0.3);
        Check("P1's map is open", p1.IsMapOpen);
        await Shot("overworld");

        // P2 opens it in the dungeon (the dungeon's page), then flips to the overworld.
        await Tap(_bots[1].Menu);
        await Seconds(0.3);
        await Tap(_bots[1].Primary);
        await Seconds(0.3);
        Check("P2's map is open", p2.IsMapOpen);
        await Shot("dungeon");
        await Tap(_bots[1].Right);
        await Seconds(0.3);
        await Shot("flipped");

        // Any button closes it.
        await Tap(_bots[0].Primary);
        await Tap(_bots[1].Primary);
        await Seconds(0.2);
        Check("both maps closed", !p1.IsMapOpen && !p2.IsMapOpen);

        GD.Print($"[map] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    private async Task Tap(Key key)
    {
        SetKeys(new HashSet<Key> { key });
        for (var i = 0; i < 3; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        SetKeys(new HashSet<Key>());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void SetKeys(HashSet<Key> wanted)
    {
        foreach (var key in _bots.SelectMany(b => b.AllKeys))
        {
            var down = wanted.Contains(key);
            if (down == _held.Contains(key) && down == Input.IsPhysicalKeyPressed(key))
            {
                continue;
            }

            if (down) _held.Add(key); else _held.Remove(key);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
        }
    }

    private void Check(string what, bool ok)
    {
        if (ok) _passed++; else _failed++;
        Log($"{(ok ? "PASS" : "FAIL")}: {what}");
    }

    private static void Log(string text) => GD.Print($"[map] {text}");

    private async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_folder}/map_{_shot++:00}_{name}.png");
    }
}
