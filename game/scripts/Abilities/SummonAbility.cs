using System;
using System.Linq;
using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// The moose's Summoner class: calls a creature to fight for it (see <see cref="Summon"/>), or the Stampede.
/// LB / RB (or the ring menu, or a <see cref="CycleOptionsAbility"/>) pick which one. Each creature has its own wait
/// before the next summon. Only <see cref="MaxSummons"/> can be out at once (counting every summon ability): a new
/// one sends the oldest away. Changing class sends them all away.
/// </summary>
public partial class SummonAbility : Ability, IHasOptions
{
    private const string Stampede = "Stampede";

    private int _selected;
    private float _castFlash;

    /// <summary>What can be summoned, comma-separated, in order: <see cref="SummonKind"/> names, or "Stampede".</summary>
    [Export] public string Summons { get; set; } = "Squirrels, SpiritWolf, Bees, Owl, Wisps, Totem";

    [Export] public string OptionsTitle { get; set; } = "Summons";

    /// <summary>The most creatures that can be out at once.</summary>
    [Export] public int MaxSummons { get; set; } = 4;

    [Export] public float StampedeCooldown { get; set; } = 12f;

    public MenuOption[] Options => SummonList.Select(s => new MenuOption(NameOf(s), $"{DescriptionOf(s)} (wait {CooldownOf(s):0} s)", null, new Color(0.6f, 0.9f, 0.6f))).ToArray();

    public int Selected => _selected;

    private string[] SummonList => Summons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Select(int index)
    {
        var list = SummonList;
        if (list.Length == 0)
        {
            return;
        }

        _selected = (index % list.Length + list.Length) % list.Length;
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), NameOf(list[_selected]), new Color(0.7f, 1f, 0.7f), 1f, 8f);
    }

    protected override void Use()
    {
        var list = SummonList;
        if (list.Length == 0)
        {
            return;
        }

        var choice = list[_selected];
        CooldownLeft = CooldownOf(choice);
        _castFlash = 0.5f;

        if (choice == Stampede)
        {
            var stampede = new Stampede { Caster = Hero, Direction = Hero.Facing };
            Hero.GetParent().AddChild(stampede);
            stampede.GlobalPosition = Hero.GlobalPosition;
            return;
        }

        // Too many out: the oldest goes away.
        var out_ = Summon.Of(Hero);
        foreach (var old in out_.Take(Math.Max(0, out_.Count - MaxSummons + 1)))
        {
            FloatingText.Spawn(old, old.HurtCenter + new Vector2(0, -12), "Bye!", new Color(0.8f, 1f, 0.8f), 0.8f, 6f);
            old.Dismiss();
        }

        var kind = Enum.Parse<SummonKind>(choice);
        var summon = new Summon { Caster = Hero, Kind = kind };
        Hero.GetParent().AddChild(summon);
        summon.GlobalPosition = Hero.GlobalPosition + Hero.Facing * (kind == SummonKind.Treant ? 20f : 14f);
    }

    public override void Deactivate()
    {
        foreach (var summon in Summon.Of(Hero))
        {
            summon.Dismiss();
        }
    }

    protected override void Tick(float dt)
    {
        _castFlash -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_castFlash > 0)
        {
            // A green ring spreading out from the moose.
            var center = Hero.HurtCenter - Hero.GlobalPosition;
            DrawArc(center, 16f * (1.5f - _castFlash * 2f), 0, Mathf.Tau, 24, new Color(0.5f, 1f, 0.5f, _castFlash * 2f), 1.5f);
        }
    }

    private float CooldownOf(string choice) => choice == Stampede ? StampedeCooldown : Summon.StatsFor(Enum.Parse<SummonKind>(choice)).SummonCooldown;

    private static string NameOf(string choice) => choice == Stampede ? Stampede : Summon.StatsFor(Enum.Parse<SummonKind>(choice)).Name;

    private static string DescriptionOf(string choice) =>
        choice == Stampede ? "Ghost deer charge across, hitting everything" : Summon.StatsFor(Enum.Parse<SummonKind>(choice)).Description;
}
