using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for dungeon scenes: <c>-- --autoplay=&lt;folder&gt;</c> and two bots (P1 lizard on WASD, P2 fox on
/// the arrows) walk from the village through the swamp and the meadow into the first dungeon and play it to the boss by pressing the real keys. Each room has a
/// little plan (Frostfang Fortress and Mossmire Keep too, starting with --room=FrostEntrance or --room=MossEntrance): fight, open the chest, walk through the right door, solve the lever puzzle (the fox bombs the rocks
/// and flips that lever, the lizard tongues the lever across the pit), beat the beetle, and take the exit portal out.
/// Logs what happens and saves a screenshot every 2 seconds (and on every new room) into the folder.
/// Heroes go through doors on their own, so when the bots end up in different rooms (one went through a door first, or
/// was knocked out and got up in the foyer or a village), the one behind finds its way back while the other waits.
/// Knocked out of a boss fight, a bot waits in the boss hall while the other fights on. (The bots aren't good enough to
/// beat a boss alone, so in boss fights their health is topped up when it runs low: this checks the dungeon can be
/// played through, not how hard the boss is.)
/// </summary>
public partial class DevDungeonAutoplay : Node
{
    private const double MaxSeconds = 600; // a bot knocked out of a boss fight leaves the other to fight alone

    private readonly AutoplayBot[] _bots =
    {
        new(0, Key.W, Key.A, Key.S, Key.D, Key.J, Key.K, Key.L, Key.Backspace, Key.I, Key.Q, Key.E, Key.Tab, Key.Shift, isFox: false),
        new(1, Key.Up, Key.Left, Key.Down, Key.Right, Key.Period, Key.Slash, Key.Comma, Key.Delete, Key.M, Key.Semicolon, Key.Apostrophe, Key.Enter, Key.Ctrl, isFox: true),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private double _nextShot;
    private double _nextLog;
    private int _shotNumber;
    private DungeonRoom? _lastRoom;
    private Hero? _behind;
    private bool _watchingDoors;
    private double _completeAt = -1;

    /// <summary>The dungeon being played (the area of the last dungeon room the team was in).</summary>
    private string _area = "";

    public static DevDungeonAutoplay Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var manager = PlayerManager.Find(this)!;
        var dungeon = Dungeon.Find(this)!;
        var room = dungeon.CurrentRoom;
        var wanted = new HashSet<Key>();
        var lizard = manager.Players[0];
        var fox = manager.Players[1];

        if (_time < 2)
        {
            // Join: P1 picks the lizard, P2 goes right once and picks the fox.
            PressAt(wanted, _bots[0].Primary, 0.2, 0.6);
            PressAt(wanted, _bots[1].Primary, 0.8);
            PressAt(wanted, _bots[1].Right, 1.1);
            PressAt(wanted, _bots[1].Primary, 1.4);
        }
        else if (lizard is not null && fox is not null)
        {
            foreach (var hero in new[] { lizard, fox }.Where(h => !h.IsDown && dungeon.RoomOf(h) is { IsBossRoom: true, IsCleared: false }))
            {
                if (hero.Health.Current <= hero.Health.Max / 4)
                {
                    hero.Health.Refill();
                    GD.Print($"[dungeon {_time:0.0}s] (dev help: topped up {hero.Name}'s health in the boss fight)");
                }
            }

            if (!_watchingDoors)
            {
                _watchingDoors = true;
                // Whoever went on through a door leaves the other behind; whoever got knocked out is behind.
                dungeon.HeroLeftRoom += (hero, _) => _behind = hero == lizard ? fox : lizard;
                dungeon.HeroRevived += hero => _behind = hero;
            }

            if (!CatchUp(dungeon, lizard, fox, wanted))
            {
                Play(room, dungeon, lizard, fox, wanted);
            }

            _bots[0].Unstick(lizard, wanted, delta);
            _bots[1].Unstick(fox, wanted, delta);
        }

        foreach (var key in _bots.SelectMany(b => b.AllKeys).Distinct())
        {
            SetKey(key, wanted.Contains(key));
        }

        if (room != _lastRoom)
        {
            _lastRoom = room;
            GD.Print($"[dungeon {_time:0.0}s] ---- now in {room.Name} ({room.Title}), keys={dungeon.Keys}");
            TakeShot(0.5);
        }

        if (_time >= _nextLog)
        {
            _nextLog += 1;
            var heroes = string.Join(" ", manager.Players.Where(p => p is not null).Select(p => $"{p!.Name}:hp{p.Health.Current}{(p.IsDown ? "DOWN" : "")}@{p.GlobalPosition - room.GlobalPosition:0}{(dungeon.RoomOf(p) != room ? $" in {dungeon.RoomOf(p).Name}" : "")}"));
            var enemies = string.Join(" ", room.Enemies.OfType<Node2D>().Select(e => e is IBoss boss ? $"{e.Name}:hp{boss.Health.Current}" : $"{e.Name.ToString()[0]}{(((IEnemy)e).IsHostile ? "!" : "z")}"));
            GD.Print($"[dungeon {_time:0.0}s] {room.Name} | {heroes} | {enemies} | keys={dungeon.Keys}");
        }

        if (_time >= _nextShot)
        {
            _nextShot += 2;
            TakeShot(0);
        }

        if (room.Area != "")
        {
            _area = room.Area;
        }

        if (_area != "" && dungeon.IsDungeonComplete(_area) && _completeAt < 0)
        {
            _completeAt = _time;
            GD.Print($"[dungeon {_time:0.0}s] DUNGEON COMPLETE");
            TakeShot(0.3);
        }

        // After the boss, wait until the team is back out in the overworld.
        var backOutside = room.Area == "" && _time - _completeAt > 1.5;
        if ((_completeAt >= 0 && (backOutside || _time - _completeAt > 25)) || _time > MaxSeconds)
        {
            GD.Print($"[dungeon {_time:0.0}s] done: {_area} complete={dungeon.IsDungeonComplete(_area)}, back in {room.Name}");
            TakeShot(0);
            GetTree().CreateTimer(0.3).Timeout += () => GetTree().Quit();
            SetPhysicsProcess(false);
        }
    }

    private void Play(DungeonRoom room, Dungeon dungeon, Hero lizard, Hero fox, HashSet<Key> wanted)
    {
        var enemies = room.Enemies.OfType<Node2D>().ToList();

        // Outdoors, only fight what's close (or attacking); otherwise walk on through.
        var heroes = new[] { lizard, fox };
        var fight = room.Area != "" || enemies.OfType<IEnemy>().Any(e => e.IsHostile && heroes.Any(h => e.IsTargeting(h)))
            || enemies.Any(e => e is IEnemy { IsHostile: true } && heroes.Any(h => h.GlobalPosition.DistanceTo(e.GlobalPosition) < 60));
        if (!room.IsCleared && fight)
        {
            _bots[0].Fight(lizard, enemies, wanted);
            _bots[1].Fight(fox, enemies, wanted);
            return;
        }

        switch (room.Name)
        {
            case "Village":
                Exit(room, "EastPath", lizard, fox, wanted);
                break;

            case "Swamp":
                Exit(room, "NorthPath", lizard, fox, wanted);
                break;

            case "Overworld":
                Exit(room, "BurrowStairs", lizard, fox, wanted);
                break;

            case "Entrance":
                Exit(room, "DoorTop", lizard, fox, wanted);
                break;

            case "KeyHall":
                Exit(room, dungeon.Keys > 0 || room.GetNode<Door>("DoorTop").IsOpen ? "DoorTop" : "DoorRight", lizard, fox, wanted);
                break;

            case "SideRoom":
                var chest = room.GetNode<TreasureChest>("KeyChest");
                if (!chest.IsOpened)
                {
                    _bots[0].Steer(lizard, chest.GlobalPosition, wanted, feet: true);
                    _bots[1].Steer(fox, chest.GlobalPosition, wanted, feet: true);
                }
                else
                {
                    Exit(room, "DoorLeft", lizard, fox, wanted);
                }

                break;

            case "PuzzleRoom":
                Puzzle(room, lizard, fox, wanted);
                break;

            case "FrostEntrance":
                Exit(room, "DoorTop", lizard, fox, wanted);
                break;

            case "IceHall":
                // The bots can't plan slides (the ice check covers the puzzle), so P1 is put on the key, then both
                // walk round the edge of the ice to the locked top door.
                var key = room.GetNode<Node2D>("IceKey");
                if (dungeon.Keys == 0 && key.Visible)
                {
                    GD.Print($"[dungeon {_time:0.0}s] ice hall: putting P1 on the key");
                    lizard.Teleport(key.GlobalPosition);
                }
                else if (lizard.GlobalPosition.Y > room.GlobalPosition.Y + 60 || fox.GlobalPosition.Y > room.GlobalPosition.Y + 60)
                {
                    lizard.Teleport(room.GlobalPosition + new Vector2(140, 46));
                    fox.Teleport(room.GlobalPosition + new Vector2(166, 46));
                }
                else
                {
                    Exit(room, "DoorTop", lizard, fox, wanted);
                }

                break;

            case "FrozenRiver":
                var lever = room.GetNode<LeverSwitch>("RiverLever");
                if (!lever.IsOn)
                {
                    // P2 (the fox) walks up to the lever and bites it; P1 waits by the river.
                    var spot = lever.GlobalPosition + new Vector2(-14, 0);
                    if (fox.GlobalPosition.DistanceTo(spot) > 4)
                    {
                        _bots[1].Steer(fox, spot, wanted, slack: 2f, feet: true);
                    }
                    else if (fox.Facing.Dot(Vector2.Right) < 0.9f)
                    {
                        wanted.Add(_bots[1].Right);
                    }
                    else
                    {
                        _bots[1].Tap(_bots[1].Primary, wanted);
                    }
                }
                else
                {
                    // Over the bridge (in the middle) and out the top.
                    foreach (var (bot, hero) in new[] { (_bots[0], lizard), (_bots[1], fox) })
                    {
                        var x = room.GlobalPosition.X + 152;
                        if (Mathf.Abs(hero.GlobalPosition.X - x) > 3 && hero.GlobalPosition.Y > room.GlobalPosition.Y + 90)
                        {
                            bot.Steer(hero, new Vector2(x, room.GlobalPosition.Y + 150), wanted, slack: 2f, feet: true);
                        }
                        else
                        {
                            bot.Steer(hero, new Vector2(x, hero.GlobalPosition.Y), wanted, slack: 2f, feet: true);
                            wanted.Add(bot.Up);
                        }
                    }
                }

                break;

            case "MossEntrance":
            case "MossHall":
            case "BurrowFoyer" or "FrostFoyer" or "MossFoyer":
            case "BurrowBossHall" or "FrostBossHall" or "MossBossHall":
                Exit(room, "DoorTop", lizard, fox, wanted);
                break;

            case "MossBog":
                Bog(room, dungeon, lizard, fox, wanted);
                break;

            case "BossRoom":
            case "FrostBoss":
            case "MossBoss":
                // Cleared: the team stays until someone uses the exit portal. After a few seconds, P1 stands on it
                // (P2 waits nearby) until it takes them out.
                var portal = room.GetNode<ExitPortal>("ExitPortal");
                if (portal.IsOpen && _completeAt >= 0 && _time - _completeAt is > 3 and < 3.02)
                {
                    GD.Print($"[dungeon {_time:0.0}s] portal test: 3 s after the boss, still in {room.Name}, portal open={portal.IsOpen}");
                    TakeShot(0);
                }

                if (portal.IsOpen && _completeAt >= 0 && _time - _completeAt > 3)
                {
                    _bots[0].Steer(lizard, portal.GlobalPosition, wanted, slack: 2f, feet: true);
                    _bots[1].Steer(fox, portal.GlobalPosition + new Vector2(0, 30), wanted, slack: 2f, feet: true);
                }

                break;
        }
    }

    /// <summary>Mossmire Keep's Bog Hall: open the key chest, the lizard tongues the lever across the bog for the
    /// bridge, then both cross in the middle and unlock the top door.</summary>
    private void Bog(DungeonRoom room, Dungeon dungeon, Hero lizard, Hero fox, HashSet<Key> wanted)
    {
        var chest = room.GetNode<TreasureChest>("BogChest");
        if (!chest.IsOpened)
        {
            _bots[0].Steer(lizard, chest.GlobalPosition, wanted, feet: true);
            _bots[1].Steer(fox, chest.GlobalPosition, wanted, feet: true);
            return;
        }

        var lever = room.GetNode<LeverSwitch>("BogLever");
        if (!lever.IsOn)
        {
            var spot = new Vector2(lever.GlobalPosition.X, room.GlobalPosition.Y + 140);
            if (lizard.GlobalPosition.DistanceTo(spot) > 4)
            {
                _bots[0].Steer(lizard, spot, wanted, slack: 2f, feet: true);
            }
            else if (lizard.Facing.Dot(Vector2.Up) < 0.9f)
            {
                wanted.Add(_bots[0].Up);
            }
            else
            {
                _bots[0].Tap(_bots[0].Secondary, wanted);
            }

            return;
        }

        foreach (var (bot, hero) in new[] { (_bots[0], lizard), (_bots[1], fox) })
        {
            var x = room.GlobalPosition.X + 152;
            if (hero.GlobalPosition.Y > room.GlobalPosition.Y + 90 && Mathf.Abs(hero.GlobalPosition.X - x) > 3)
            {
                bot.Steer(hero, new Vector2(x, room.GlobalPosition.Y + 150), wanted, slack: 2f, feet: true);
            }
            else if (hero.GlobalPosition.Y > room.GlobalPosition.Y + 90)
            {
                bot.Steer(hero, new Vector2(x, hero.GlobalPosition.Y), wanted, slack: 2f, feet: true);
                wanted.Add(bot.Up);
            }
        }

        if (lizard.GlobalPosition.Y < room.GlobalPosition.Y + 90 && fox.GlobalPosition.Y < room.GlobalPosition.Y + 90)
        {
            Exit(room, "DoorTop", lizard, fox, wanted);
        }
    }

    private void Puzzle(DungeonRoom room, Hero lizard, Hero fox, HashSet<Key> wanted)
    {
        var bridge = room.GetNode<Pit>("Bridge");
        if (bridge.HasBridge)
        {
            // Line up with the bridge, cross it, go through the top door.
            foreach (var (bot, hero) in new[] { (_bots[0], lizard), (_bots[1], fox) })
            {
                var x = room.GlobalPosition.X + 168;
                if (Mathf.Abs(hero.GlobalPosition.X - x) > 3 && hero.GlobalPosition.Y > room.GlobalPosition.Y + 90)
                {
                    bot.Steer(hero, new Vector2(x, room.GlobalPosition.Y + 140), wanted, slack: 2f, feet: true);
                }
                else
                {
                    bot.Steer(hero, new Vector2(x, hero.GlobalPosition.Y), wanted, slack: 2f, feet: true);
                    wanted.Add(bot.Up);
                }
            }

            return;
        }

        // Lizard: stand under the lever across the pit, face up, flick the tongue.
        var acrossPit = room.GetNode<LeverSwitch>("LeverAcrossPit");
        if (!acrossPit.IsOn)
        {
            var spot = new Vector2(acrossPit.GlobalPosition.X, room.GlobalPosition.Y + 136);
            if (lizard.GlobalPosition.DistanceTo(spot) > 4)
            {
                _bots[0].Steer(lizard, spot, wanted, slack: 2f, feet: true);
            }
            else if (lizard.Facing.Dot(Vector2.Up) < 0.9f)
            {
                wanted.Add(_bots[0].Up);
            }
            else
            {
                _bots[0].Tap(_bots[0].Secondary, wanted);
            }
        }

        // Fox: bomb the cracked rocks from the right, then walk in and bite the lever.
        var behindRocks = room.GetNode<LeverSwitch>("LeverBehindRocks");
        var rocksLeft = room.GetChildren().OfType<Boulder>().Count(b => b.Name.ToString() is "Rock5" or "Rock6" or "Rock7");
        if (!behindRocks.IsOn)
        {
            if (rocksLeft > 0)
            {
                var spot = room.GlobalPosition + new Vector2(96, 192);
                if (fox.GlobalPosition.DistanceTo(spot) > 4)
                {
                    _bots[1].Steer(fox, spot, wanted, slack: 2f, feet: true);
                }
                else if (fox.Facing.Dot(Vector2.Left) < 0.9f)
                {
                    wanted.Add(_bots[1].Left);
                }
                else
                {
                    _bots[1].Tap(_bots[1].Secondary, wanted);
                }
            }
            else
            {
                var spot = behindRocks.GlobalPosition + new Vector2(12, 0);
                if (fox.GlobalPosition.DistanceTo(spot) > 4)
                {
                    _bots[1].Steer(fox, spot, wanted, slack: 2f, feet: true);
                }
                else if (fox.Facing.Dot(Vector2.Left) < 0.9f)
                {
                    wanted.Add(_bots[1].Left);
                }
                else
                {
                    _bots[1].Tap(_bots[1].Primary, wanted);
                }
            }
        }
    }

    /// <summary>Both heroes walk to the door, line up with it, and walk through.</summary>
    /// <summary>
    /// The bots are in different rooms. In a boss fight, whoever's in the boss room fights on (the other waits in the
    /// boss hall); otherwise the one behind walks room by room to the other, who waits. Returns false when they're
    /// together.
    /// </summary>
    private bool CatchUp(Dungeon dungeon, Hero lizard, Hero fox, HashSet<Key> wanted)
    {
        var lizardRoom = dungeon.RoomOf(lizard);
        var foxRoom = dungeon.RoomOf(fox);
        if (lizardRoom == foxRoom)
        {
            return false;
        }

        foreach (var (bot, hero, room) in new[] { (_bots[0], lizard, lizardRoom), (_bots[1], fox, foxRoom) })
        {
            if (room is { IsBossRoom: true, IsCleared: false })
            {
                bot.Fight(hero, room.Enemies.OfType<Node2D>(), wanted);
                return true;
            }
        }

        var (behindBot, behind, ahead) = _behind == lizard ? (_bots[0], lizard, fox) : (_bots[1], fox, lizard);
        if (!behind.IsDown && WayTowards(dungeon.RoomOf(behind), dungeon.RoomOf(ahead)) is { } door)
        {
            ExitOne(behindBot, behind, door, wanted);
        }

        return true;
    }

    /// <summary>The first door on the way from one room to another (through open doors), if there's a way.</summary>
    private static Door? WayTowards(DungeonRoom from, DungeonRoom to)
    {
        var firstDoor = new Dictionary<DungeonRoom, Door?> { [from] = null };
        var queue = new Queue<DungeonRoom>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            if (room == to)
            {
                return firstDoor[room];
            }

            foreach (var door in room.Doors.Where(d => d.IsOpen && d.Partner is not null && !d.SealedUntilOtherDungeons))
            {
                var next = door.Partner!.Room;
                if (!firstDoor.ContainsKey(next))
                {
                    firstDoor[next] = firstDoor[room] ?? door;
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    private void Exit(DungeonRoom room, string doorName, Hero lizard, Hero fox, HashSet<Key> wanted)
    {
        var door = room.GetNode<Door>(doorName);
        foreach (var (bot, hero) in new[] { (_bots[0], lizard), (_bots[1], fox) })
        {
            if (!hero.IsDown)
            {
                ExitOne(bot, hero, door, wanted);
            }
        }
    }

    /// <summary>Walk <paramref name="hero"/> out through <paramref name="door"/>.</summary>
    private static void ExitOne(AutoplayBot bot, Hero hero, Door door, HashSet<Key> wanted)
    {
        var inside = door.EntryPoint;
        var lined = door.Side is DoorSide.Top or DoorSide.Bottom
            ? Mathf.Abs(hero.GlobalPosition.X - inside.X) <= 3
            : Mathf.Abs(hero.GlobalPosition.Y - inside.Y) <= 3;
        var close = hero.GlobalPosition.DistanceTo(inside) < 30;
        if (lined && close)
        {
            bot.Push(door.Outward, wanted);
            bot.Steer(hero, inside, wanted, slack: 2f, feet: true); // keep lined up
            if (door.Side is DoorSide.Top or DoorSide.Bottom) { wanted.Remove(bot.Up); wanted.Remove(bot.Down); bot.Push(door.Outward, wanted); }
            else { wanted.Remove(bot.Left); wanted.Remove(bot.Right); bot.Push(door.Outward, wanted); }
        }
        else
        {
            bot.Steer(hero, inside, wanted, slack: 2f, feet: true);
        }
    }

    private void PressAt(HashSet<Key> wanted, Key key, params double[] times)
    {
        if (times.Any(t => _time >= t && _time < t + 0.12))
        {
            wanted.Add(key);
        }
    }

    private void SetKey(Key key, bool down)
    {
        // (Godot lets go of every key when the window loses focus, so check it's really still held.)
        if (down == _held.Contains(key) && down == Input.IsPhysicalKeyPressed(key))
        {
            return;
        }

        if (down) _held.Add(key); else _held.Remove(key);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
    }

    private async void TakeShot(double after)
    {
        var path = $"{_folder}/shot_{_shotNumber++:00}.png";
        if (after > 0)
        {
            await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
