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
/// says whether its attack landed (P1's health, poison), then it's knocked out and the log says what it dropped.
/// Saves a screenshot of each one mid-attack.
/// </summary>
public partial class DevBeastCheck : Node
{
    public const string Flag = "--autoplay-beasts=";

    private static readonly string[] Kinds = { "deer", "boar", "arctic_fox", "frog", "snake" };

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
                manager.JoinForDev(new PlayerInput(InputDevice.KeyboardLeft), 1, "Bomb Master"); // the fox (a still chameleon would be hidden)
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
        hero.Teleport(room.GlobalPosition + new Vector2(560, 328));
        hero.Health.Refill();
        CoopCamera.Find(this)?.Snap();
        _hpBefore = hero.Health.Current;
        _beast = GD.Load<PackedScene>($"res://scenes/actors/{Kinds[_kind]}.tscn").Instantiate<Beast>();
        room.AddChild(_beast);
        _beast.GlobalPosition = hero.GlobalPosition + (_kind == 3 ? new Vector2(-88, 18) : new Vector2(-50, 10));
        GD.Print($"[beasts {_time:0.0}s] {Kinds[_kind]}: health {_beast.Health.Current}");
    }

    private void Finish(Hero hero)
    {
        var beast = _beast!;
        GD.Print($"[beasts {_time:0.0}s] {Kinds[_kind]}: P1 hp {_hpBefore} -> {hero.Health.Current}{(hero.IsDown ? " (knocked out)" : "")}, chasing={beast.CurrentMood}");
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
    }

    private async void Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_folder}/beast{name}.png");
    }
}
