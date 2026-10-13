using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper: run with <c>-- --autoplay=&lt;folder&gt;</c> and two bots play the room by pressing the real
/// keyboard keys (P1 on WASD, P2 on the arrows). Script:
/// 1. both join, 2. fight until every deer has said thanks (P1 punches, P2 uses the tongue),
/// 3. walk to opposite sides to test the camera, 4. P2 holds Delete to leave.
/// Logs what happens and saves a screenshot every 2 seconds into the folder.
/// </summary>
public partial class DevAutoplay : Node
{
    private const string Flag = "--autoplay=";
    private const double MaxSeconds = 90;

    private readonly Bot[] _bots =
    {
        new(0, Key.W, Key.A, Key.S, Key.D, Key.J, Key.K, Key.Backspace, useTongue: false, joinAt: 0.3),
        new(1, Key.Up, Key.Left, Key.Down, Key.Right, Key.Period, Key.Slash, Key.Delete, useTongue: true, joinAt: 1.0),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private double _nextShot;
    private double _nextLog;
    private int _shotNumber;
    private Phase _phase = Phase.Fight;
    private double _phaseStart;

    private enum Phase
    {
        Fight,
        Spread,
        Leave,
        Done,
    }

    public static void AttachIfRequested(Node parent)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(Flag))
            {
                parent.AddChild(new DevAutoplay { _folder = arg[Flag.Length..], ProcessPhysicsPriority = -20 });
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var manager = PlayerManager.Find(this)!;
        var deer = GetTree().GetNodesInGroup(Enemies.Group).OfType<Deer>().ToList();
        var camera = CoopCamera.Find(this)!;
        var wanted = new HashSet<Key>();

        foreach (var bot in _bots)
        {
            var lizard = manager.Players[bot.Slot];
            if (lizard is null)
            {
                if (_phase == Phase.Fight && _time >= bot.JoinAt && _time < bot.JoinAt + 0.05)
                {
                    wanted.Add(bot.Punch);
                }

                continue;
            }

            switch (_phase)
            {
                case Phase.Fight:
                    bot.Fight(lizard, deer, wanted);
                    break;
                case Phase.Spread:
                    wanted.Add(bot.Slot == 0 ? bot.Left : bot.Right);
                    break;
                case Phase.Leave when bot.Slot == 1:
                    wanted.Add(bot.Leave);
                    break;
            }
        }

        foreach (var key in _bots.SelectMany(b => b.AllKeys).Distinct())
        {
            SetKey(key, wanted.Contains(key));
        }

        if (_time >= _nextLog)
        {
            _nextLog += 0.5;
            var players = string.Join(" ", manager.Players.Where(p => p is not null)
                .Select(p => $"{p!.Name}:hp{p.Health.Current}{(p.IsDown ? "DOWN" : "")}@{p.GlobalPosition.X:0},{p.GlobalPosition.Y:0}"));
            var animals = string.Join(" ", deer.Select(d => $"{d.CurrentMood.ToString()[0]}{d.Health.Current}"));
            GD.Print($"[autoplay {_time:0.0}s] {_phase} zoom={camera.Zoom.X:0.00} cam={camera.Position.X:0},{camera.Position.Y:0} | {players} | deer {animals}");
        }

        if (_time >= _nextShot)
        {
            _nextShot += 2;
            TakeShot();
        }

        AdvancePhase(deer, manager);
    }

    private void AdvancePhase(List<Deer> deer, PlayerManager manager)
    {
        var inPhase = _time - _phaseStart;
        var next = _phase switch
        {
            Phase.Fight when deer.All(d => d.CurrentMood == Deer.Mood.Thankful) && inPhase > 2 => Phase.Spread,
            Phase.Spread when inPhase > 3 => Phase.Leave,
            Phase.Leave when inPhase > 2 => Phase.Done,
            _ => _phase,
        };

        if (_time > MaxSeconds)
        {
            next = Phase.Done;
        }

        if (next == _phase)
        {
            return;
        }

        TakeShot();
        GD.Print($"[autoplay {_time:0.0}s] ---- {next} (players: {manager.Players.Count(p => p is not null)})");
        _phase = next;
        _phaseStart = _time;
        if (_phase == Phase.Done)
        {
            GetTree().Quit();
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

    private sealed class Bot
    {
        private bool _attackHeld;

        public Bot(int slot, Key up, Key left, Key down, Key right, Key punch, Key tongue, Key leave, bool useTongue, double joinAt)
        {
            Slot = slot;
            Up = up;
            Left = left;
            Down = down;
            Right = right;
            Punch = punch;
            Tongue = tongue;
            Leave = leave;
            UseTongue = useTongue;
            JoinAt = joinAt;
        }

        public int Slot { get; }
        public Key Up { get; }
        public Key Left { get; }
        public Key Down { get; }
        public Key Right { get; }
        public Key Punch { get; }
        public Key Tongue { get; }
        public Key Leave { get; }
        public bool UseTongue { get; }
        public double JoinAt { get; }

        public Key[] AllKeys => new[] { Up, Left, Down, Right, Punch, Tongue, Leave };

        /// <summary>Walk to the nearest hostile deer, turn to face it, attack every other frame.</summary>
        public void Fight(Lizard lizard, List<Deer> deer, HashSet<Key> wanted)
        {
            var target = deer.Where(d => d.IsHostile).OrderBy(d => d.GlobalPosition.DistanceTo(lizard.GlobalPosition)).FirstOrDefault();
            if (target is null || lizard.IsDown)
            {
                return;
            }

            var offset = target.HurtCenter - lizard.HurtCenter;
            var steer = new List<Key>();
            if (offset.X > 6) steer.Add(Right);
            if (offset.X < -6) steer.Add(Left);
            if (offset.Y > 6) steer.Add(Down);
            if (offset.Y < -6) steer.Add(Up);

            if (offset.Length() > (UseTongue ? 34 : 20) || _attackHeld)
            {
                // Walk closer, or (on alternate frames) tap toward the deer to face it.
                wanted.UnionWith(steer);
                _attackHeld = false;
            }
            else
            {
                wanted.Add(UseTongue ? Tongue : Punch);
                _attackHeld = true;
            }
        }
    }
}
