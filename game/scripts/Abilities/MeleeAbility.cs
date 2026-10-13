using System.Linq;
using Godot;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>A short-range hit in front of the hero: the lizard's punch, the fox's bite.</summary>
public partial class MeleeAbility : Ability
{
    private float _activeLeft;
    private float _flashLeft;
    private Vector2 _hitPoint;

    [Export] public int Damage { get; set; } = 5;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>How far in front of the hero the hit lands.</summary>
    [Export] public float Reach { get; set; } = 12f;

    /// <summary>Size of the hit circle.</summary>
    [Export] public float Radius { get; set; } = 8f;

    /// <summary>How long the attack keeps the hero busy.</summary>
    [Export] public float ActiveSeconds { get; set; } = 0.2f;

    /// <summary>Walking speed while attacking (0..1).</summary>
    [Export] public float MoveFactorWhileActive { get; set; } = 0.3f;

    [Export] public Color FlashColour { get; set; } = new(1f, 0.93f, 0.5f);

    public override bool IsActive => _activeLeft > 0;

    public override float MoveFactor => MoveFactorWhileActive;

    protected override void Use()
    {
        _activeLeft = ActiveSeconds;
        _flashLeft = 0.1f;
        _hitPoint = Hero.HurtCenter + Hero.Facing * Reach;

        foreach (var enemy in Hero.EnemyTargets().Where(t => Hits.Touches(t, _hitPoint, Radius)))
        {
            enemy.TakeHit(Damage, Hero.GlobalPosition, DamageType);
        }

        Hero.Lunge(Hero.Facing * 3f);
    }

    public override void Cancel() => _activeLeft = 0;

    protected override void Tick(float dt)
    {
        _activeLeft -= dt;
        _flashLeft -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_flashLeft > 0)
        {
            var at = ToLocal(_hitPoint);
            DrawCircle(at, 5f, new Color(1, 1, 1, 0.8f));
            DrawArc(at, 7f, 0, Mathf.Tau, 16, FlashColour, 1f);
        }
    }
}
