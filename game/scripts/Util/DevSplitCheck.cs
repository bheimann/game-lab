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
/// view while the boss fights, split again once it's beaten. Started in the room leading to a boss hall
/// (<c>--room=PuzzleRoom</c>, <c>FrozenRiver</c>, <c>MossHall</c>) it checks the boss hall: its boss door waits for
/// everyone, then the way back shuts behind them, and both open once the boss is beaten. Started in a foyer
/// (<c>--room=BurrowFoyer</c>) it checks joining a teammate who waits on the gathering marker, from the menu.
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

        if (start.Doors.FirstOrDefault(d => d.Partner?.Room.Doors.Any(x => x.LeadsToBoss) == true) is { } toHall)
        {
            await BossHall(start, toHall, p1, p2, split);
            return;
        }

        if (start.GetChildren().OfType<GatherMarker>().FirstOrDefault() is { } marker)
        {
            await JoinAtMarker(start, marker, p1, p2);
            return;
        }

        // Further in: a dungeon room that isn't the foyer. Out: towards the foyer (or straight outside).
        static bool IsFoyer(DungeonRoom room) => room.GetChildren().OfType<GatherMarker>().Any();
        var inward = start.Doors.First(d => d.Partner is { } p && p.Room.Area == start.Area && p.Room != start && !IsFoyer(p.Room));
        var outward = start.Doors.First(d => d.Partner is { } p && (p.Room.Area != start.Area || IsFoyer(p.Room)));
        var next = inward.Partner!.Room;
        Log($"start in {start.Name} ({start.Area}); further in: {next.Name}; out through: {outward.Partner!.Room.Name}");
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

        // 5. Both leave the dungeon (through the foyer): everything in it is back.
        await Walk((p1, _bots[0], outward), (p2, _bots[1], outward));
        var outside = outward.Partner!.Room;
        if (outside.Area == start.Area && outside.Doors.FirstOrDefault(d => d.Partner?.Room.Area != start.Area) is { } exit)
        {
            Check("both came into the foyer", dungeon.RoomOf(p1) == outside && dungeon.RoomOf(p2) == outside);
            Check("the dungeon's enemies stay beaten while we're in the foyer", enemies.All(e => !e.IsHostile));
            await Walk((p1, _bots[0], exit), (p2, _bots[1], exit));
            outside = exit.Partner!.Room;
        }

        Check("both left the dungeon", dungeon.RoomOf(p1) == outside && dungeon.RoomOf(p2) == outside);
        Check("the dungeon is asleep", !start.IsActive && !next.IsActive);
        Check("its enemies are back (nobody's in the dungeon)", enemies.All(e => e.IsHostile));
        await Shot("outside");

        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    private async Task BossHall(DungeonRoom room, Door toHall, Hero p1, Hero p2, SplitScreen split)
    {
        var dungeon = Dungeon.Find(this)!;
        var hall = toHall.Partner!.Room;
        var bossDoor = hall.Doors.First(d => d.LeadsToBoss);
        var backDoor = toHall.Partner!;
        var boss = bossDoor.Partner!.Room;
        Log($"start in {room.Name}; the boss hall {hall.Name} leads to {boss.Name}");
        Check("the boss hall has no enemies", !hall.Enemies.Any());

        // Skip this room's own puzzle: beat its enemies and open its shut and locked doors.
        Beat(room);
        foreach (var door in room.Doors.Where(d => d.State is DoorState.Shut or DoorState.Locked))
        {
            door.Open();
        }

        await Seconds(1.0);

        // 1. P1 goes into the boss hall alone: the boss door waits for P2.
        await Walk((p1, _bots[0], toHall));
        Check("P1 is in the boss hall", dungeon.RoomOf(p1) == hall);
        Check("the boss door waits for P2", !bossDoor.IsOpen && bossDoor.IsHeldShut);
        Check("the way back is still open", backDoor.IsOpen);
        await Walk((p1, _bots[0], bossDoor));
        Check("P1 can't go on alone", dungeon.RoomOf(p1) == hall);
        await Shot("waiting");

        // 2. P2 comes in too: the way back shuts behind them and the boss door opens.
        await Walk((p2, _bots[1], toHall));
        Check("everyone's in the boss hall", dungeon.RoomOf(p2) == hall);
        Check("the way back shut behind them", !backDoor.IsOpen && backDoor.IsHeldShut);
        Check("the boss door opened", bossDoor.IsOpen);
        await Walk((p2, _bots[1], backDoor));
        Check("P2 can't go back", dungeon.RoomOf(p2) == hall);
        await Shot("gathered");

        // 3. In to the boss, one after the other.
        await Walk((p1, _bots[0], bossDoor));
        Check("P1 went in first: still split", dungeon.RoomOf(p1) == boss && dungeon.RoomOf(p2) == hall && !split.Shared);
        await Walk((p2, _bots[1], bossDoor));
        Check("everyone's in the boss room: one shared view", dungeon.RoomOf(p2) == boss && split.Shared && split.ViewCount == 1);

        // 4. Beat the boss: both of the boss hall's doors open again.
        await BeatBoss(boss);
        Check("the boss is beaten", boss.IsCleared);
        await Walk((p1, _bots[0], boss.Doors.First(d => d.Partner?.Room == hall)));
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Check("after the boss, both boss hall doors are open", dungeon.RoomOf(p1) == hall && bossDoor.IsOpen && backDoor.IsOpen);
        await Walk((p1, _bots[0], backDoor));
        Check("and P1 can go back", dungeon.RoomOf(p1) == room);

        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    private async Task JoinAtMarker(DungeonRoom room, GatherMarker marker, Hero p1, Hero p2)
    {
        var dungeon = Dungeon.Find(this)!;
        Log($"start in {room.Name} ({marker.PlaceName})");
        // P2 steps out of the dungeon (the rooms further in have enemies that could knock P2 back through the door).
        var away = room.Doors.FirstOrDefault(d => d.Partner?.Room.Area != room.Area) ?? room.Doors.First(d => d.Partner is not null);
        await Walk((p2, _bots[1], away));
        Check("P2 went out", dungeon.RoomOf(p2) == away.Partner!.Room);

        // P1 waits on the stones: P2's menu gets a "Join" ring first; picking it brings P2 here.
        p1.Teleport(marker.GlobalPosition);
        await Seconds(1.4);
        Check("P1 is waiting on the marker", marker.Waiting.Contains(p1));
        await Shot("waiting");
        await Tap(_bots[1].Menu);
        await Seconds(0.4);
        Check("P2's menu is open", p2.IsMenuOpen);
        await Shot("menu");
        await Tap(_bots[1].Primary);
        await Seconds(1.0);
        Log($"P2 in {dungeon.RoomOf(p2).Name} at {p2.GlobalPosition} (join point {marker.JoinPoint}), down={p2.IsDown}, menu open={p2.IsMenuOpen}, hp={p2.Health.Current}");
        Check("P2 joined P1", dungeon.RoomOf(p2) == room && p2.GlobalPosition.DistanceTo(marker.JoinPoint) < 16);
        Check("the foyer is remembered as where to start", dungeon.LastGatherPoint == marker);

        p1.Teleport(marker.GlobalPosition + new Vector2(40, 0));
        await Seconds(0.2);
        Check("P1 stepped off: nobody's waiting", !marker.Waiting.Any());

        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    /// <summary>Press and let go of a key (a few frames, so the game sees it).</summary>
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

    private async Task BossFight(DungeonRoom room, SplitScreen split)
    {
        Log($"start in the boss room {room.Name}");
        Check("everyone in the boss room shares one view", split.Shared && split.ViewCount == 1);
        await Shot("boss");

        await BeatBoss(room);
        Check("the boss is beaten", room.IsCleared);
        Check("the screen splits again", !split.Shared && split.ViewCount == 2);
        await Shot("beaten");
        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    /// <summary>Walk up to the boss so it wakes, then keep hitting it (a shell might only let 1 damage through).</summary>
    private async Task BeatBoss(DungeonRoom room)
    {
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
