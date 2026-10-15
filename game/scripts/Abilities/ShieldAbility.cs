using Godot;

namespace LizardWizard.Abilities;

/// <summary>
/// Raise a shield for a moment: any attack that lands while it's up is blocked.
/// With <see cref="Shell"/>, the hero ducks into its shell instead (the beetle): same block, drawn as a shell dome.
/// </summary>
public partial class ShieldAbility : Ability
{
    private float _upLeft;

    /// <summary>How long the shield stays up after a press.</summary>
    [Export] public float BlockSeconds { get; set; } = 0.5f;

    /// <summary>Walking speed with the shield up (0..1).</summary>
    [Export] public float MoveFactorWhileUp { get; set; } = 0.5f;

    /// <summary>Hide in a shell instead of holding a shield out.</summary>
    [Export] public bool Shell { get; set; }

    [Export] public Color ShellColour { get; set; } = new(0.45f, 0.3f, 0.7f);

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

        if (Shell)
        {
            DrawShell();
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

    private void DrawShell()
    {
        // A glossy dome over the hero, with a seam down the middle and a shine, pulsing a little.
        var outline = new Color(0.08f, 0.09f, 0.18f);
        var pulse = 1f + 0.05f * Mathf.Sin(_upLeft * 30f);
        var center = new Vector2(0, -8);
        DrawSetTransform(center, 0, new Vector2(1.1f, 1f) * pulse);
        DrawCircle(Vector2.Zero, 10f, ShellColour with { A = 0.85f });
        DrawLine(new Vector2(0, -10), new Vector2(0, 10), ShellColour.Darkened(0.45f), 1f);
        DrawCircle(new Vector2(-4, -5), 2f, ShellColour.Lightened(0.6f));
        DrawArc(Vector2.Zero, 10f, 0, Mathf.Tau, 24, outline, 1f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
