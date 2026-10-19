using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Effects;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the wild animals: <c>-- --room=Lake --autoplay-beasts=&lt;folder&gt;</c>. P1 joins (as the fox) and
/// stands still while each kind of animal (deer, boar, arctic fox, frog, snake) is put next to it in turn: the log
/// says how it takes fire, ice and lightning (its climate's weakness), whether its attack landed (P1's health), then it's
/// knocked out and the log says what it dropped.
/// Saves a screenshot of each one mid-attack.
/// </summary>
public partial class DevBeastCheck : Node
{
    public const string Flag = "--autoplay-beasts=";

    /// <summary>The last one, "fenced", is a deer with a fence between it and P1: it has to find its way round.</summary>
    private static readonly string[] Kinds = { "deer", "boar", "arctic_fox", "frog", "snake", "fenced" };

    private Node2D? _fence;

    private string _folder = "";
    private double _time;
    private double _stepStart;
    private int _kind = -1;
    private int _hpBefore;
    private Beast? _beast;

    public static DevBeastCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var manager = PlayerManager.Find(this)!;
        var hero = manager.Players[0];
        if (hero is null)
        {
            if (_time > 0.2)
            {
                manager.JoinForDev(InputDevice.KeyboardLeft, 1, "Bomb Master"); // the fox (a still chameleon would be hidden)
            }

            return;
        }

        var since = _time - _stepStart;
        if (_kind < 0 || since > 7)
        {
            if (_beast is not null)
            {
                Finish(hero);
            }

            _kind++;
            if (_kind >= Kinds.Length)
            {
                GD.Print("[beasts] done");
                GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                SetPhysicsProcess(false);
                return;
            }

            Start(hero);
        }
        else if (since is > 3.0 and < 3.02)
        {
            Shot($"_{Kinds[_kind]}");
        }
    }

    private void Start(Hero hero)
    {
        // A fresh hero (full health), with the animal a little way along the beach path (kept clear of bushes and rocks).
        _stepStart = _time;
        var room = Dungeon.Find(this)!.CurrentRoom;
        if (_kind == 0)
        {
            // A quiet room: the area's own animals would join in and spoil the results.
            foreach (var other in room.Enemies.OfType<Node>().ToList())
            {
                other.QueueFree();
            }
        }

        hero.Teleport(room.GlobalPosition + new Vector2(560, 328));
        hero.Health.Refill();
        CoopCamera.Find(this)?.Snap();
        _hpBefore = hero.Health.Current;
        var fenced = Kinds[_kind] == "fenced";
        _beast = GD.Load<PackedScene>($"res://scenes/actors/{(fenced ? "deer" : Kinds[_kind])}.tscn").Instantiate<Beast>();
        room.AddChild(_beast);
        _beast.GlobalPosition = hero.GlobalPosition + (_kind == 3 ? new Vector2(-88, 18) : new Vector2(-50, 10));
        if (fenced)
        {
            // A fence (three pieces wide) between the deer and P1, far enough from P1 that the deer can't kick over it.
            _beast.GlobalPosition = hero.GlobalPosition + new Vector2(0, -85); // close enough to see P1
            _fence = new Node2D();
            room.AddChild(_fence);
            for (var i = -1; i <= 1; i++)
            {
                var piece = new Prop { Kind = PropKind.Fence };
                _fence.AddChild(piece);
                piece.GlobalPosition = hero.GlobalPosition + new Vector2(i * 40, -50); // too far to kick over
            }
        }

        GD.Print($"[beasts {_time:0.0}s] {Kinds[_kind]}: health {_beast.Health.Current}");
    }

    private void Finish(Hero hero)
    {
        var beast = _beast!;
        GD.Print($"[beasts {_time:0.0}s] {Kinds[_kind]}: P1 hp {_hpBefore} -> {hero.Health.Current}{(hero.IsDown ? " (knocked out)" : "")}, chasing={beast.CurrentMood}");

        // Elements: 4 damage each of fire, ice and lightning (lightning always hits double); the log shows what got through.
        var hits = new System.Collections.Generic.List<string>();
        foreach (var type in new[] { Combat.DamageType.Fire, Combat.DamageType.Ice, Combat.DamageType.Lightning })
        {
            beast.Health.Refill();
            var before = beast.Health.Current;
            beast.TakeHit(4, beast.GlobalPosition + Vector2.Left * 10, type);
            hits.Add($"{type} -{before - beast.Health.Current}");
        }

        GD.Print($"[beasts {_time:0.0}s] {Kinds[_kind]} ({beast.Climate}): {string.Join(", ", hits)}");

        while (beast.IsHostile)
        {
            beast.TakeHit(5, beast.GlobalPosition + Vector2.Left * 10);
        }

        var loot = GetTree().CurrentScene.FindChildren("*", "", true, false).OfType<Loot>().Where(l => l.Part is not null).ToList();
        GD.Print($"[beasts {_time:0.0}s] {Kinds[_kind]}: asleep={beast.IsAsleep}, parts dropped: {string.Join(", ", loot.Select(l => l.Part!.Name))}");
        foreach (var l in loot)
        {
            l.QueueFree();
        }

        beast.QueueFree();
        _beast = null;
        _fence?.QueueFree();
        _fence = null;
    }

    private async void Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_folder}/beast{name}.png");
    }
}
