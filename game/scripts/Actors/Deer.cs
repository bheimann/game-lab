using Godot;

namespace LizardWizard.Actors;

/// <summary>
/// A deer tricked into fighting the heroes: walks up, flashes yellow (dodge now!) and kicks. Drops an antler and a hide.
/// </summary>
public partial class Deer : Beast
{
    [Export] public int KickDamage { get; set; } = 3;

    /// <summary>How long the deer flashes before it kicks, so players can dodge.</summary>
    [Export] public float WindUpSeconds { get; set; } = 0.4f;

    [Export] public float KickCooldownSeconds { get; set; } = 1f;

    protected override float BodyHeight => 32f;

    protected override string WalkStrip => "res://assets/sprites/deer/deer_walk.png";

    protected override Vector2I FrameSize => new(40, 42);

    protected override Items.MonsterPart[] Parts => new[] { Items.MonsterPart.Antler, Items.MonsterPart.Hide };

    public override string StealableMove => Abilities.StolenMoveAbility.DeerKick;

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        var inReach = InReach(target);
        if (WindUp >= 0)
        {
            WindUp += dt;
            if (WindUp >= WindUpSeconds)
            {
                if (inReach)
                {
                    target.TakeHit(KickDamage, GlobalPosition);
                }

                WindUp = -1f;
                AttackCooldown = KickCooldownSeconds;
            }

            return Vector2.Zero;
        }

        if (inReach)
        {
            if (AttackCooldown <= 0)
            {
                WindUp = 0f;
            }

            return Vector2.Zero;
        }

        return (target.GlobalPosition - GlobalPosition).Normalized() * ChaseSpeed;
    }
}
