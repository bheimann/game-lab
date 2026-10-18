using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// A frog: keeps its distance, hopping about, and spits slime blobs from afar. Drops frog skin.
/// </summary>
public partial class Frog : Beast
{
    private float _hop;
    private float _hopWait;
    private Vector2 _hopDirection;

    [Export] public int SpitDamage { get; set; } = 2;

    [Export] public float SpitCooldownSeconds { get; set; } = 1.8f;

    [Export] public float WindUpSeconds { get; set; } = 0.35f;

    public Frog()
    {
        MinHealth = 6;
        MaxHealth = 8;
        ChaseSpeed = 90f; // hop speed
        WanderSpeed = 14f;
        SightRange = 120f;
        LoseSightRange = 170f;
    }

    protected override float BodyHeight => 12f;

    public override float HurtRadius => 8f;

    protected override string WalkStrip => "res://assets/sprites/frog/frog_walk.png";

    protected override Vector2I FrameSize => new(20, 16);

    protected override MonsterPart[] Parts => new[] { MonsterPart.FrogSkin };

    public override string StealableMove => Abilities.StolenMoveAbility.FrogSpit;

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        var toTarget = target.GlobalPosition - GlobalPosition;
        var distance = toTarget.Length();

        if (WindUp >= 0)
        {
            WindUp += dt;
            if (WindUp >= WindUpSeconds)
            {
                WindUp = -1f;
                AttackCooldown = SpitCooldownSeconds;
                EnemyShot.Fire(this, HurtCenter, target.HurtCenter - HurtCenter, SpitDamage, IsCharmed);
            }

            return Vector2.Zero;
        }

        // Mid-hop: keep going.
        if (_hop > 0)
        {
            _hop -= dt;
            return _hopDirection * ChaseSpeed;
        }

        if (AttackCooldown <= 0 && distance < 130f)
        {
            WindUp = 0f;
            return Vector2.Zero;
        }

        // Between spits: hop to stay 60-100 pixels away.
        _hopWait -= dt;
        if (_hopWait <= 0)
        {
            _hopWait = (float)Rng.RandfRange(0.5f, 0.9f);
            _hop = 0.22f;
            var dir = toTarget.Normalized();
            _hopDirection = (distance < 60f ? -dir : distance > 100f ? dir : dir.Orthogonal() * (Rng.Randf() < 0.5f ? 1 : -1)).Rotated(Rng.RandfRange(-0.4f, 0.4f));
        }

        return Vector2.Zero;
    }
}
