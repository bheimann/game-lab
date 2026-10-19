using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for heroes going their own ways: <c>-- --room=Entrance --autoplay-split=&lt;folder&gt;</c> (any dungeon room
/// with a door further in and a door out). P1 (lizard) and P2 (fox) join; P1 goes through to the next room alone (two
/// views, both rooms awake), beats its enemies, gets knocked out there (gets up where it came in, not next to P2),
/// and comes back (the beaten enemies stay beaten while anyone's in the dungeon); then both leave the dungeon
/// (its enemies are back). Started in a boss room (<c>--room=BossRoom</c>) it checks the boss fight instead: one shared
/// view while the boss fights, split again once it's beaten.
/// Logs PASS / FAIL for each check and saves a screenshot at each step.
/// </summary>
public partial class DevSplitCheck : Node
{
    public const string Flag = "--autoplay-split=";

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

    public static DevSplitCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _Ready() => Run();

    private async void Run()
    {
        await Seconds(0.3);
        var manager = PlayerManager.Find(this)!;
        var dungeon = Dungeon.Find(this)!;
        var split = SplitScreen.Find(this)!;
        var p1 = manager.JoinForDev(InputDevice.KeyboardLeft, 0, "")!;
        var p2 = manager.JoinForDev(InputDevice.KeyboardRight, 1, "")!;
        await Seconds(0.5);

        var start = dungeon.RoomOf(p1);
        if (start.IsBossRoom)
        {
            await BossFight(start, split);
            return;
        }

        var inward = start.Doors.First(d => d.Partner is { } p && p.Room.Area == start.Area && p.Room != start);
        var outward = start.Doors.First(d => d.Partner is { } p && p.Room.Area != start.Area);
        var next = inward.Partner!.Room;
        Log($"start in {start.Name} ({start.Area}); further in: {next.Name}; out: {outward.Partner!.Room.Name}");
        Check("two heroes, two views", split.ViewCount == 2 && !split.Shared);

        // The doors only open once the room's enemies are beaten.
        Beat(start);
        await Seconds(1.0);
        Check("the doors are open", inward.IsOpen && outward.IsOpen);

        // 1. P1 goes on alone.
        await Walk((p1, _bots[0], inward));
        Check("P1 went on alone", dungeon.RoomOf(p1) == next && dungeon.RoomOf(p2) == start);
        Check("both rooms awake", start.IsActive && next.IsActive);
        Check("each view shows its hero's room",
            split.ViewOf(p1)?.Camera.Room == next.Layout && split.ViewOf(p2)?.Camera.Room == start.Layout);
        await Shot("apart");

        // 2. P1 beats the enemies in there.
        var enemies = next.Enemies.ToList();
        Beat(next);

        await Seconds(0.5);
        Log($"{next.Name} has {enemies.Count} enemies");
        Check("P1 beat them", enemies.Count > 0 && enemies.All(e => !e.IsHostile));

        // 3. P1 is knocked out with nobody else in the room: up again where it came in.
        p1.TakeHit(999, p1.GlobalPosition + new Vector2(10, 0));
        await Seconds(p1.RespawnSeconds + 0.5);
        Check("P1 got back up in its own room, where it came in",
            !p1.IsDown && dungeon.RoomOf(p1) == next && p1.GlobalPosition.DistanceTo(dungeon.RespawnPointFor(p1)) < 24);

        // 4. P1 comes back: the room behind goes to sleep, its enemies stay beaten.
        await Walk((p1, _bots[0], inward.Partner!));
        Check("P1 is back with P2", dungeon.RoomOf(p1) == start && dungeon.RoomOf(p2) == start);
        Check("the empty room is asleep", !next.IsActive && start.IsActive);
        Check("its enemies stay beaten (someone's still in the dungeon)", enemies.All(e => !e.IsHostile));
        await Shot("together");

        // 5. Both leave the dungeon: everything in it is back.
        await Walk((p1, _bots[0], outward), (p2, _bots[1], outward));
        var outside = outward.Partner!.Room;
        Check("both left the dungeon", dungeon.RoomOf(p1) == outside && dungeon.RoomOf(p2) == outside);
        Check("the dungeon is asleep", !start.IsActive && !next.IsActive);
        Check("its enemies are back (nobody's in the dungeon)", enemies.All(e => e.IsHostile));
        await Shot("outside");

        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    private async Task BossFight(DungeonRoom room, SplitScreen split)
    {
        Log($"start in the boss room {room.Name}");
        Check("everyone in the boss room shares one view", split.Shared && split.ViewCount == 1);
        await Shot("boss");

        // Walk up to the boss so it wakes, then keep hitting it (a shell might only let 1 damage through).
        var boss = room.Enemies.OfType<Node2D>().First();
        var heroes = PlayerManager.Find(this)!.Players.OfType<Hero>().ToList();
        for (var i = 0; i < heroes.Count; i++)
        {
            heroes[i].Teleport(boss.GlobalPosition + new Vector2(-40 + i * 80, 50));
        }

        for (var time = 0.0; time < 20 && !room.IsCleared; time += GetPhysicsProcessDeltaTime())
        {
            Beat(room);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        await Seconds(0.3);
        Check("the boss is beaten", room.IsCleared);
        Check("the screen splits again", !split.Shared && split.ViewCount == 2);
        await Shot("beaten");
        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    private static void Beat(DungeonRoom room)
    {
        foreach (var enemy in room.Enemies.OfType<IHittable>().Where(e => e.CanBeHit))
        {
            enemy.TakeHit(999, room.FloorRect.GetCenter());
        }
    }

    /// <summary>Each hero starts just inside its door and walks out through it; waits until they've all arrived.</summary>
    private async Task Walk(params (Hero Hero, AutoplayBot Bot, Door Door)[] walks)
    {
        var dungeon = Dungeon.Find(this)!;
        for (var i = 0; i < walks.Length; i++)
        {
            var (hero, _, door) = walks[i];
            hero.Teleport(door.EntryPoint); // one behind the other: doorways are only one hero wide
        }

        for (var time = 0.0; time < 4.0; time += GetPhysicsProcessDeltaTime())
        {
            var wanted = new HashSet<Key>();
            foreach (var (hero, bot, door) in walks.Where(w => dungeon.RoomOf(w.Hero) != w.Door.Partner!.Room))
            {
                bot.Push(door.Outward, wanted);
            }

            SetKeys(wanted);
            if (wanted.Count == 0)
            {
                break;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        SetKeys(new HashSet<Key>());
        await Seconds(0.6); // the fade in
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

    private static void Log(string text) => GD.Print($"[split] {text}");

    private async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_folder}/split_{_shot++:00}_{name}.png");
    }
}
