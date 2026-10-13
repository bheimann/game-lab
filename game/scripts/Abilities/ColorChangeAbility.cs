using Godot;
using LizardWizard.Combat;
using System.Linq;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// Chameleon colour change: each press switches to the next colour, and each colour resists one element.
/// Green (normal) → blue (ice) → red (fire) → yellow (lightning) → purple (arcane) → green.
/// LB / RB and the ring menu can also pick a colour directly.
/// </summary>
public partial class ColorChangeAbility : Ability, IHasOptions
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

    public string OptionsTitle => "Colours";

    public MenuOption[] Options =>
        Colours.Select(c => new MenuOption(c.Name.Split(':')[0], c.Resists is null ? "No resistance" : $"Half damage from {c.Resists.ToString()!.ToLower()}", null, c.TextColour)).ToArray();

    public int Selected => _index;

    public void Select(int index)
    {
        _index = (index % Colours.Length + Colours.Length) % Colours.Length;
        Apply();
        var colour = Colours[_index];
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), colour.Name, colour.TextColour, 1f, 8f);
    }

    public override void _Ready()
    {
        base._Ready();
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/hue_shift.gdshader") };
        CallDeferred(MethodName.Apply);
    }

    public override float DamageMultiplier(DamageType type) => Resists == type ? Resistance : 1f;

    protected override void Use() => Select(_index + 1);

    private void Apply()
    {
        Hero.Sprite.Material = _index == 0 ? null : _material;
        _material.SetShaderParameter("hue_shift", Colours[_index].HueShift);
    }
}
