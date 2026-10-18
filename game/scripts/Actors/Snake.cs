using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// A snake: hard to see in the grass until it's close, slides up slowly and bites with poison (it keeps hurting for a
/// while). Drops a scale.
/// </summary>
public partial class Snake : Beast
{
    [Export] public int BiteDamage { get; set; } = 2;

    [Export] public float WindUpSeconds { get; set; } = 0.3f;

    [Export] public float BiteCooldownSeconds { get; set; } = 1.4f;

    public Snake()
    {
        MinHealth = 8;
        MaxHealth = 10;
        ChaseSpeed = 30f;
        WanderSpeed = 10f;
        SightRange = 80f;
    }

    protected override float BodyHeight => 8f;

    public override float HurtRadius => 8f;

    protected override string WalkStrip => "res://assets/sprites/snake/snake_walk.png";

    protected override Vector2I FrameSize => new(28, 12);

    protected override MonsterPart[] Parts => new[] { MonsterPart.Scale };

    public override string StealableMove => Abilities.StolenMoveAbility.PoisonBite;

    /// <summary>Faint until a hero is close.</summary>
    protected override float Alpha => CurrentMood == Mood.Chasing || Heroes().Length == 0 ? 1f
        : System.Linq.Enumerable.Any(Heroes(), h => h.GlobalPosition.DistanceTo(GlobalPosition) < 50f) ? 1f : 0.35f;

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        if (WindUp >= 0)
        {
            WindUp += dt;
            if (WindUp >= WindUpSeconds)
            {
                if (InReach(target, 8f))
                {
                    target.TakeHit(BiteDamage, GlobalPosition);
                    if (target is Hero hero)
                    {
                        hero.Poison(3f);
                    }
                }

                WindUp = -1f;
                AttackCooldown = BiteCooldownSeconds;
            }

            return Vector2.Zero;
        }

        if (InReach(target))
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
