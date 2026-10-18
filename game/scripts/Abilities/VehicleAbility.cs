using System.Linq;
using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// The beetle Vehicle Master builds a <see cref="Vehicle"/> and hops in to drive it. LB / RB (or the ring menu) pick
/// which: tank, cart, boat, glider or plane. Pressing the button again next to the vehicle climbs back in; far from it
/// (or with a different vehicle picked), the old one is packed away and the new one built right here. When a vehicle
/// breaks, the beetle has to wait a while before building another. (While driving, the same button hops out; see Hero.)
/// </summary>
public partial class VehicleAbility : Ability, IHasOptions
{
    /// <summary>What the beetle can build: everything, but the hoversled only once Frostfang Fortress is beaten.</summary>
    private static VehicleKind[] Kinds => System.Enum.GetValues<VehicleKind>()
        .Where(k => k != VehicleKind.Hoversled || Unlocks.IsBeaten(Actors.FrostMoose.Boss)).ToArray();

    private Vehicle? _vehicle;
    private float _rebuildLeft;
    private int _selected;

    /// <summary>How long after a vehicle breaks before another can be built.</summary>
    [Export] public float RebuildSeconds { get; set; } = 10f;

    public Vehicle? Vehicle => _vehicle is not null && IsInstanceValid(_vehicle) && !_vehicle.IsQueuedForDeletion() ? _vehicle : null;

    public VehicleKind Kind => Kinds[Mathf.Min(_selected, Kinds.Length - 1)];

    /// <summary>Pick a vehicle by kind (if the beetle can build it).</summary>
    public void Choose(VehicleKind kind)
    {
        var index = System.Array.IndexOf(Kinds, kind);
        if (index >= 0)
        {
            Select(index);
        }
    }

    public string OptionsTitle => "Vehicles";

    public MenuOption[] Options => Kinds.Select(k => new MenuOption(k.ToString(), Effects.Vehicle.Describe(k), null, Tint(k))).ToArray();

    public int Selected => _selected;

    public void Select(int index)
    {
        _selected = (index % Kinds.Length + Kinds.Length) % Kinds.Length;
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), Kind.ToString(), Tint(Kind), 1f, 8f);
    }

    protected override void Use()
    {
        var textAt = Hero.HurtCenter + new Vector2(0, -16);
        if (Vehicle is { } vehicle && vehicle.Kind == Kind && vehicle.GlobalPosition.DistanceTo(Hero.GlobalPosition) < 32f)
        {
            vehicle.Board(Hero);
            return;
        }

        if (_rebuildLeft > 0)
        {
            FloatingText.Spawn(this, textAt, $"Fixing up... {Mathf.CeilToInt(_rebuildLeft)}s", new Color(0.8f, 0.8f, 0.8f), 0.8f, 7f);
            CooldownLeft = 0.2f;
            return;
        }

        Vehicle?.Break(quietly: true);
        _vehicle = new Vehicle { Builder = Hero, Kind = Kind };
        _vehicle.Broken += () => _rebuildLeft = RebuildSeconds;
        Hero.GetParent().AddChild(_vehicle);
        _vehicle.GlobalPosition = Hero.GlobalPosition + new Vector2(0, -6);
        FloatingText.Spawn(this, textAt, $"{Kind} built!", new Color(0.93f, 0.85f, 0.6f), 1.2f, 8f);
        _vehicle.Board(Hero);
    }

    public override void Deactivate() => Vehicle?.Break(quietly: true);

    protected override void Tick(float dt) => _rebuildLeft -= dt;

    private static Color Tint(VehicleKind kind) => kind switch
    {
        VehicleKind.Tank => new Color(0.6f, 0.5f, 0.8f),
        VehicleKind.Cart => new Color(0.66f, 0.42f, 0.26f),
        VehicleKind.Boat => new Color(0.4f, 0.7f, 1f),
        VehicleKind.Glider => new Color(0.95f, 0.85f, 0.55f),
        VehicleKind.Hoversled => new Color(0.6f, 0.85f, 1f),
        _ => new Color(0.85f, 0.27f, 0.25f),
    };
}
