using System.Linq;
using Godot;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>
/// The lizard's tongue: shoots out, hits the first enemy it touches, and snaps back.
/// Aim assist bends it toward an enemy that's roughly in front of the hero.
/// </summary>
public partial class TongueAbility : Ability
{
    private const float OutSeconds = 0.1f;
    private const float HoldSeconds = 0.05f;
    private const float InSeconds = 0.15f;

    private Line2D _line = null!;
    private float _time = -1f;
    private bool _hasHit;
    private Vector2 _direction;
    private float _length;

    [Export] public int Damage { get; set; } = 1;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>How far the tongue reaches, in pixels.</summary>
    [Export] public float Range { get; set; } = 44f;

    /// <summary>Aim assist: the tongue snaps to an enemy within this many degrees of where the hero faces.</summary>
    [Export] public float AimAssistDegrees { get; set; } = 35f;

    [Export] public Color Colour { get; set; } = new(1f, 0.32f, 0.47f);

    public override bool IsActive => _time >= 0;

    public override float MoveFactor => 0f;

    public override void _Ready()
    {
        base._Ready();
        _line = new Line2D { Width = 2f, DefaultColor = Colour, EndCapMode = Line2D.LineCapMode.Round, Visible = false };
        AddChild(_line);
    }

    protected override void Use()
    {
        _time = 0;
        _hasHit = false;

        // Aim assist: snap to the closest enemy roughly in front of us, and stop at it.
        var mouth = Hero.ToGlobal(Hero.AttackOrigin);
        var assisted = Hero.EnemyTargets()
            .Select(t => t.HurtCenter - mouth)
            .Where(d => d.Length() <= Range + 8f && Mathf.RadToDeg(Mathf.Abs(Hero.Facing.AngleTo(d))) <= AimAssistDegrees)
            .OrderBy(d => d.Length())
            .FirstOrDefault();
        _direction = assisted == Vector2.Zero ? Hero.Facing : assisted.Normalized();
        _length = assisted == Vector2.Zero ? Range : Mathf.Min(Range, assisted.Length());
    }

    public override void Cancel()
    {
        _time = -1f;
        _line.Visible = false;
    }

    protected override void Tick(float dt)
    {
        if (_time < 0)
        {
            return;
        }

        _time += dt;
        float reach;
        if (_time < OutSeconds)
        {
            reach = _time / OutSeconds;
        }
        else if (_time < OutSeconds + HoldSeconds)
        {
            reach = 1f;
        }
        else
        {
            reach = 1f - (_time - OutSeconds - HoldSeconds) / InSeconds;
        }

        if (reach <= 0)
        {
            Cancel();
            return;
        }

        var mouth = Hero.AttackOrigin;
        var tip = mouth + _direction * _length * Mathf.Clamp(reach, 0, 1);
        _line.Points = new[] { mouth, tip };
        _line.Visible = true;
        _line.ZIndex = Hero.View == "up" ? -1 : 0; // facing away: tongue starts behind the head

        if (!_hasHit && reach >= 1f)
        {
            _hasHit = true;
            var start = Hero.ToGlobal(mouth);
            var end = Hero.ToGlobal(tip);
            var target = Hero.EnemyTargets()
                .Where(e => Hits.Touches(e, start, end))
                .OrderBy(e => e.HurtCenter.DistanceTo(start))
                .FirstOrDefault();
            target?.TakeHit(Damage, Hero.GlobalPosition, DamageType);
        }
    }
}
