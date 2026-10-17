using Godot;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>
/// The beetle Vehicle Master builds a <see cref="Tank"/> and hops in to drive it. Pressing the button again next to the
/// tank climbs back in; far from it, the old one is packed away and a new one built right here. When the tank breaks,
/// the beetle has to wait a while before building another. (While driving, the same button hops out; see Hero.)
/// </summary>
public partial class VehicleAbility : Ability
{
    private Tank? _tank;
    private float _rebuildLeft;

    /// <summary>How long after the tank breaks before another can be built.</summary>
    [Export] public float RebuildSeconds { get; set; } = 10f;

    public Tank? Tank => _tank is not null && IsInstanceValid(_tank) && !_tank.IsQueuedForDeletion() ? _tank : null;

    protected override void Use()
    {
        var textAt = Hero.HurtCenter + new Vector2(0, -16);
        if (Tank is { } tank && tank.GlobalPosition.DistanceTo(Hero.GlobalPosition) < 32f)
        {
            tank.Board(Hero);
            return;
        }

        if (_rebuildLeft > 0)
        {
            FloatingText.Spawn(this, textAt, $"Fixing the tank... {Mathf.CeilToInt(_rebuildLeft)}s", new Color(0.8f, 0.8f, 0.8f), 0.8f, 7f);
            CooldownLeft = 0.2f;
            return;
        }

        Tank?.Break(quietly: true);
        _tank = new Tank { Builder = Hero };
        _tank.Broken += () => _rebuildLeft = RebuildSeconds;
        Hero.GetParent().AddChild(_tank);
        _tank.GlobalPosition = Hero.GlobalPosition + new Vector2(0, -6);
        FloatingText.Spawn(this, textAt, "Tank built!", new Color(0.93f, 0.85f, 0.6f), 1.2f, 8f);
        _tank.Board(Hero);
    }

    public override void Deactivate() => Tank?.Break(quietly: true);

    protected override void Tick(float dt) => _rebuildLeft -= dt;
}
