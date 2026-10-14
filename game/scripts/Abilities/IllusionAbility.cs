using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// Illusionist spells, cast with Y; LB / RB (or the ring menu) pick the spell. They cost illusionist energy,
/// which fills back up over time. Every spell lasts <see cref="SpellSeconds"/>.
/// - Clones: copies of the hero that enemies chase instead.
/// - Tail decoy: drop your tail; enemies attack it instead.
/// - Charm: nearby enemies fight for the heroes (costs energy for each one). Bosses can't be charmed.
/// </summary>
public partial class IllusionAbility : Ability, IHasOptions
{
    private static readonly (string Name, string Description)[] Spells =
    {
        ("Clones", "Copies of you that enemies chase"),
        ("Tail Decoy", "Drop your tail; enemies attack it"),
        ("Charm", "Nearby enemies fight for you"),
    };

    private int _selected;
    private float _castFlash;

    public IllusionAbility()
    {
        Slot = AbilitySlot.Special;
        CooldownSeconds = 0.4f;
    }

    [Export] public float MaxEnergy { get; set; } = 10f;

    /// <summary>Energy that comes back each second.</summary>
    [Export] public float EnergyPerSecond { get; set; } = 1f;

    [Export] public float SpellSeconds { get; set; } = 5f;

    [Export] public int CloneCost { get; set; } = 5;

    [Export] public int CloneCount { get; set; } = 2;

    [Export] public int DecoyCost { get; set; } = 4;

    /// <summary>Energy for each enemy charmed.</summary>
    [Export] public int CharmCost { get; set; } = 2;

    /// <summary>How close enemies must be to get charmed.</summary>
    [Export] public float CharmRange { get; set; } = 70f;

    public float Energy { get; private set; } = 10f;

    public string OptionsTitle => "Spells";

    public MenuOption[] Options =>
        Spells.Select((s, i) => new MenuOption(s.Name, $"{s.Description} ({CostText(i)})", null, new Color(0.75f, 0.55f, 1f))).ToArray();

    public int Selected => _selected;

    public void Select(int index)
    {
        _selected = (index % Spells.Length + Spells.Length) % Spells.Length;
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), Spells[_selected].Name, new Color(0.8f, 0.65f, 1f), 1f, 8f);
    }

    public override void _Ready()
    {
        base._Ready();
        Energy = MaxEnergy;
    }

    protected override void Use()
    {
        var cast = _selected switch
        {
            0 => CastClones(),
            1 => CastDecoy(),
            _ => CastCharm(),
        };

        if (!cast)
        {
            CooldownLeft = 0;
        }
    }

    protected override void Tick(float dt)
    {
        Energy = Mathf.Min(MaxEnergy, Energy + EnergyPerSecond * dt);
        _castFlash -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Energy as purple pips under the hero's feet.
        var pips = Mathf.RoundToInt(MaxEnergy);
        var left = -pips * 3 / 2f;
        for (var i = 0; i < pips; i++)
        {
            var full = Energy >= i + 1;
            var rect = new Rect2(left + i * 3, 5, 2, 2);
            DrawRect(rect.Grow(0.5f), new Color(0.08f, 0.09f, 0.18f));
            DrawRect(rect, full ? new Color(0.75f, 0.5f, 1f) : new Color(0.3f, 0.25f, 0.4f));
        }

        if (_castFlash > 0)
        {
            DrawArc(Hero.HurtCenter - Hero.GlobalPosition, 14f * (1.5f - _castFlash * 2f), 0, Mathf.Tau, 24, new Color(0.8f, 0.6f, 1f, _castFlash * 2f), 1.5f);
        }
    }

    private bool CastClones()
    {
        if (!Spend(CloneCost))
        {
            return false;
        }

        for (var i = 0; i < CloneCount; i++)
        {
            var side = new Vector2(-Hero.Facing.Y, Hero.Facing.X) * (i % 2 == 0 ? 14f : -14f);
            AddLure(LureKind.Clone, Hero.GlobalPosition + side);
        }

        return true;
    }

    private bool CastDecoy()
    {
        if (!Spend(DecoyCost))
        {
            return false;
        }

        AddLure(LureKind.Tail, Hero.GlobalPosition - Hero.Facing * 10f);
        return true;
    }

    private bool CastCharm()
    {
        var targets = GetTree().GetNodesInGroup(Enemies.Group).OfType<ICharmable>()
            .Where(e => e.CanBeCharmed && e.HurtCenter.DistanceTo(Hero.HurtCenter) <= CharmRange)
            .OrderBy(e => e.HurtCenter.DistanceTo(Hero.HurtCenter))
            .Take((int)(Energy / CharmCost))
            .ToList();
        if (targets.Count == 0)
        {
            var why = Energy < CharmCost ? "Not enough energy" : "Nobody to charm";
            FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), why, new Color(0.8f, 0.8f, 0.8f), 0.8f, 6f);
            return false;
        }

        Spend(CharmCost * targets.Count);
        foreach (var target in targets)
        {
            target.Charm(SpellSeconds, Hero);
        }

        return true;
    }

    private bool Spend(int cost)
    {
        if (Energy < cost)
        {
            FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), "Not enough energy", new Color(0.8f, 0.8f, 0.8f), 0.8f, 6f);
            return false;
        }

        Energy -= cost;
        _castFlash = 0.5f;
        Hero.NotifyAction();
        return true;
    }

    private void AddLure(LureKind kind, Vector2 at)
    {
        var lure = new Lure { Caster = Hero, Kind = kind, Seconds = SpellSeconds };
        Hero.GetParent().AddChild(lure);
        lure.GlobalPosition = at;
    }

    private string CostText(int spell) => spell switch
    {
        0 => $"{CloneCost} energy",
        1 => $"{DecoyCost} energy",
        _ => $"{CharmCost} energy each",
    };
}
