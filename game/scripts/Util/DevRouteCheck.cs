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
/// Dev helper for the routes that let a hero play a dungeon alone: <c>-- --room=PuzzleRoom --autoplay-routes=&lt;folder&gt;</c>
/// (the Old Burrow's Pit of Two Levers) or <c>--room=MossBog</c> (Mossmire Keep's Bog Hall). P1 joins as the lizard
/// (Chameleon) on WASD and P2 as the moose (Tinkerer, the class with no summons) on the arrows. Checks that: the
/// lizard's punch and tongue don't knock the dead tree over, the moose can't walk into the pit, the moose's antler bash
/// knocks the tree over into a log, and the moose walks across it. In the Pit of Two Levers also: the lizard can't
/// squeeze past the ring stone, its tongue drags the stone out, it walks through the gap and flips the lever behind
/// the rocks, and tongues the one across the pit, so the bridge comes out without any bombs.
/// Logs each result and saves screenshots into the folder.
/// </summary>
public partial class DevRouteCheck : Node
{
    public const string Flag = "--autoplay-routes=";

    private static readonly Key[] Keys = { Key.W, Key.A, Key.S, Key.D, Key.Up, Key.Left, Key.Down, Key.Right };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private int _step;
    private double _stepStart;
    private int _shotNumber;
    private bool _punched;

    public static DevRouteCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var wanted = new HashSet<Key>();
        var manager = PlayerManager.Find(this)!;
        var room = Dungeon.Find(this)!.CurrentRoom;
        var since = _time - _stepStart;
        var burrow = room.Name == "PuzzleRoom";
        if (!burrow && room.Name != "MossBog")
        {
            GD.Print($"[routes] nothing to check in {room.Name}: add --room=PuzzleRoom or --room=MossBog");
            SetPhysicsProcess(false);
            GetTree().Quit();
            return;
        }

        Vector2 At(Vector2 p) => room.GlobalPosition + p;
        Vector2 Where(Hero hero) => hero.GlobalPosition - room.GlobalPosition;
        var lizard = manager.Players[0];
        var moose = manager.Players[1];
        var tree = room.GetNode<FallingTree>(burrow ? "DeadTree" : "BogTree");
        var log = room.GetNode<Pit>(burrow ? "LogPit" : "BogLog");
        var treeFoot = tree.Position + new Vector2(0, 14);

        switch (_step)
        {
            case 0:
                if (since > 0.2 && lizard is null)
                {
                    manager.JoinForDev(InputDevice.KeyboardLeft, 0, "Chameleon");
                    manager.JoinForDev(InputDevice.KeyboardRight, 2, "Tinkerer");
                }

                if (lizard is not null && moose is not null && since > 0.6)
                {
                    Log($"joined: {lizard.DisplayName} ({lizard.CurrentClass}), {moose.DisplayName} ({moose.CurrentClass}, strong={moose.Strong})");
                    foreach (var enemy in room.Enemies.OfType<IHittable>())
                    {
                        enemy.TakeHit(999, enemy.HurtCenter); // the bog's guards would get in the way
                    }

                    lizard.Teleport(At(treeFoot));
                    lizard.Face(Vector2.Up);
                    moose.Teleport(At(treeFoot + new Vector2(-30, 20)));
                    Next();
                }

                break;

            case 1:
                // The lizard punches the tree and flicks its tongue at it: nothing happens.
                if (Once(0.6)) lizard!.GetNode<Ability>("Punch").TryUse();
                if (Once(1.2)) lizard!.GetNode<Ability>("Tongue").TryUse();
                if (since > 2)
                {
                    Log($"lizard punched and tongued the tree: fallen={tree.IsOn} (want False)");
                    lizard!.Teleport(At(treeFoot + new Vector2(30, 20)));
                    moose!.Teleport(At(treeFoot));
                    moose.Face(Vector2.Up);
                    Next();
                }

                break;

            case 2:
                // The moose walks into the pit: blocked.
                if (since > 0.3) wanted.Add(Key.Up);
                if (since > 1.5)
                {
                    Log($"moose walked into the pit: at {Where(moose!):0} (pit edge at y={log.Position.Y + log.Size.Y:0})");
                    Next();
                }

                break;

            case 3:
                if (Once(0.2))
                {
                    moose!.Face(Vector2.Up);
                    moose.GetNode<Ability>("AntlerBash").TryUse();
                }

                if (Once(0.4)) Shot(0, "_falling");
                if (since > 1.2)
                {
                    Log($"moose bashed the tree: fallen={tree.IsOn}, log across={log.HasBridge}");
                    Shot(0, "_log");
                    Next();
                }

                break;

            case 4:
                // Over the log.
                wanted.Add(Key.Up);
                if (Where(moose!).Y < log.Position.Y - 4 || since > 2.5)
                {
                    Log($"moose walked over the log: at {Where(moose!):0}, across={Where(moose!).Y < log.Position.Y} (want True)");
                    Shot(0.2, "_across");
                    Next();
                    if (!burrow)
                    {
                        _step = 20;
                    }
                }

                break;

            case 5:
                // Lizard: the gap under the ring stone is too small to squeeze through.
                if (since < 0.3)
                {
                    lizard!.Teleport(At(new Vector2(84, 220)));
                    break;
                }

                wanted.Add(Key.A);
                if (since > 1.5)
                {
                    Log($"lizard tried to squeeze past the ring stone: at {Where(lizard!):0} (want x > 64)");
                    lizard!.Teleport(At(new Vector2(98, 212)));
                    lizard.Face(Vector2.Left);
                    Next();
                }

                break;

            case 6:
                var stone = room.GetNode<RingStone>("RingStone");
                if (Once(0.4)) lizard!.GetNode<Ability>("Tongue").TryUse();
                if (since > 1.4)
                {
                    Log($"lizard tongued the ring stone: stone at {stone.Position:0} (started at (57, 216))");
                    Shot(0, "_pulled");
                    // Just right of the gap (or of the stone, if it didn't move), then walk left.
                    lizard!.Teleport(At(new Vector2(stone.Position.X > 80 ? stone.Position.X - 15 : 72, 216)));
                    Next();
                }

                break;

            case 7:
                // Through the gap, up to the lever behind the rocks.
                var behind = room.GetNode<LeverSwitch>("LeverBehindRocks");
                if (Where(lizard!).X > 38 && since < 2.5)
                {
                    wanted.Add(Key.A);
                    break;
                }

                if (!_punched)
                {
                    _punched = true;
                    Log($"lizard walked through the gap: at {Where(lizard!):0}");
                    lizard!.Face(Vector2.Left);
                    lizard.GetNode<Ability>("Punch").TryUse();
                }

                if (since > 3.2 || behind.IsOn)
                {
                    Log($"lizard punched the lever behind the rocks: on={behind.IsOn}");
                    lizard!.Teleport(At(new Vector2(240, 140)));
                    lizard.Face(Vector2.Up);
                    Next();
                }

                break;

            case 8:
                if (Once(0.6)) lizard!.GetNode<Ability>("Tongue").TryUse();
                if (since > 1.8)
                {
                    var bridge = room.GetNode<Pit>("Bridge");
                    var rocks = room.GetChildren().OfType<Boulder>().Count();
                    Log($"lizard tongued the lever across the pit: across on={room.GetNode<LeverSwitch>("LeverAcrossPit").IsOn}, bridge={bridge.HasBridge} (cracked rocks still standing: {rocks})");
                    Shot(0.3, "_bridge");
                    _step = 20;
                    _stepStart = _time;
                }

                break;

            case 20:
                if (since > 0.6)
                {
                    Log("done");
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

    private void Log(string text) => GD.Print($"[routes {_time:0.0}s] {text}");

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
        var path = $"{_folder}/routes_{_shotNumber++:00}{name}.png";
        if (after > 0)
        {
            await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
