using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// The moose Tinkerer fuses a monster part from the team's bag onto a teammate's main attack or armor (or its own, if
/// nobody is close), Tears of the Kingdom style: see <see cref="Fusion"/>. LB / RB (or the ring menu) pick the part and
/// whether it goes on the attack (a weapon or a gun, whichever the teammate's main attack is) or the armor.
/// </summary>
public partial class FuseAbility : Ability, IHasOptions
{
    private static readonly Color Spark = new(0.93f, 0.85f, 0.6f);

    private float _activeLeft;
    private Vector2 _sparkAt;
    private int _selected;

    /// <summary>How close a teammate has to be to get the fusion (otherwise the moose fuses its own).</summary>
    [Export] public float Reach { get; set; } = 30f;

    public override bool IsActive => _activeLeft > 0;

    public override float MoveFactor => 0.2f;

    public string OptionsTitle => "Fuse";

    /// <summary>Every part twice: onto the attack, then onto the armor.</summary>
    private static (MonsterPart Part, bool Armor)[] Choices =>
        MonsterPart.All.SelectMany(p => new[] { (p, false), (p, true) }).ToArray();

    public MenuOption[] Options => Choices.Select(c => new MenuOption(
        $"{c.Part.Name} {(c.Armor ? "armor" : "weapon")}",
        $"{(c.Armor ? c.Part.ArmorDescription : c.Part.AttackDescription)} (you have {MonsterParts.Count(c.Part)})",
        c.Part.Icon, Colors.White) { Count = $"x{MonsterParts.Count(c.Part)}", Enabled = MonsterParts.Count(c.Part) > 0 }).ToArray();

    public int Selected => _selected;

    public void Select(int index)
    {
        var count = Choices.Length;
        _selected = (index % count + count) % count;
        var (part, armor) = Choices[_selected];
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), $"Fuse {part.Name} onto {(armor ? "armor" : "weapon")} (x{MonsterParts.Count(part)})", Colors.White, 1f, 8f);
    }

    protected override void Use()
    {
        var (part, armor) = Choices[_selected];
        var textAt = Hero.HurtCenter + new Vector2(0, -16);
        if (MonsterParts.Count(part) <= 0)
        {
            FloatingText.Spawn(this, textAt, $"No {part.Plural}! Beaten enemies drop them", new Color(0.8f, 0.8f, 0.8f), 1.2f, 7f);
            CooldownLeft = 0.2f;
            return;
        }

        // The closest teammate in reach, or the moose itself.
        var target = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .Where(h => h != Hero && h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(Hero.GlobalPosition) <= Reach)
            .OrderBy(h => h.GlobalPosition.DistanceTo(Hero.GlobalPosition))
            .FirstOrDefault() ?? Hero;

        MonsterParts.Take(part);
        var fusion = new Fusion(part, armor ? FuseKind.Armor : target.MainAttackKind);
        var replaced = target.AddFusion(fusion);
        var name = fusion.NameFor(target.MainAttackName);
        var who = target == Hero ? "" : $" for the {target.DisplayName.ToLowerInvariant()}";
        FloatingText.Spawn(this, target.HurtCenter + new Vector2(0, -18), $"{name}{who}!", Spark, 1.6f, 8f);
        if (replaced is not null)
        {
            FloatingText.Spawn(this, target.HurtCenter + new Vector2(0, -8), $"(replaced {replaced.NameFor(target.MainAttackName)})", new Color(0.8f, 0.8f, 0.8f), 1.6f, 6f);
        }

        _activeLeft = 0.35f;
        _sparkAt = target.HurtCenter;
        Hero.Lunge((target.GlobalPosition - Hero.GlobalPosition).Normalized() * 3f);
    }

    public override void Cancel() => _activeLeft = 0;

    protected override void Tick(float dt)
    {
        _activeLeft -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_activeLeft <= 0)
        {
            return;
        }

        // Sparks flying from the moose to whoever got the fusion.
        var from = ToLocal(Hero.HurtCenter);
        var to = ToLocal(_sparkAt);
        for (var i = 0; i < 5; i++)
        {
            var t = Mathf.PosMod(i / 5f + _activeLeft * 3f, 1f);
            var at = from.Lerp(to, t) + new Vector2(0, -Mathf.Sin(t * Mathf.Pi) * 10f);
            DrawCircle(at, 1.5f, Spark);
        }

        DrawArc(to, 9f * (1.4f - _activeLeft * 2f), 0, Mathf.Tau, 16, Spark, 1f);
    }
}
