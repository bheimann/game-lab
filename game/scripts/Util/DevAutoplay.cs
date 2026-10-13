using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper: run with <c>-- --autoplay=&lt;folder&gt;</c> and a bot plays the room by pressing the real
/// keyboard keys (walks to the nearest hostile deer; uses the tongue on the first deer and punches the rest). It logs what happens, saves a
/// screenshot every 2 seconds into the folder, and quits once every deer has said thanks.
/// </summary>
public partial class DevAutoplay : Node
{
    private const string Flag = "--autoplay=";
    private const double MaxSeconds = 60;

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private double _nextShot = 0.5;
    private int _shotNumber;
    private double _finishedAt = -1;
    private bool _attackHeld;
    private string _lastLog = "";

    public static void AttachIfRequested(Node parent)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(Flag))
            {
                parent.AddChild(new DevAutoplay { _folder = arg[Flag.Length..] });
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var lizard = GetTree().GetNodesInGroup(Lizard.Group).OfType<Lizard>().First();
        var deer = GetTree().GetNodesInGroup(Enemies.Group).OfType<Deer>().ToList();

        Log($"lizard hp={lizard.Health.Current}{(lizard.IsDown ? " DOWN" : "")} fight={lizard.IsInFight} | " +
            string.Join(" ", deer.Select(d => $"{d.Name}:{d.CurrentMood}/{d.Health.Current}")));

        var target = deer.Where(d => d.IsHostile).OrderBy(d => d.GlobalPosition.DistanceTo(lizard.GlobalPosition)).FirstOrDefault();
        var wanted = new HashSet<Key>();
        var attack = false;
        var useTongue = false;
        if (target is not null && !lizard.IsDown)
        {
            // Aim for the deer's side, level with its body.
            var offset = target.HurtCenter - lizard.HurtCenter;
            useTongue = target.Name == "Deer1";
            var steer = new HashSet<Key>();
            if (offset.X > 6) steer.Add(Key.D);
            if (offset.X < -6) steer.Add(Key.A);
            if (offset.Y > 6) steer.Add(Key.S);
            if (offset.Y < -6) steer.Add(Key.W);

            if (offset.Length() > (useTongue ? 34 : 20))
            {
                wanted = steer;
            }
            else if (_attackHeld)
            {
                // Aim frame: tap toward the deer so the lizard faces it, like a player would.
                wanted = steer;
            }
            else
            {
                attack = true;
            }
        }

        SetKeys(wanted);
        SetKey(Key.J, attack && !useTongue);
        SetKey(Key.K, attack && useTongue);
        _attackHeld = attack;

        if (_finishedAt < 0 && deer.All(d => d.CurrentMood == Deer.Mood.Thankful))
        {
            _finishedAt = _time;
            GD.Print($"[autoplay {_time:0.0}s] every deer is thankful");
        }

        if (_time >= _nextShot)
        {
            _nextShot += 2;
            TakeShot();
        }

        if ((_finishedAt >= 0 && _time > _finishedAt + 3) || _time > MaxSeconds)
        {
            TakeShot();
            GD.Print($"[autoplay {_time:0.0}s] done");
            GetTree().Quit();
        }
    }

    private void Log(string line)
    {
        if (line != _lastLog)
        {
            GD.Print($"[autoplay {_time:0.0}s] {line}");
            _lastLog = line;
        }
    }

    private void SetKeys(HashSet<Key> wanted)
    {
        foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D })
        {
            SetKey(key, wanted.Contains(key));
        }
    }

    private void SetKey(Key key, bool down)
    {
        if (down == _held.Contains(key))
        {
            return;
        }

        if (down) _held.Add(key); else _held.Remove(key);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
    }

    private async void TakeShot()
    {
        var path = $"{_folder}/shot_{_shotNumber++:00}.png";
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
