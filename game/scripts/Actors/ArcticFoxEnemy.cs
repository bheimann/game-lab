using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// An arctic fox: quick and nimble. It darts in, bites, then runs off before coming back for another go.
/// Drops fur.
/// </summary>
public partial class ArcticFoxEnemy : Beast
{
    private float _retreating;

    [Export] public int BiteDamage { get; set; } = 2;

    [Export] public float WindUpSeconds { get; set; } = 0.2f;

    public ArcticFoxEnemy()
    {
        MinHealth = 7;
        MaxHealth = 9;
        ChaseSpeed = 74f;
        WanderSpeed = 26f;
        SightRange = 110f;
    }

    /// <summary>From the snow: weak to fire, resists ice, wherever it is.</summary>
    protected override Combat.Climate? NativeClimate => Combat.Climate.Cold;

    protected override float BodyHeight => 18f;

    public override float HurtRadius => 9f;

    protected override string WalkStrip => "res://assets/sprites/arctic_fox_enemy/arctic_fox_enemy_walk.png";

    protected override Vector2I FrameSize => new(28, 22);

    protected override MonsterPart[] Parts => new[] { MonsterPart.Fur };

    public override string StealableMove => Abilities.StolenMoveAbility.FoxPounce;

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        var away = (GlobalPosition - target.GlobalPosition).Normalized();
        if (_retreating > 0)
        {
            // Run off (sideways a bit, so it circles round), then come back.
            _retreating -= dt;
            return away.Rotated(0.6f) * ChaseSpeed;
        }

        if (WindUp >= 0)
        {
            WindUp += dt;
            if (WindUp >= WindUpSeconds)
            {
                if (InReach(target, 8f))
                {
                    target.TakeHit(BiteDamage, GlobalPosition);
                }

                WindUp = -1f;
                _retreating = (float)Rng.RandfRange(0.6f, 1.0f);
            }

            return Vector2.Zero;
        }

        if (InReach(target))
        {
            WindUp = 0f;
            return Vector2.Zero;
        }

        return -away * ChaseSpeed;
    }
}
