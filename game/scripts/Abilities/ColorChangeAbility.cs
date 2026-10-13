using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>
/// Chameleon colour change: each press switches to the next colour, and each colour resists one element.
/// Green (normal) → blue (ice) → red (fire) → yellow (lightning) → purple (arcane) → green.
/// </summary>
public partial class ColorChangeAbility : Ability
{
    private static readonly (string Name, DamageType? Resists, float HueShift, Color TextColour)[] Colours =
    {
        ("Green", null, 0f, new Color(0.4f, 0.8f, 0.3f)),
        ("Blue: resists ice", DamageType.Ice, 0.30f, new Color(0.3f, 0.7f, 1f)),
        ("Red: resists fire", DamageType.Fire, 0.72f, new Color(1f, 0.4f, 0.3f)),
        ("Yellow: resists lightning", DamageType.Lightning, 0.85f, new Color(1f, 0.9f, 0.3f)),
        ("Purple: resists arcane", DamageType.Arcane, 0.52f, new Color(0.8f, 0.5f, 1f)),
    };

    private ShaderMaterial _material = null!;
    private int _index;

    public ColorChangeAbility()
    {
        Slot = AbilitySlot.Special;
        CooldownSeconds = 0.4f;
    }

    /// <summary>Damage of the matching element is multiplied by this (0.5 = half damage).</summary>
    [Export] public float Resistance { get; set; } = 0.5f;

    public DamageType? Resists => Colours[_index].Resists;

    public override void _Ready()
    {
        base._Ready();
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/hue_shift.gdshader") };
        CallDeferred(MethodName.Apply);
    }

    public override float DamageMultiplier(DamageType type) => Resists == type ? Resistance : 1f;

    protected override void Use()
    {
        _index = (_index + 1) % Colours.Length;
        Apply();
        var colour = Colours[_index];
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), colour.Name, colour.TextColour, 1f, 8f);
    }

    private void Apply()
    {
        Hero.Sprite.Material = _index == 0 ? null : _material;
        _material.SetShaderParameter("hue_shift", Colours[_index].HueShift);
    }
}
