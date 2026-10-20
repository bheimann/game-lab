using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the bomb route round a gap, for a fox playing alone:
/// <c>-- --room=PuzzleRoom --autoplay-bomb-route=&lt;folder&gt;</c> (the Old Burrow's Pit of Two Levers) or
/// <c>--room=MossBog</c> (Mossmire Keep's Bog Hall). P1 joins as the fox (Bomb Master) on WASD, P2 as the beetle (Roller)
/// on the arrows. Checks that: the Roller's roll knocks the dead tree over; the fox can't walk through the cracked wall;
/// a bomb blows it open (on the tunnel's side too); the fox walks into the tunnel, bombs a way through the rubble, and
/// comes out through the tunnel's other door on the far side of the gap. Logs each result and saves screenshots.
/// </summary>
public partial class DevBombRouteCheck : Node
{
    public const string Flag = "--autoplay-bomb-route=";

    private static readonly Key[] Keys = { Key.W, Key.A, Key.S, Key.D };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private int _step;
    private double _stepStart;
    private int _shotNumber;
    private int _bombs;
    private DungeonRoom? _room; // where the check started

    public static DevBombRouteCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var wanted = new HashSet<Key>();
        var manager = PlayerManager.Find(this)!;
        var dungeon = Dungeon.Find(this)!;
        var since = _time - _stepStart;
        var fox = manager.Players[0];
        var beetle = manager.Players[1];
        var room = fox is null ? dungeon.CurrentRoom : _room ??= dungeon.RoomOf(fox);
        var burrow = room.Name == "PuzzleRoom";
        if (!burrow && room.Name != "MossBog")
        {
            GD.Print($"[bomb route] nothing to check in {room.Name}: add --room=PuzzleRoom or --room=MossBog");
            SetPhysicsProcess(false);
            GetTree().Quit();
            return;
        }

        var tunnel = room.GetParent().GetNode<DungeonRoom>(burrow ? "BurrowTunnel" : "MossTunnel");
        var crack = room.GetNode<Door>("DoorRightLow");
        var way = room.GetNode<Door>("DoorRightHigh");
        var tree = room.GetNode<FallingTree>(burrow ? "DeadTree" : "BogTree");
        var gapTop = room.GetNode<Pit>(burrow ? "LogPit" : "BogLog").Position.Y;
        Vector2 Where(Hero hero) => hero.GlobalPosition - dungeon.RoomOf(hero).GlobalPosition;

        switch (_step)
        {
            case 0:
                if (since > 0.2 && fox is null)
                {
                    manager.JoinForDev(InputDevice.KeyboardLeft, 1, "Bomb Master");
                    manager.JoinForDev(InputDevice.KeyboardRight, 4, "Roller");
                }

                if (fox is not null && beetle is not null && since > 0.6)
                {
                    Log($"joined: {fox.DisplayName} ({fox.CurrentClass}), {beetle.DisplayName} ({beetle.CurrentClass})");
                    foreach (var enemy in room.Enemies.Concat(tunnel.Enemies).OfType<IHittable>())
                    {
                        enemy.TakeHit(999, enemy.HurtCenter); // guards would get in the way
                    }

                    fox.Teleport(room.GlobalPosition + new Vector2(150, 190));
                    beetle.Teleport(room.GlobalPosition + tree.Position + new Vector2(0, 24));
                    beetle.Face(Vector2.Up);
                    Next();
                }

                break;

            case 1:
                // The Roller rolls into the dead tree.
                if (Once(0.4)) beetle!.GetNode<Ability>("Roll").TryUse();
                if (since > 1.6)
                {
                    Log($"roller rolled into the tree: fallen={tree.IsOn} (want True)");
                    beetle!.Teleport(room.GlobalPosition + new Vector2(150, 200));
                    fox!.Teleport(crack.EntryPoint + new Vector2(-12, 0));
                    fox.Face(Vector2.Right);
                    Next();
                }

                break;

            case 2:
                // Walk into the cracked wall: it's a wall.
                if (since > 0.3) wanted.Add(Key.D);
                if (since > 1.3)
                {
                    Log($"fox walked into the cracked wall: still in {dungeon.RoomOf(fox!).Name} (want {room.Name}), wall open={crack.IsOpen}");
                    Shot(0, "_cracked");
                    fox!.Teleport(crack.EntryPoint + new Vector2(-24, 0));
                    fox.Face(Vector2.Right);
                    Next();
                }

                break;

            case 3:
                if (Once(0.2)) fox!.GetNode<Ability>("Bomb").TryUse();
                if (since > 2.2)
                {
                    var inside = tunnel.GetNode<Door>("DoorLow");
                    Log($"fox bombed the cracked wall: open={crack.IsOpen}, tunnel side open={inside.IsOpen} (want True, True)");
                    Shot(0, "_blown");
                    Next();
                }

                break;

            case 4:
                // Through the hole.
                wanted.Add(Key.D);
                if (dungeon.RoomOf(fox!) == tunnel || since > 2)
                {
                    Log($"fox walked through: now in {dungeon.RoomOf(fox!).Name} (want {tunnel.Name})");
                    Next();
                }

                break;

            case 5:
                // Bomb the rubble from just below the middle of it until there's a gap.
                var rubble = tunnel.GetChildren().OfType<Boulder>().ToList();
                var middle = new Vector2(64, rubble.Count > 0 ? rubble.Max(r => r.Position.Y) + 18 : 0);
                if (since < 0.4)
                {
                    fox!.Teleport(tunnel.GlobalPosition + middle);
                    fox.Face(Vector2.Up);
                    break;
                }

                if (Once(0.5)) { _bombs++; Log($"bomb {_bombs}: thrown={fox!.GetNode<Ability>("Bomb").TryUse()} from {Where(fox):0}"); }
                if (since > 3) // broken rocks crumble for half a second before they're gone
                {
                    var gap = !rubble.Any(r => Mathf.Abs(r.Position.X - 64) < 14);
                    Log($"bomb {_bombs} at the rubble: {rubble.Count} rocks left, gap in the middle={gap}");
                    Shot(0, "_rubble");
                    if (gap || _bombs >= 4)
                    {
                        Next();
                    }
                    else
                    {
                        _stepStart = _time;
                    }
                }

                break;

            case 6:
                // Up through the gap, then over to the other door and out.
                var high = tunnel.GetNode<Door>("DoorHigh");
                if (dungeon.RoomOf(fox!) == tunnel)
                {
                    var target = Where(fox!).Y > high.EntryPoint.Y - tunnel.GlobalPosition.Y + 4 ? new Vector2(64, high.EntryPoint.Y - tunnel.GlobalPosition.Y) : high.EntryPoint - tunnel.GlobalPosition + new Vector2(-30, 0);
                    var to = target - Where(fox!);
                    if (to.Y < -2) wanted.Add(Key.W);
                    if (to.X < -2) wanted.Add(Key.A);
                    if (to.X > 2) wanted.Add(Key.D);
                }

                if (dungeon.RoomOf(fox!) == room || since > 6)
                {
                    Log($"fox came out of the tunnel: in {dungeon.RoomOf(fox!).Name} at {Where(fox!):0}, past the gap={dungeon.RoomOf(fox!) == room && Where(fox!).Y < gapTop} (want True)");
                    Shot(0.4, "_far_side");
                    Next();
                }

                break;

            case 7:
                if (since > 0.8)
                {
                    Log($"done (the far-side door was open from the start: {way.IsOpen})");
                    GetTree().CreateTimer(0.3).Timeout += () => GetTree().Quit();
                    SetPhysicsProcess(false);
                }

                break;
        }

        foreach (var key in Keys)
        {
            SetKey(key, wanted.Contains(key));
        }

        bool Once(double at) => since >= at && since < at + delta;
    }

    private void Next()
    {
        _step++;
        _stepStart = _time;
    }

    private void Log(string text) => GD.Print($"[bomb route {_time:0.0}s] {text}");

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
        var path = $"{_folder}/bomb_{_shotNumber++:00}{name}.png";
        if (after > 0)
        {
            await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
