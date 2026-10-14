using System.Linq;
using Godot;
using LizardWizard.Combat;

namespace LizardWizard.Abilities;

/// <summary>
/// The lizard's tongue: shoots out, hits the first enemy it touches, and snaps back.
/// Light enemies (<see cref="IPullable"/>) get stuck to it and reeled in to right in front of the hero.
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
    private IHittable? _caught;
    private float _reelTime;
    private float _stuckTime;
    private Vector2 _caughtLastPosition;
    private float _pullSpeed;

    [Export] public int Damage { get; set; } = 1;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>How far the tongue reaches, in pixels.</summary>
    [Export] public float Range { get; set; } = 44f;

    /// <summary>Aim assist: the tongue snaps to an enemy within this many degrees of where the hero faces.</summary>
    [Export] public float AimAssistDegrees { get; set; } = 35f;

    /// <summary>How fast a caught enemy gets reeled in, in pixels per second.</summary>
    [Export] public float PullSpeed { get; set; } = 170f;

    /// <summary>Let go after this long even if the enemy hasn't arrived yet.</summary>
    [Export] public float MaxReelSeconds { get; set; } = 0.6f;

    /// <summary>Reeled-in enemies stop this many pixels from the mouth (past their own size).</summary>
    [Export] public float PullGap { get; set; } = 4f;

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
        _caught = null;

        // Aim assist: snap to the closest enemy roughly in front of us, and stop at it.
        var mouth = Hero.ToGlobal(Hero.AttackOrigin);
        var assisted = Hero.EnemyTargets()
            .Select(t => t.HurtCenter - mouth)
            .Where(d => d.Length() <= Range + 8f && Mathf.RadToDeg(Mathf.Abs(Hero.Facing.AngleTo(d))) <= AimAssistDegrees)
            .Where(d => Hero.ClearPath(mouth, mouth + d * 0.85f))
            .OrderBy(d => d.Length())
            .FirstOrDefault();
        _direction = assisted == Vector2.Zero ? Hero.Facing : assisted.Normalized();
        _length = assisted == Vector2.Zero ? Range : Mathf.Min(Range, assisted.Length());

        // Walls and rocks stop the tongue (it reaches over pits, though).
        var wall = Hero.GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(mouth, mouth + _direction * _length, collisionMask: 1));
        if (wall.Count > 0)
        {
            _length = Mathf.Max(4f, ((Vector2)wall["position"]).DistanceTo(mouth) - 1f);
        }
    }

    public override void Cancel()
    {
        _time = -1f;
        _caught = null;
        _line.Visible = false;
    }

    protected override void Tick(float dt)
    {
        if (_time < 0)
        {
            return;
        }

        if (_caught is not null)
        {
            Reel(dt);
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
            if (target is IPullable { CanBePulled: true })
            {
                _caught = target;
                _reelTime = 0;
                _stuckTime = 0;
                _caughtLastPosition = target.HurtCenter;
                _pullSpeed = 0;
            }
        }
    }

    /// <summary>The tongue is stuck to an enemy: drag it in until it's right in front of us, then let go.</summary>
    private void Reel(float dt)
    {
        var caught = _caught!;
        if (!IsInstanceValid((GodotObject)caught))
        {
            LetGo();
            return;
        }

        _reelTime += dt;

        // A pit or wall in the way stops the enemy, and then the tongue slips off.
        var moved = caught.HurtCenter.DistanceTo(_caughtLastPosition);
        _caughtLastPosition = caught.HurtCenter;
        _stuckTime = _pullSpeed > 0 && moved < _pullSpeed * dt * 0.25f ? _stuckTime + dt : 0;

        var toMouth = Hero.ToGlobal(Hero.AttackOrigin) - caught.HurtCenter;
        var gap = caught.HurtRadius + PullGap;
        if (toMouth.Length() <= gap || _reelTime >= MaxReelSeconds || _stuckTime >= 0.1f)
        {
            LetGo();
            return;
        }

        // Slow down at the end so it doesn't overshoot into the hero.
        _pullSpeed = Mathf.Min(PullSpeed, (toMouth.Length() - gap) / dt);
        ((IPullable)caught).Pull(toMouth.Normalized() * _pullSpeed);

        _line.Points = new[] { Hero.AttackOrigin, Hero.ToLocal(caught.HurtCenter) };
    }

    /// <summary>Stop pulling and snap the tongue back from wherever its tip is now.</summary>
    private void LetGo()
    {
        var tip = _line.Points.Length == 2 ? _line.Points[1] - Hero.AttackOrigin : Vector2.Zero;
        _caught = null;
        _direction = tip == Vector2.Zero ? _direction : tip.Normalized();
        _length = tip.Length();
        _time = OutSeconds + HoldSeconds;
    }
}
