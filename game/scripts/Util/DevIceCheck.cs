using System.Linq;
using Godot;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the Ice Hall's slide puzzle: <c>-- --room=IceHall --autoplay-ice=&lt;folder&gt;</c>. P1 joins (the fox),
/// stands on the floor just above the ice, and plays the solution with the real keys (down, right, down, right: found by
/// a little solver); after each slide the log says where it stopped, and at the end whether the key was picked up.
/// </summary>
public partial class DevIceCheck : Node
{
    public const string Flag = "--autoplay-ice=";

    private static readonly Key[] Moves = { Key.S, Key.D, Key.S, Key.D };

    private string _folder = "";
    private double _time;
    private int _move = -1;
    private double _moveStart;
    private Key? _held;

    public static DevIceCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var manager = PlayerManager.Find(this)!;
        var room = Dungeon.Find(this)!.CurrentRoom;
        var hero = manager.Players[0];
        if (hero is null)
        {
            if (_time > 0.2)
            {
                manager.JoinForDev(InputDevice.KeyboardLeft, 1, "Bomb Master");
            }

            return;
        }

        var ice = room.GetNode<IceFloor>("Ice");
        if (_move < 0)
        {
            // On the floor just above column 7 of the ice.
            hero.Teleport(ice.GlobalPosition + new Vector2(7 * IceFloor.Cell + 8, -8));
            _move = 0;
            _moveStart = _time;
            return;
        }

        var since = _time - _moveStart;
        if (_move < Moves.Length)
        {
            Press(since < 0.15 ? Moves[_move] : null);
            if (since > 0.08 && since < 0.1)
            {
                Shot($"_{_move}");
            }

            if (since > 1.2)
            {
                var local = hero.GlobalPosition - ice.GlobalPosition;
                GD.Print($"[ice {_time:0.0}s] after {Moves[_move]}: cell ({Mathf.FloorToInt(local.X / IceFloor.Cell)}, {Mathf.FloorToInt(local.Y / IceFloor.Cell)}), keys={Dungeon.Find(this)!.Keys}");
                _move++;
                _moveStart = _time;
            }
        }
        else
        {
            Press(null);
            GD.Print($"[ice] done: key picked up={Dungeon.Find(this)!.Keys > 0 || !room.GetNode<Node2D>("IceKey").Visible}");
            GetTree().CreateTimer(0.3).Timeout += () => GetTree().Quit();
            SetPhysicsProcess(false);
        }
    }

    private void Press(Key? key)
    {
        if (_held == key)
        {
            return;
        }

        if (_held is { } old)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = old, Keycode = old, Pressed = false });
        }

        if (key is { } k)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = k, Keycode = k, Pressed = true });
        }

        _held = key;
    }

    private async void Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_folder}/ice{name}.png");
    }
}
