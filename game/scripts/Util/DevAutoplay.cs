using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper: run with <c>-- --autoplay=&lt;folder&gt;</c> and two bots play the room by pressing the real
/// keyboard keys (P1 on WASD as the lizard, P2 on the arrows as the fox). Script:
/// 1. both join and pick characters, 2. P1 gets spotted by a deer then stands still (camouflage test),
/// 3. P1 changes colour twice, 3b. P2 gets spotted then leaves (deer must forget them) and rejoins, 4. fight until every deer has said thanks (P1 punches, P2 throws bombs),
/// 5. walk to opposite sides to test the camera, 6. P2 holds Delete to leave.
/// Logs what happens and saves a screenshot every 2 seconds into the folder.
/// </summary>
public partial class DevAutoplay : Node
{
    private const string Flag = "--autoplay=";
    private const double MaxSeconds = 120;

    private readonly Bot[] _bots =
    {
        new(0, Key.W, Key.A, Key.S, Key.D, Key.J, Key.K, Key.L, Key.Backspace, isFox: false),
        new(1, Key.Up, Key.Left, Key.Down, Key.Right, Key.Period, Key.Slash, Key.Comma, Key.Delete, isFox: true),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private double _nextShot;
    private double _nextLog;
    private int _shotNumber;
    private Phase _phase = Phase.Join;
    private double _phaseStart;
    private double _spottedAt = -1;
    private double _ditchSpottedAt = -1;

    private enum Phase
    {
        Join,
        Camouflage,
        Colour,
        Ditch,
        Rejoin,
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
        var inPhase = _time - _phaseStart;
        var manager = PlayerManager.Find(this)!;
        var deer = GetTree().GetNodesInGroup(Enemies.Group).OfType<Deer>().ToList();
        var camera = CoopCamera.Find(this)!;
        var wanted = new HashSet<Key>();
        var lizard = manager.Players[0];

        switch (_phase)
        {
            case Phase.Join:
                // P1: join, then pick the first character (lizard). P2: join, go right once (fox), pick.
                PressAt(wanted, _bots[0].Primary, inPhase, 0.2, 0.6);
                PressAt(wanted, _bots[1].Primary, inPhase, 0.8);
                PressAt(wanted, _bots[1].Right, inPhase, 1.1);
                PressAt(wanted, _bots[1].Primary, inPhase, 1.4);
                break;

            case Phase.Camouflage when lizard is not null:
                // Walk at the nearest deer until one targets us, then freeze.
                var targeted = deer.Any(d => d.IsTargeting(lizard));
                if (_spottedAt < 0 && targeted)
                {
                    _spottedAt = _time;
                    GD.Print($"[autoplay {_time:0.0}s] a deer spotted P1 - standing still now");
                }

                if (_spottedAt < 0)
                {
                    var nearest = deer.OrderBy(d => d.GlobalPosition.DistanceTo(lizard.GlobalPosition)).First();
                    _bots[0].Steer(lizard, nearest.HurtCenter, wanted);
                }
                else if (_time - _spottedAt > 0.3 && _time - _spottedAt < 0.35)
                {
                    TakeShot();
                }

                break;

            case Phase.Colour:
                PressAt(wanted, _bots[0].Special, inPhase, 0.2, 0.9);
                break;

            case Phase.Ditch:
                // P2 walks at a deer until it's chased, then holds Delete to drop out.
                if (manager.Players[1] is { } fox)
                {
                    if (_ditchSpottedAt < 0 && deer.Any(d => d.IsTargeting(fox)))
                    {
                        _ditchSpottedAt = _time;
                        GD.Print($"[autoplay {_time:0.0}s] a deer is chasing P2 - P2 leaves now");
                    }

                    if (_ditchSpottedAt < 0)
                    {
                        var nearest = deer.OrderBy(d => d.GlobalPosition.DistanceTo(fox.GlobalPosition)).First();
                        _bots[1].Steer(fox, nearest.HurtCenter, wanted);
                    }
                    else
                    {
                        wanted.Add(_bots[1].Leave);
                    }
                }

                break;

            case Phase.Rejoin:
                // The slot remembers the last pick (fox), so join and confirm.
                PressAt(wanted, _bots[1].Primary, inPhase, 0.2, 0.6);
                break;

            case Phase.Fight:
                foreach (var bot in _bots)
                {
                    if (manager.Players[bot.Slot] is { } hero)
                    {
                        bot.Fight(hero, deer, wanted);
                    }
                }

                break;

            case Phase.Spread:
                wanted.Add(_bots[0].Left);
                wanted.Add(_bots[1].Right);
                break;

            case Phase.Leave:
                wanted.Add(_bots[1].Leave);
                break;
        }

        foreach (var key in _bots.SelectMany(b => b.AllKeys).Distinct())
        {
            SetKey(key, wanted.Contains(key));
        }

        if (_time >= _nextLog)
        {
            _nextLog += 0.5;
            var players = string.Join(" ", manager.Players.Where(p => p is not null).Select(p =>
                $"{p!.Name}({p.DisplayName}):hp{p.Health.Current}{(p.IsDown ? "DOWN" : "")}{(p.IsVisibleToEnemies ? "" : " HIDDEN")}"));
            var animals = string.Join(" ", deer.Select(d => $"{d.CurrentMood.ToString()[0]}{d.Health.Current}"));
            GD.Print($"[autoplay {_time:0.0}s] {_phase} zoom={camera.Zoom.X:0.00} | {players} | deer {animals}");
        }

        if (_time >= _nextShot)
        {
            _nextShot += 2;
            TakeShot();
        }

        AdvancePhase(deer, manager, inPhase);
    }

    private void AdvancePhase(List<Deer> deer, PlayerManager manager, double inPhase)
    {
        var lizard = manager.Players[0];
        var next = _phase switch
        {
            Phase.Join when inPhase > 2 => Phase.Camouflage,
            Phase.Camouflage when _spottedAt >= 0 && _time - _spottedAt > 2.5 => Phase.Colour,
            Phase.Camouflage when inPhase > 12 => Phase.Colour,
            Phase.Colour when inPhase > 1.5 => Phase.Ditch,
            Phase.Ditch when manager.Slots[1].IsFree && inPhase > 0.5 && _ditchSpottedAt >= 0 && _time - _ditchSpottedAt > 1.6 => Phase.Rejoin,
            Phase.Ditch when inPhase > 12 => Phase.Rejoin,
            Phase.Rejoin when inPhase > 1.5 => Phase.Fight,
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

        if (_phase == Phase.Camouflage && lizard is not null)
        {
            var chasing = deer.Count(d => d.IsTargeting(lizard));
            GD.Print($"[autoplay {_time:0.0}s] camouflage test: P1 hidden={!lizard.IsVisibleToEnemies}, deer still chasing P1={chasing}");
        }

        if (_phase == Phase.Colour && lizard?.Abilities.OfType<ColorChangeAbility>().FirstOrDefault() is { } colour)
        {
            GD.Print($"[autoplay {_time:0.0}s] colour test: P1 now resists {colour.Resists?.ToString() ?? "nothing"}");
        }

        if (_phase == Phase.Ditch)
        {
            var lost = deer.Count(d => d.CurrentMood == Deer.Mood.Chasing && !manager.Players.Any(p => p is not null && d.IsTargeting(p)));
            GD.Print($"[autoplay {_time:0.0}s] leave test: P2 left={manager.Slots[1].IsFree}, deer still chasing a player who left={lost}");
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

    /// <summary>Hold <paramref name="key"/> for one short tap at each of the given times (seconds into the phase).</summary>
    private static void PressAt(HashSet<Key> wanted, Key key, double inPhase, params double[] times)
    {
        if (times.Any(t => inPhase >= t && inPhase < t + 0.12))
        {
            wanted.Add(key);
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

        public Bot(int slot, Key up, Key left, Key down, Key right, Key primary, Key secondary, Key special, Key leave, bool isFox)
        {
            Slot = slot;
            Up = up;
            Left = left;
            Down = down;
            Right = right;
            Primary = primary;
            Secondary = secondary;
            Special = special;
            Leave = leave;
            IsFox = isFox;
        }

        public int Slot { get; }
        public Key Up { get; }
        public Key Left { get; }
        public Key Down { get; }
        public Key Right { get; }
        public Key Primary { get; }
        public Key Secondary { get; }
        public Key Special { get; }
        public Key Leave { get; }
        public bool IsFox { get; }

        public Key[] AllKeys => new[] { Up, Left, Down, Right, Primary, Secondary, Special, Leave };

        public void Steer(Hero hero, Vector2 target, HashSet<Key> wanted)
        {
            var offset = target - hero.HurtCenter;
            if (offset.X > 6) wanted.Add(Right);
            if (offset.X < -6) wanted.Add(Left);
            if (offset.Y > 6) wanted.Add(Down);
            if (offset.Y < -6) wanted.Add(Up);
        }

        /// <summary>
        /// Walk to the nearest hostile deer, turn to face it, attack every other frame.
        /// The lizard punches up close; the fox lobs bombs from a few steps away and bites if a deer gets close.
        /// </summary>
        public void Fight(Hero hero, List<Deer> deer, HashSet<Key> wanted)
        {
            var target = deer.Where(d => d.IsHostile).OrderBy(d => d.GlobalPosition.DistanceTo(hero.GlobalPosition)).FirstOrDefault();
            if (target is null || hero.IsDown)
            {
                return;
            }

            var distance = target.HurtCenter.DistanceTo(hero.HurtCenter);
            Key? attack = IsFox
                ? distance < 20 ? Primary : distance is >= 34 and <= 58 ? Secondary : null
                : distance <= 20 ? Primary : null;

            if (attack is null || _attackHeld)
            {
                // Walk closer (or, on alternate frames, tap toward the deer to face it).
                Steer(hero, target.HurtCenter, wanted);
                _attackHeld = false;
            }
            else
            {
                wanted.Add(attack.Value);
                _attackHeld = true;
            }
        }
    }
}
