using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// A boar: the toughest of the wild animals. It paws the ground (flashing yellow), then charges in a straight line and
/// hits hard; after a charge it stops to catch its breath, which is the moment to hit back. Drops a tusk.
/// </summary>
public partial class Boar : Beast
{
    private Vector2 _chargeDirection;
    private float _charging = -1f;
    private float _resting;
    private bool _hitThisCharge;

    [Export] public int ChargeDamage { get; set; } = 5;

    [Export] public float ChargeSpeed { get; set; } = 170f;

    [Export] public float ChargeSeconds { get; set; } = 0.7f;

    [Export] public float WindUpSeconds { get; set; } = 0.6f;

    /// <summary>How long it stands, out of breath, after a charge.</summary>
    [Export] public float RestSeconds { get; set; } = 1.3f;

    public Boar()
    {
        MinHealth = 24;
        MaxHealth = 30;
        ChaseSpeed = 30f;
        SightRange = 110f;
    }

    protected override float BodyHeight => 22f;

    public override float HurtRadius => 12f;

    protected override string WalkStrip => "res://assets/sprites/boar/boar_walk.png";

    protected override Vector2I FrameSize => new(32, 24);

    protected override MonsterPart[] Parts => new[] { MonsterPart.Tusk };

    public override string StealableMove => Abilities.StolenMoveAbility.BoarCharge;

    public override int StealableCoins => 5;

    /// <summary>Who to charge at, when the charge starts (normally whoever it's chasing).</summary>
    protected virtual IEnemyTarget ChargeAt(IEnemyTarget target) => target;

    /// <summary>For bosses: true once it's charging (or catching its breath after).</summary>
    protected bool IsCharging => _charging >= 0;

    protected override void StopAttack()
    {
        base.StopAttack();
        _charging = -1f;
    }

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        if (_resting > 0)
        {
            _resting -= dt;
            return Vector2.Zero;
        }

        if (_charging >= 0)
        {
            _charging += dt;
            if (!_hitThisCharge)
            {
                foreach (var victim in VictimsNear(HurtCenter + _chargeDirection * 8f, 10f))
                {
                    victim.TakeHit(ChargeDamage, GlobalPosition);
                    _hitThisCharge = true;
                }
            }

            // It stops when the charge runs out or it slams into something.
            if (_charging >= ChargeSeconds || (GetSlideCollisionCount() > 0 && _charging > 0.1f))
            {
                _charging = -1f;
                _resting = RestSeconds;
                return Vector2.Zero;
            }

            return _chargeDirection * ChargeSpeed;
        }

        if (WindUp >= 0)
        {
            WindUp += dt;
            if (WindUp >= WindUpSeconds)
            {
                WindUp = -1f;
                _charging = 0f;
                _hitThisCharge = false;
                _chargeDirection = (ChargeAt(target).GlobalPosition - GlobalPosition).Normalized();
            }

            return Vector2.Zero;
        }

        // Close enough: start pawing the ground.
        if (target.GlobalPosition.DistanceTo(GlobalPosition) < 120f)
        {
            WindUp = 0f;
            return Vector2.Zero;
        }

        return (target.GlobalPosition - GlobalPosition).Normalized() * ChaseSpeed;
    }
}
