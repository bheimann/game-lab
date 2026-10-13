using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>
/// Chameleon camouflage (passive): stand still for a moment and the hero fades out and enemies lose
/// track of it. Moving, attacking or getting hit makes it visible again.
/// Armor changes it: the shadow cloak keeps you hidden while walking, ember scales resist fire while hidden.
/// </summary>
public partial class CamouflageAbility : Ability
{
    private bool _hidden;
    private int _actionsSeen;
    private float _shimmer;

    public CamouflageAbility()
    {
        Slot = AbilitySlot.Passive;
    }

    /// <summary>How long the hero has to stand still before it's hidden.</summary>
    [Export] public float StillSecondsToHide { get; set; } = 0.6f;

    /// <summary>How see-through the hero is while hidden (0 = invisible, 1 = solid).</summary>
    [Export] public float HiddenAlpha { get; set; } = 0.3f;

    public bool IsHidden => _hidden && !Hero.IsDown;

    public override bool HidesHero => IsHidden;

    public override float SpriteAlpha => IsHidden ? HiddenAlpha + 0.08f * Mathf.Sin(_shimmer) : 1f;

    public override float DamageMultiplier(DamageType type) =>
        IsHidden && Hero.Armor?.ResistWhileHidden == type ? 0.5f : 1f;

    protected override void Use()
    {
    }

    protected override void Tick(float dt)
    {
        _shimmer += dt * 6f;

        // Attacking or getting hit always shows you; walking does too, unless your armor says otherwise.
        var acted = Hero.ActionCount != _actionsSeen;
        _actionsSeen = Hero.ActionCount;
        var walked = Hero.StillSeconds == 0;
        if (Hero.IsDown || acted || (walked && Hero.Armor?.HideWhileMoving != true))
        {
            _hidden = false;
        }
        else if (!_hidden && Hero.StillSeconds >= StillSecondsToHide)
        {
            _hidden = true;
            FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), "hidden", new Color(0.7f, 1f, 0.7f, 0.8f), 0.7f, 6f);
        }
    }
}
