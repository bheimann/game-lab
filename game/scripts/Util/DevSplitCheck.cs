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
/// views, both rooms awake), beats its enemies, gets knocked out there (gets up in the dungeon's foyer), and comes
/// back (the beaten enemies stay beaten while anyone's in the dungeon); then both leave the dungeon (its enemies are
/// back), P1 is knocked out in the overworld (gets up in Scalebrook Village) and P3 joins (in the village too). Started in a boss room (<c>--room=BossRoom</c>) it checks the boss fight instead: one shared
/// view while the boss fights, split again once it's beaten. Started in the room leading to a boss hall
/// (<c>--room=PuzzleRoom</c>, <c>FrozenRiver</c>, <c>MossHall</c>) it checks the boss hall: a hero in the boss
/// doorway is held there (pushing back steps out) until everyone's in it, then they all go in together; the way back
/// shuts while the fight is on (and opens again if everyone's knocked out); both open once the boss is beaten. Started in a foyer
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

        // 3. P1 is knocked out in the dungeon with P2 still standing: P1 stays down (P2 could come and help), can't go
        //    back before the wait, and can let a fairy out to get up right there.
        Items.Purse.AddFairies(1);
        await KnockDown(p1);
        await Seconds(p1.RespawnSeconds + 1);
        Check("P1 stays down: P2 could still come and help", p1.IsDown && dungeon.RoomOf(p1) == next && dungeon.CanBeHelped(p1));
        await HoldKey(_bots[0].Primary, 0.8);
        Check("P1 can't go back yet (waiting for help comes first)", p1.IsDown && dungeon.RoomOf(p1) == next);
        await Shot("down");
        await Tap(_bots[0].Item);
        Check("P1 let a fairy out: up again right there, with half hearts",
            !p1.IsDown && dungeon.RoomOf(p1) == next && p1.Health.Current == p1.Health.Max / 2 && Items.Purse.Fairies == 0);

        // ... and knocked out again: this time P1 goes back (after the wait), up again in the dungeon's foyer.
        await Seconds(1.6); // (the flicker after getting up)
        await KnockOutAndGoBack(p1, _bots[0]);
        var foyer = dungeon.RoomOf(p1);
        Check("P1 got back up in the dungeon's foyer", !p1.IsDown && IsFoyer(foyer) && foyer.Area == start.Area);

        // 4. P1 comes back: the room behind goes to sleep, its enemies stay beaten.
        await Walk((p1, _bots[0], foyer.Doors.First(d => d.Partner?.Room == start)));
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

        // 6. Knocked out in the overworld, going back: up again in the last village (Scalebrook: P1 hasn't been to one).
        await KnockOutAndGoBack(p1, _bots[0]);
        Check("P1 got back up in Scalebrook Village", dungeon.RoomOf(p1) is { IsVillage: true } village && village.Name == "Village");

        // 7. P3 joins: in Scalebrook Village, not next to the others.
        var joinManager = PlayerManager.Find(this)!;
        var p3 = joinManager.JoinForDev(InputDevice.Gamepad(92), 2, "");
        await Seconds(0.3);
        Log($"P3 join: hero={p3?.Name ?? "none"}, boss fight on={joinManager.BossFightOn}, slots: {string.Join(", ", joinManager.Slots.Select(sl => sl.IsFree ? "free" : sl.Hero is { } h ? h.Name.ToString() : sl.IsWaitingForBossFight ? "waiting" : "picking"))}");
        Check("P3 joined in Scalebrook Village", p3 is not null && dungeon.RoomOf(p3).Name == "Village");
        await Shot("joined");

        GD.Print($"[split] done: {_passed} passed, {_failed} failed");
        GetTree().Quit();
    }

    /// <summary>Knock a hero out with nobody to help them, and wait until they've gone back by themselves.</summary>
    private async Task KnockOut(Hero hero)
    {
        await KnockDown(hero);
        await Seconds(hero.RespawnSeconds + 0.8);
    }

    /// <summary>Hit a hero until they're down (just after a hit they flicker and can't be hit for a moment).</summary>
    private async Task KnockDown(Hero hero)
    {
        for (var time = 0.0; time < 3 && !hero.IsDown; time += GetPhysicsProcessDeltaTime())
        {
            hero.TakeHit(999, hero.GlobalPosition + new Vector2(10, 0));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    /// <summary>
    /// Knock a hero out while a teammate could help: wait out the wait, then hold the button to go back.
    /// <paramref name="keepUp"/>: a teammate whose health is kept topped up meanwhile (so a boss doesn't knock them out
    /// too while they stand idle: that would make it a wipe).
    /// </summary>
    private async Task KnockOutAndGoBack(Hero hero, AutoplayBot bot, Hero? keepUp = null)
    {
        await KnockDown(hero);
        for (var time = 0.0; time < hero.GiveUpSeconds + 0.3; time += 0.1)
        {
            keepUp?.Health.Refill();
            await Seconds(0.1);
        }

        await HoldKey(bot.Primary, 0.9);
        await Seconds(0.8); // the fade
    }

    private async Task HoldKey(Key key, double seconds)
    {
        SetKeys(new HashSet<Key> { key });
        await Seconds(seconds);
        SetKeys(new HashSet<Key>());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static bool IsFoyer(DungeonRoom room) => room.GetChildren().OfType<GatherMarker>().Any();

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

        // 1. P1 goes into the boss hall alone, and into the boss doorway: held there, waiting for P2.
        await Walk((p1, _bots[0], toHall));
        Check("P1 is in the boss hall", dungeon.RoomOf(p1) == hall);
        Check("both of the boss hall's doors are open", bossDoor.IsOpen && backDoor.IsOpen);
        await HoldAt(p1, _bots[0], bossDoor);
        await Seconds(0.5);
        Check("P1 is held in the boss doorway, waiting for P2", dungeon.IsAtBossDoor(p1) && p1.IsHeld && dungeon.RoomOf(p1) == hall);
        await Shot("waiting");

        // P1 pushes back: out of the doorway again, then back in.
        await StepBack(_bots[0], bossDoor);
        Check("pushing back steps P1 out of the doorway", !dungeon.IsAtBossDoor(p1) && !p1.IsHeld && dungeon.RoomOf(p1) == hall);
        await HoldAt(p1, _bots[0], bossDoor);
        Check("P1 is back in the doorway", dungeon.IsAtBossDoor(p1));

        // 2. P2 comes too: the way back stays open until someone's in the boss room.
        await Walk((p2, _bots[1], toHall));
        Check("P2 is in the boss hall; the way back is still open", dungeon.RoomOf(p2) == hall && backDoor.IsOpen);

        // 3. P2 steps into the doorway too: everyone goes in together.
        await Walk((p2, _bots[1], bossDoor));
        Check("everyone went in together: one shared view", dungeon.RoomOf(p1) == boss && dungeon.RoomOf(p2) == boss && split.Shared && split.ViewCount == 1);
        Check("nobody's held any more", !p1.IsHeld && !p2.IsHeld);

        // 3b. P3 tries to join during the fight: nobody joins until it's over (it's on one shared screen).
        var manager = PlayerManager.Find(this)!;
        var p3 = manager.JoinForDev(InputDevice.Gamepad(92), 2, "");
        await Seconds(0.3);
        // (P3's slot is the one with its controller: a real controller on this computer may have taken a slot too.)
        var p3Slot = manager.Slots.ToList().FindIndex(sl => sl.Input?.Device == InputDevice.Gamepad(92));
        Check("P3 can't join during the boss fight: waiting", p3 is null && p3Slot >= 0 && manager.Slots[p3Slot] is { Hero: null, IsWaitingForBossFight: true });
        Check("still one shared view", split.Shared && split.ViewCount == 1);

        // 3c. P1 is knocked out: stays down in the boss room, and P2 wakes P1 with smelling salts (half hearts).
        await KnockDown(p1);
        await Seconds(p1.RespawnSeconds + 1);
        Check("P1 stays down in the boss room (P2 is still fighting)", p1.IsDown && dungeon.RoomOf(p1) == boss);
        Items.Purse.AddSalts(1);
        p2.Teleport(p1.GlobalPosition + new Vector2(14, 0));
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        p2.UseSalts();
        Check("P2 woke P1 with smelling salts: half hearts", !p1.IsDown && p1.Health.Current == p1.Health.Max / 2 && Items.Purse.Salts == 0);

        // 3d. P1 is knocked out again: no going back in a boss fight (it stays on one shared screen), even after the
        //     wait, while P2 is still fighting.
        await Seconds(1.6); // (the flicker after getting up)
        await KnockOutAndGoBack(p1, _bots[0], keepUp: p2);
        Check("P1 can't go back in a boss fight: still down in the boss room", p1.IsDown && dungeon.RoomOf(p1) == boss);
        Check("still one shared view", split.Shared && split.ViewCount == 1);
        await Shot("down in the fight");

        // 3e. P2 is knocked out too: nobody can help (no fairies), so the team goes back to the boss hall together,
        //     and the boss starts over: everyone can try again.
        await KnockOut(p2);
        var bosses = boss.Enemies.OfType<IBoss>().ToList();
        Check("everyone was sent back to the boss hall", dungeon.RoomOf(p1) == hall && dungeon.RoomOf(p2) == hall);
        Check("the boss started over (full health, not fighting)", bosses.Count > 0 && bosses.All(b => !b.IsFighting && b.Health.Current == b.Health.Max));
        Check("nobody's waiting any more", !dungeon.IsBenched(p1) && !dungeon.IsBenched(p2));
        Check("the fight's over: the way back is open again (the team can go and get ready)", backDoor.IsOpen);
        await Seconds(0.3);
        Check("the fight's over: P3 joined, in Scalebrook Village", manager.Slots[p3Slot].Hero is { } joined && dungeon.RoomOf(joined).Name == "Village");
        manager.Leave(p3Slot); // (P3 has no keys to play with; the others go back in without waiting for it)
        await Seconds(0.2);
        await Walk((p1, _bots[0], bossDoor), (p2, _bots[1], bossDoor));
        Check("back in for another try", dungeon.RoomOf(p1) == boss && dungeon.RoomOf(p2) == boss);

        // 4. Beat the boss (with P1 knocked out in there): P1 gets back up, and both of the boss hall's doors open again.
        await Seconds(1.6); // (the flicker after getting in)
        await KnockDown(p1);
        await BeatBoss(boss);
        Check("the boss is beaten", boss.IsCleared);
        await Seconds(1.5);
        Check("P1 got back up in the boss room, full hearts", !p1.IsDown && dungeon.RoomOf(p1) == boss && p1.Health.Current == p1.Health.Max);
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

    /// <summary>Walk a hero into a boss doorway until they're held there.</summary>
    private async Task HoldAt(Hero hero, AutoplayBot bot, Door door)
    {
        var dungeon = Dungeon.Find(this)!;
        hero.Teleport(door.EntryPoint);
        for (var time = 0.0; time < 3 && !dungeon.IsAtBossDoor(hero); time += GetPhysicsProcessDeltaTime())
        {
            var wanted = new HashSet<Key>();
            bot.Push(door.Outward, wanted);
            SetKeys(wanted);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        SetKeys(new HashSet<Key>());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>Push away from a door for a moment (stepping back out of the boss doorway).</summary>
    private async Task StepBack(AutoplayBot bot, Door door)
    {
        var wanted = new HashSet<Key>();
        bot.Push(-door.Outward, wanted);
        SetKeys(wanted);
        for (var i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        SetKeys(new HashSet<Key>());
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
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
