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
    private Deer? _cloneTarget;
    private int _abilityHp;
    private int _abilityHp2;
    private Vector2 _abilityPos;
    private int _rollerStage;
    private Deer? _summonTarget;
    private int _rollerNextStep;
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
            if (arg.StartsWith(DevGrappleCheck.Flag))
            {
                parent.AddChild(DevGrappleCheck.Create(arg[DevGrappleCheck.Flag.Length..]));
            }

            if (arg.StartsWith(DevTourCheck.Flag))
            {
                parent.AddChild(DevTourCheck.Create(arg[DevTourCheck.Flag.Length..]));
            }

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
                    var nearest = deer.Where(d => d.IsHostile).OrderBy(d => d.GlobalPosition.DistanceTo(lizard.GlobalPosition)).FirstOrDefault();
                    if (nearest is not null)
                    {
                        _bots[0].Steer(lizard, nearest.HurtCenter, wanted);
                    }
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
            Phase.Abilities when inPhase > 13 => Phase.Camouflage,
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
    /// <summary>
    /// The moose's Summoner class: summon the wolf (and check the wait before the next one), add squirrels, bees and
    /// an owl, watch them fight the deer the moose hit, check that a fifth summon sends the oldest away, switch the
    /// big summon with B's ability and stampede a deer, then check the totem heals and the treant draws the deer.
    /// </summary>
    private void SummonerTest(Hero p1, List<Deer> deer, int step, System.Action<Deer, Hero, float> place, System.Func<Hero, string, Ability> ability)
    {
        var light = (SummonAbility)ability(p1, "LightSummon");
        var heavy = (SummonAbility)ability(p1, "HeavySummon");
        void Log(string text) => GD.Print($"[autoplay {_time:0.0}s] summoner test: {text}");
        string Out() => string.Join(", ", Summon.Of(p1).Select(s => $"{s.Kind}"));
        void Add(SummonKind kind, Vector2 offset)
        {
            var summon = new Summon { Caster = p1, Kind = kind };
            p1.GetParent().AddChild(summon);
            summon.GlobalPosition = p1.GlobalPosition + offset;
        }

        // Deer still up for a fight (the summons put the others to sleep quickly).
        Deer Fresh(int skip = 0) => deer.Where(d => d.IsHostile).Skip(skip).FirstOrDefault() ?? deer[0];

        switch (step)
        {
            case 1:
                p1.Face(Vector2.Right);
                _summonTarget = Fresh();
                place(_summonTarget, p1, 22);
                ability(p1, "AntlerBash").TryUse();
                break;
            case 2:
                light.Select(1);
                Log($"spirit wolf: started={light.TryUse()}, right again (should wait)={light.TryUse()}");
                Add(SummonKind.Squirrels, new Vector2(0, 14));
                Add(SummonKind.Bees, new Vector2(-10, 0));
                Add(SummonKind.Owl, new Vector2(0, -10));
                _abilityHp = _summonTarget!.Health.Current;
                TakeShot(0.3, "_summons");
                break;
            case 6:
                Log($"out: {Out()}; the deer the moose hit: hp {_abilityHp} -> {_summonTarget!.Health.Current}");
                break;
            case 11:
                light.Select(4);
                Log($"wisps (a 5th): started={light.TryUse()}, out now: {Out()}");
                TakeShot(0.5, "_wisps");
                break;
            case 12:
                ability(p1, "HeavySwitch").TryUse();
                p1.Face(Vector2.Right);
                _summonTarget = Fresh();
                place(_summonTarget, p1, 50);
                _abilityHp2 = _summonTarget.Health.Current;
                Log($"big summon switched to {heavy.Options[heavy.Selected].Name}: started={heavy.TryUse()}");
                TakeShot(0.4, "_stampede");
                break;
            case 15:
                Log($"stampede: deer hp {_abilityHp2} -> {_summonTarget!.Health.Current}");
                Add(SummonKind.Totem, new Vector2(10, 4));
                p1.Health.Damage(4);
                _abilityHp = p1.Health.Current;
                break;
            case 20:
                Log($"totem: moose hp {_abilityHp} -> {p1.Health.Current} (max {p1.Health.Max})");
                foreach (var d in deer.Where(d => d.IsHostile))
                {
                    d.Respawn();
                }

                Add(SummonKind.Treant, new Vector2(24, 0));
                place(Fresh(), p1, 60);
                TakeShot(0.3, "_treant");
                break;
            case 25:
                var treant = Summon.Of(p1).FirstOrDefault(s => s.Kind == SummonKind.Treant);
                Log($"treant hp {treant?.HealthLeft}/30, deer chasing the moose={deer.Count(d => d.IsTargeting(p1))}, out: {Out()}");
                foreach (var summon in Summon.Of(p1))
                {
                    summon.Dismiss(); // out of the way of the later tests
                }

                break;
        }
    }

    /// <summary>
    /// The beetle's Roller class, one check at a time; each waits until the beetle isn't busy or flashing from a
    /// deer's kick (a hit cancels a roll): roll into a deer, get hit inside the shell, get hit with the tough shell,
    /// roll into the top wall and bounce.
    /// </summary>
    private void RollerTest(Hero p1, List<Deer> deer, int step, System.Action<Deer, Hero, float> place, System.Func<Hero, string, Ability> ability)
    {
        var ready = p1.CanBeHit && !p1.Abilities.Any(a => a.IsActive) && step >= _rollerNextStep;
        void Log(string text) => GD.Print($"[autoplay {_time:0.0}s] roller test: {text}");
        switch (_rollerStage)
        {
            case 0 when ready:
                p1.Face(Vector2.Left);
                place(deer[0], p1, 40);
                _abilityHp = deer[0].Health.Current;
                ability(p1, "Roll").TryUse();
                TakeShot(0.15, "_roll");
                break;
            case 1:
                Log($"roll hit the deer: hp {_abilityHp} -> {deer[0].Health.Current}");
                break;
            case 2 when ready:
                _abilityHp = p1.Health.Current;
                ability(p1, "ShellShield").TryUse();
                TakeShot(0.1, "_shell");
                p1.TakeHit(3, p1.GlobalPosition + Vector2.Right * 10);
                Log($"hit (3) inside the shell: hp {_abilityHp} -> {p1.Health.Current}");
                break;
            case 3 when ready:
                _abilityHp = p1.Health.Current;
                p1.TakeHit(5, p1.GlobalPosition + Vector2.Right * 10);
                Log($"hit (5) with the tough shell: hp {_abilityHp} -> {p1.Health.Current}");
                break;
            case 4 when ready:
                // Deer close by would kick the beetle out of its roll.
                foreach (var near in deer.Where(d => d.GlobalPosition.DistanceTo(p1.GlobalPosition) < 60))
                {
                    near.GlobalPosition = new Vector2(470, 240);
                }

                p1.Face(Vector2.Up);
                _abilityPos = p1.GlobalPosition;
                Log($"rolling up: started={ability(p1, "Roll").TryUse()}");
                break;
            case 5:
            case 6:
                Log($"rolling up from y={_abilityPos.Y:0}, now y={p1.GlobalPosition.Y:0}, facing {p1.Facing}");
                break;
            default:
                return;
        }

        _rollerStage++;
        _rollerNextStep = step + 1;
    }

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
                case 8: spells.Select(1); spells.TryUse(); break;
                case 17:
                    // Once the charm has worn off, punch a fresh deer: the clones should go after it.
                    _cloneTarget = deer.First(d => d.IsHostile && !d.IsCharmed);
                    _cloneTarget.Respawn();
                    Place(_cloneTarget, p1, 14);
                    A(p1, "Punch").TryUse();
                    break;
                case 18: spells.Select(0); spells.TryUse(); _cloneTarget!.GlobalPosition = p1.GlobalPosition + new Vector2(70, 0); break;
            }

            if (step % 2 == 0)
            {
                GD.Print($"[autoplay {_time:0.0}s] ability test: P1 energy {spells.Energy:0.0}, charmed deer {deer.Count(d => d.IsCharmed)}, decoys out {GetTree().GetNodesInGroup(Enemies.LureGroup).Count}, P1 last hit {(p1.LastHitEnemy as Node)?.Name ?? "nothing"} (hp {(p1.LastHitEnemy as Deer)?.Health.Current})");
            }
        }

        // Roller (P1, on the left) tests to its left and upward; Grappler (P2, on the right) to its right and downward.
        if (p1?.CurrentClass == "Summoner")
        {
            SummonerTest(p1, deer, step, Place, A);
        }

        if (p1?.CurrentClass == "Roller")
        {
            RollerTest(p1, deer, step, Place, A);
        }

        if (p2?.CurrentClass == "Grappler")
        {
            var feathers = (GunAbility)A(p2, "Feathers");
            switch (step)
            {
                case 1: p2.Face(Vector2.Right); feathers.Select(2); Place(deer[2], p2, 50); _abilityHp2 = deer[2].Health.Current; feathers.TryUse(); TakeShot(0.12, "_feathers"); break;
                case 2: GD.Print($"[autoplay {_time:0.0}s] grappler test: {feathers.Options[feathers.Selected].Name} feathers: deer hp {_abilityHp2} -> {deer[2].Health.Current}"); break;
                case 3: deer[2].GlobalPosition = new Vector2(470, 60); p2.Face(Vector2.Down); Place(deer[3], p2, 90); A(p2, "Grapple").TryUse(); TakeShot(0.15, "_grapple"); break;
                case 5: GD.Print($"[autoplay {_time:0.0}s] grappler test: grappled deer is now {deer[3].GlobalPosition.DistanceTo(p2.GlobalPosition):0} px away (was 90)"); break;
                case 6: deer[3].GlobalPosition = new Vector2(470, 60); p2.Face(Vector2.Right); Place(deer[1], p2, 80); _abilityHp2 = deer[1].Health.Current; A(p2, "Rocket").TryUse(); TakeShot(0.15, "_rocket"); TakeShot(0.4, "_boom"); break;
                case 8: GD.Print($"[autoplay {_time:0.0}s] grappler test: rocket: deer hp {_abilityHp2} -> {deer[1].Health.Current}"); break;
                case 9: p2.Teleport(new Vector2(110, 200)); p2.Face(Vector2.Left); A(p2, "Grapple").TryUse(); TakeShot(0.3, "_zip"); break;
                case 11: GD.Print($"[autoplay {_time:0.0}s] grappler test: zipped to the left wall: x 110 -> {p2.GlobalPosition.X:0}"); break;
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

    private async void TakeShot(double after = 0, string name = "")
    {
        var path = $"{_folder}/shot_{_shotNumber++:00}{name}.png";
        if (after > 0)
        {
            await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
