using System.Linq;
using Godot;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>The Bomb Master's detonator (Y): sets off every remote bomb this hero has waiting on the ground, all at once.</summary>
public partial class DetonateAbility : Ability
{
    public DetonateAbility()
    {
        Slot = AbilitySlot.Special;
        CooldownSeconds = 0.2f;
    }

    protected override void Use()
    {
        var waiting = Hero.GetParent().GetChildren().OfType<Bomb>().Where(b => b.Thrower == Hero && b.IsWaitingForRemote).ToList();
        if (waiting.Count == 0)
        {
            FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), "No remote bombs out", new Color(0.8f, 0.8f, 0.8f), 0.8f, 6f);
            return;
        }

        waiting.ForEach(b => b.Detonate());
    }
}
