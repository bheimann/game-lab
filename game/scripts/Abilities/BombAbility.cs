using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>Bomb Master: lob a bomb in front of the hero. It lands, the fuse burns, and it blows up everything nearby.</summary>
public partial class BombAbility : Ability
{
    public BombAbility()
    {
        Slot = AbilitySlot.Secondary;
        CooldownSeconds = 0.6f;
    }

    /// <summary>How far the bomb is thrown, in pixels (shorter if a wall is in the way).</summary>
    [Export] public float ThrowDistance { get; set; } = 48f;

    [Export] public float FlightSeconds { get; set; } = 0.4f;

    [Export] public float FuseSeconds { get; set; } = 1.0f;

    /// <summary>Blast radius in pixels.</summary>
    [Export] public float Radius { get; set; } = 28f;

    [Export] public int Damage { get; set; } = 8;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>How many of this hero's bombs can be out at once.</summary>
    [Export] public int MaxBombs { get; set; } = 2;

    protected override void Use()
    {
        var parent = Hero.GetParent();
        if (parent.GetChildren().OfType<Bomb>().Count(b => b.Thrower == Hero) >= MaxBombs)
        {
            CooldownLeft = 0;
            return;
        }

        var from = Hero.GlobalPosition;
        var to = from + Hero.Facing * ThrowDistance;

        // Land in front of a wall rather than going through it.
        var query = PhysicsRayQueryParameters2D.Create(from, to, collisionMask: 1);
        var hit = Hero.GetWorld2D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            to = (Vector2)hit["position"] - Hero.Facing * 6f;
        }

        var bomb = new Bomb
        {
            Thrower = Hero,
            From = from,
            To = to,
            FlightSeconds = FlightSeconds,
            FuseSeconds = FuseSeconds,
            Radius = Radius,
            Damage = Damage,
            DamageType = DamageType,
        };
        parent.AddChild(bomb);
        Hero.Lunge(Hero.Facing * 2f);
    }
}
