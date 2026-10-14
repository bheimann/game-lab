using Godot;

namespace LizardWizard.Abilities;

/// <summary>Raise a shield for a moment: any attack that lands while it's up is blocked.</summary>
public partial class ShieldAbility : Ability
{
    private float _upLeft;

    /// <summary>How long the shield stays up after a press.</summary>
    [Export] public float BlockSeconds { get; set; } = 0.5f;

    /// <summary>Walking speed with the shield up (0..1).</summary>
    [Export] public float MoveFactorWhileUp { get; set; } = 0.5f;

    public override bool IsActive => _upLeft > 0;

    public override float MoveFactor => MoveFactorWhileUp;

    public override bool Blocks(Vector2 from) => IsActive;

    protected override void Use() => _upLeft = BlockSeconds;

    public override void Cancel() => _upLeft = 0;

    protected override void Tick(float dt)
    {
        _upLeft -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsActive)
        {
            return;
        }

        // A round shield held out in front, and a faint ring to show it's protecting the hero.
        var center = Hero.HurtCenter - Hero.GlobalPosition;
        var at = center + Hero.Facing * 8f;
        DrawCircle(center, 12f, new Color(0.7f, 0.85f, 1f, 0.15f));
        DrawCircle(at, 5f, new Color(0.55f, 0.6f, 0.75f));
        DrawArc(at, 5f, 0, Mathf.Tau, 16, new Color(0.08f, 0.09f, 0.18f), 1f);
        DrawCircle(at, 1.5f, new Color(0.95f, 0.8f, 0.35f));
    }
}
