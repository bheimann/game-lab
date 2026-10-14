using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper: run with <c>-- --autoplay=&lt;folder&gt;</c> and two bots play the room by pressing the real
/// keyboard keys (P1 on WASD as the lizard, P2 on the arrows as the fox). Script:
/// 1. both join and pick characters, 2. P1 gets spotted by a deer then stands still (camouflage test),
/// 3. P1 changes colour twice, 3b. P2 gets spotted then leaves (deer must forget them) and rejoins,
/// 3c. P1 gets into a fight, opens the ring menu and puts on armor while P2 keeps fighting and switches to proximity bombs, 4. fight
/// (P2 switches bomb type every few seconds, P1 eats a herb) until every deer has said thanks (P1 punches, P2 throws bombs),
/// 5. walk to opposite sides to test the camera, 6. P2 holds Delete to leave.
/// Logs what happens and saves a screenshot every 2 seconds into the folder.
/// Add <c>--autoplay-pick=2,3</c> to make the bots pick other characters (indexes into
/// <see cref="PlayerManager.Characters"/>); the lizard-only tests then just log "nothing".
/// Add <c>--autoplay-classes=Illusionist,Weapon Master</c> to paint the bots into those classes once they've joined;
/// in the fight they then also press Y every so often and flip through their choices (spells, weapons, arrows).
/// </summary>
public partial class DevAutoplay : Node
{
    private const string Flag = "--autoplay=";
    private const string PickFlag = "--autoplay-pick=";
    private const string ClassesFlag = "--autoplay-classes=";
    private const double MaxSeconds = 120;

    private readonly AutoplayBot[] _bots =
    {
        new(0, Key.W, Key.A, Key.S, Key.D, Key.J, Key.K, Key.L, Key.Backspace, Key.I, Key.Q, Key.E, Key.Tab, Key.Shift, isFox: false),
        new(1, Key.Up, Key.Left, Key.Down, Key.Right, Key.Period, Key.Slash, Key.Comma, Key.Delete, Key.M, Key.Semicolon, Key.Apostrophe, Key.Enter, Key.Ctrl, isFox: true),
    };

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private int[] _picks = { 0, 1 };
    private string[] _classes = System.Array.Empty<string>();
    private int _abilityStep = -1;
    private double _time;
    private double _nextShot;
    private double _nextLog;
    private int _shotNumber;
    private Phase _phase = Phase.Join;
    private double _phaseStart;
    private double _spottedAt = -1;
    private double _ditchSpottedAt = -1;
    private double _gearSpottedAt = -1;
    private readonly HashSet<ulong> _bombsSeen = new();
    private readonly Dictionary<BombKind, int> _bombsThrown = new();

    private enum Phase
    {
        Join,
        Abilities,
        Camouflage,
        Colour,
        Ditch,
        Rejoin,
        Gear,
        Fight,
        Spread,
        Leave,
        Done,
    }

    public static void AttachIfRequested(Node parent)
    {
        var args = OS.GetCmdlineUserArgs();
        var picks = args.Where(a => a.StartsWith(PickFlag)).Select(a => a[PickFlag.Length..].Split(',').Select(int.Parse).ToArray()).FirstOrDefault();
        var classes = args.Where(a => a.StartsWith(ClassesFlag)).Select(a => a[ClassesFlag.Length..].Split(',')).FirstOrDefault();
        foreach (var arg in args)
        {
            if (arg.StartsWith(Flag))
            {
                // Dungeons get their own script; the test room gets this one.
                var folder = arg[Flag.Length..];
                parent.AddChild(Dungeons.Dungeon.Find(parent) is not null
                    ? DevDungeonAutoplay.Create(folder)
                    : new DevAutoplay { _folder = folder, _picks = picks ?? new[] { 0, 1 }, _classes = classes ?? System.Array.Empty<string>(), ProcessPhysicsPriority = -20 });
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
                PressAt(wanted, _bots[0].Primary, inPhase, 0.2, 0.65);
                Choose(wanted, _bots[0], _picks[0], inPhase, 0.35);
                PressAt(wanted, _bots[1].Primary, inPhase, 0.8);
                Choose(wanted, _bots[1], _picks[1], inPhase, 1.0);
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

            case Phase.Gear when lizard is not null:
                // P1 walks at a deer until it's chased, then (still being chased) opens the menu, walks a bit,
                // goes to the Armor ring, picks the next piece (shadow cloak) and closes the menu by picking.
                if (_gearSpottedAt < 0 && deer.Any(d => d.IsTargeting(lizard)))
                {
                    _gearSpottedAt = _time;
                    GD.Print($"[autoplay {_time:0.0}s] a deer is chasing P1 - opening the menu");
                }

                if (_gearSpottedAt < 0)
                {
                    var nearest = deer.Where(d => d.IsHostile).OrderBy(d => d.GlobalPosition.DistanceTo(lizard.GlobalPosition)).First();
                    _bots[0].Steer(lizard, nearest.HurtCenter, wanted);
                }
                else
                {
                    var since = _time - _gearSpottedAt;
                    PressAt(wanted, _bots[0].Menu, since, 0.1);
                    PressAt(wanted, _bots[0].Special, since, 0.5);
                    PressAt(wanted, _bots[0].Next, since, 0.8);
                    PressAt(wanted, _bots[0].Primary, since, 1.2);
                    if (since is > 0.3 and < 1.0)
                    {
                        wanted.Add(_bots[0].Left);
                    }

                    if (since is > 0.75 and < 0.77)
                    {
                        GD.Print($"[autoplay {_time:0.0}s] menu test: P1 menu open={lizard.IsMenuOpen}, in a fight={lizard.IsInFight}, walking speed={lizard.Velocity.Length():0} (normal {lizard.Speed:0})");
                        TakeShot();
                    }
                }

                if (manager.Players[1] is { } bomber && _gearSpottedAt >= 0)
                {
                    // Waits until P1 is being chased (otherwise its bombs may put every deer to sleep first).
                    _bots[1].Fight(bomber, deer, wanted);
                    PressAt(wanted, _bots[1].Previous, _time - _gearSpottedAt, 0.3);
                }

                break;

            case Phase.Abilities:
                AbilityTest(manager, deer, inPhase);
                break;

            case Phase.Fight:
                foreach (var bot in _bots)
                {
                    if (manager.Players[bot.Slot] is { } hero)
                    {
                        bot.Fight(hero, deer, wanted);
                    }
                }

                // P2 tries every bomb type; P1 eats a herb.
                if (inPhase % 2 is > 1.7 and < 1.82)
                {
                    wanted.Add(_bots[1].Next);
                }

                // Painted bots also use Y now and then, and P1 flips through its choices too.
                if (_classes.Length > 0)
                {
                    if (inPhase % 1.5 is > 0.7 and < 0.82)
                    {
                        wanted.Add(_bots[0].Special);
                        wanted.Add(_bots[1].Special);
                    }

                    if (inPhase % 4 is > 3.1 and < 3.22)
                    {
                        wanted.Add(_bots[0].Next);
                    }
                }

                PressAt(wanted, _bots[0].Item, inPhase, 1.0);
                break;

            case Phase.Spread:
                wanted.Add(_bots[0].Left);
                wanted.Add(_bots[1].Right);
                break;

            case Phase.Leave:
                wanted.Add(_bots[1].Leave);
                break;
        }

        foreach (var bomb in manager.ActorsParent?.GetChildren().OfType<Bomb>() ?? Enumerable.Empty<Bomb>())
        {
            if (_bombsSeen.Add(bomb.GetInstanceId()))
            {
                _bombsThrown[bomb.Kind] = _bombsThrown.GetValueOrDefault(bomb.Kind) + 1;
            }
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
            Phase.Join when inPhase > 2 => _classes.Length > 0 ? Phase.Abilities : Phase.Camouflage,
            Phase.Abilities when inPhase > 9 => Phase.Camouflage,
            Phase.Camouflage when _spottedAt >= 0 && _time - _spottedAt > 2.5 => Phase.Colour,
            Phase.Camouflage when inPhase > 12 => Phase.Colour,
            Phase.Colour when inPhase > 1.5 => Phase.Ditch,
            Phase.Ditch when manager.Slots[1].IsFree && inPhase > 0.5 && _ditchSpottedAt >= 0 && _time - _ditchSpottedAt > 1.6 => Phase.Rejoin,
            Phase.Ditch when inPhase > 12 => Phase.Rejoin,
            Phase.Rejoin when inPhase > 1.5 => Phase.Gear,
            Phase.Gear when _gearSpottedAt >= 0 && _time - _gearSpottedAt > 1.6 => Phase.Fight,
            Phase.Gear when inPhase > 12 => Phase.Fight,
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

        if (_phase == Phase.Gear && lizard is not null)
        {
            var bomber = manager.Players[1]?.Abilities.OfType<BombAbility>().FirstOrDefault();
            GD.Print($"[autoplay {_time:0.0}s] gear test: P1 menu open={lizard.IsMenuOpen}, armor={lizard.Armor?.Name ?? "none"}; P2 bomb={bomber?.Kind.ToString() ?? "?"}");
        }

        if (_phase == Phase.Fight)
        {
            var thrown = string.Join(", ", _bombsThrown.Select(kv => $"{kv.Key}x{kv.Value}"));
            GD.Print($"[autoplay {_time:0.0}s] fight test: bombs thrown: {thrown}; P1 herbs left={lizard?.Herbs}");
        }

        if (_phase == Phase.Ditch)
        {
            var lost = deer.Count(d => d.CurrentMood == Deer.Mood.Chasing && !manager.Players.Any(p => p is not null && d.IsTargeting(p)));
            GD.Print($"[autoplay {_time:0.0}s] leave test: P2 left={manager.Slots[1].IsFree}, deer still chasing a player who left={lost}");
        }

        if (_phase is Phase.Join or Phase.Rejoin)
        {
            for (var i = 0; i < _classes.Length && i < manager.Players.Count; i++)
            {
                if (manager.Players[i] is { } hero && _classes[i].Trim() != "")
                {
                    hero.Paint(_classes[i].Trim());
                    GD.Print($"[autoplay {_time:0.0}s] P{i + 1} painted as a {hero.CurrentClass}");
                }
            }
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

    /// <summary>
    /// With <c>--autoplay-classes</c>: a scripted check of the painted classes' moves. Deer are put right next to
    /// the heroes and each move is used directly, once, at a set time; the log says what happened.
    /// (Written for P1 = Illusionist, P2 = Weapon Master.)
    /// </summary>
    private void AbilityTest(PlayerManager manager, List<Deer> deer, double inPhase)
    {
        var step = (int)(inPhase * 2); // a step every half second
        if (step == _abilityStep || deer.Count < 5)
        {
            return;
        }

        _abilityStep = step;
        var p1 = manager.Players[0];
        var p2 = manager.Players[1];
        void Place(Deer d, Hero h, float distance) => d.GlobalPosition = h.GlobalPosition + h.Facing * distance;
        Ability A(Hero h, string name) => h.GetNode<Ability>(name);

        if (p2?.CurrentClass == "Weapon Master")
        {
            var weapons = (WeaponSelectorAbility)A(p2, "Weapons");
            switch (step)
            {
                case 1: weapons.Select(0); Place(deer[0], p2, 16); A(p2, "Shield").TryUse(); p2.TakeHit(3, deer[0].GlobalPosition); break;
                case 3: A(p2, "Sword").TryUse(); break;
                case 5: weapons.Select(1); Place(deer[1], p2, 50); A(p2, "JavelinThrow").TryUse(); break;
                case 7: weapons.Select(2); Place(deer[2], p2, 14); A(p2, "FlailSpin").TryUse(); break;
                case 9: weapons.Select(3); A(p2, "ArrowType").TryUse(); break; // normal -> arcane
                case 11:
                    var bow = (GunAbility)A(p2, "Bow");
                    bow.Select(6); // explosive
                    Place(deer[3], p2, 60);
                    bow.TryUse();
                    break;
                case 13: ((GunAbility)A(p2, "Bow")).Select(5); Place(deer[3], p2, 60); A(p2, "Bow").TryUse(); break; // poison
            }
        }

        if (p1?.CurrentClass == "Illusionist")
        {
            var spells = (IllusionAbility)A(p1, "Illusion");
            switch (step)
            {
                case 2: spells.Select(2); Place(deer[4], p1, 20); deer[4].GlobalPosition += new Vector2(0, 10); spells.TryUse(); break;
                case 8: spells.Select(0); spells.TryUse(); break;
                case 12: spells.Select(1); spells.TryUse(); break;
            }

            if (step % 2 == 0)
            {
                GD.Print($"[autoplay {_time:0.0}s] ability test: P1 energy {spells.Energy:0.0}, charmed deer {deer.Count(d => d.IsCharmed)}, decoys out {GetTree().GetNodesInGroup(Enemies.LureGroup).Count}");
            }
        }

        if (step % 4 == 0)
        {
            TakeShot();
        }
    }

    /// <summary>On the character picker, step from the first character to <paramref name="index"/> (the short way round).</summary>
    private static void Choose(HashSet<Key> wanted, AutoplayBot bot, int index, double inPhase, double at)
    {
        var count = PlayerManager.Characters.Length;
        var steps = index <= count / 2 ? index : index - count;
        for (var i = 0; i < Mathf.Abs(steps); i++)
        {
            PressAt(wanted, steps > 0 ? bot.Right : bot.Left, inPhase, at + i * 0.15);
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
}
