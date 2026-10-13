using Godot;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>
/// Chameleon camouflage (passive): stand still for a moment and the hero fades out and enemies lose
/// track of it. Moving, attacking or getting hit makes it visible again.
/// </summary>
public partial class CamouflageAbility : Ability
{
    private bool _wasHidden;
    private float _shimmer;

    public CamouflageAbility()
    {
        Slot = AbilitySlot.Passive;
    }

    /// <summary>How long the hero has to stand still before it's hidden.</summary>
    [Export] public float StillSecondsToHide { get; set; } = 0.6f;

    /// <summary>How see-through the hero is while hidden (0 = invisible, 1 = solid).</summary>
    [Export] public float HiddenAlpha { get; set; } = 0.3f;

    public bool IsHidden => !Hero.IsDown && Hero.StillSeconds >= StillSecondsToHide;

    public override bool HidesHero => IsHidden;

    public override float SpriteAlpha => IsHidden ? HiddenAlpha + 0.08f * Mathf.Sin(_shimmer) : 1f;

    protected override void Use()
    {
    }

    protected override void Tick(float dt)
    {
        _shimmer += dt * 6f;
        if (IsHidden && !_wasHidden)
        {
            FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), "hidden", new Color(0.7f, 1f, 0.7f, 0.8f), 0.7f, 6f);
        }

        _wasHidden = IsHidden;
    }
}
