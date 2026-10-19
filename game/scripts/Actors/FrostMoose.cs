using System.Linq;
using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// Frostfang Fortress's bosses are a pair that fight together: <b>Frostmane</b>, this big moose, stomps the ice to send out
/// a ring of frost that chills heroes (they walk at half speed), and <b>Tusker</b>, the boar (see <see cref="TuskerBoar"/>),
/// charges whoever is chilled. Up close the moose swings its antlers. When one of them is beaten, the other gets angry:
/// faster, and it attacks more often. Beating both completes the dungeon.
/// </summary>
public partial class FrostMoose : Beast, IBoss, Saving.ISaveable
{
    /// <summary>What both bosses count as for unlocks (one more fuse slot, the Hoversled).</summary>
    public const string Boss = "Frostfang Fortress";

    private float _stompWait = 2.5f;
    private bool _stomping;
    private bool _angry;

    [Export] public string BossName { get; set; } = "Frostmane the Moose";

    /// <summary>The boar it fights alongside.</summary>
    [Export] public NodePath? Partner { get; set; }

    [Export] public int SwipeDamage { get; set; } = 4;

    public FrostMoose()
    {
        MinHealth = 140;
        MaxHealth = 140;
        ChaseSpeed = 24f;
        WanderSpeed = 10f;
        SightRange = 220f;
        LoseSightRange = 500f;
    }

    public bool IsFighting => CurrentMood == Mood.Chasing;

    public override bool CanBeCharmed => false;

    public override bool CanBePulled => false; // far too heavy

    public override float HurtRadius => 18f;

    /// <summary>From the snow: weak to fire, resists ice, wherever it is.</summary>
    protected override Combat.Climate? NativeClimate => Combat.Climate.Cold;

    protected override float BodyHeight => 54f;

    protected override string WalkStrip => "res://assets/sprites/frost_moose/frost_moose_walk.png";

    protected override Vector2I FrameSize => new(44, 36);

    protected override Vector2 SpriteScale => new(1.7f, 1.7f);

    protected override Color BaseTint => _angry ? new Color(1f, 0.8f, 0.8f) : new Color(0.9f, 0.95f, 1.1f);

    protected override MonsterPart[] Parts => new[] { MonsterPart.Antler, MonsterPart.Hide };

    public override string StealableMove => Abilities.StolenMoveAbility.DeerKick;

    public override int StealableCoins => 10;

    /// <summary>Bosses stay beaten.</summary>
    public override void Respawn()
    {
    }

    public string? SaveState() => IsHostile ? null : "Beaten";

    public void LoadState(string state) => SetBeaten();

    protected override void DropLoot() => BossLoot.Drop(this, Partner);

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        if (!_angry && BossLoot.PartnerBeaten(this, Partner))
        {
            _angry = true;
            ChaseSpeed *= 1.5f;
            FloatingText.Spawn(this, TextAt, "Frostmane is angry!", new Color(1f, 0.5f, 0.4f), 2f, 9f);
        }

        // The stomp: rear up (flashing), then slam down and send out a ring of frost.
        if (_stomping)
        {
            WindUp += dt;
            if (WindUp >= 0.9f)
            {
                var wave = new IceWave { MaxRadius = _angry ? 130f : 100f };
                GetParent().AddChild(wave);
                wave.GlobalPosition = GlobalPosition;
                Players.CoopCamera.ShakeAll(this, 4f);
                WindUp = -1f;
                _stomping = false;
                _stompWait = _angry ? 2.5f : 4.5f;
            }

            return Vector2.Zero;
        }

        _stompWait -= dt;
        if (_stompWait <= 0)
        {
            _stomping = true;
            WindUp = 0f;
            return Vector2.Zero;
        }

        // Antler swipe up close.
        if (WindUp >= 0)
        {
            WindUp += dt;
            if (WindUp >= 0.5f)
            {
                if (InReach(target, 10f))
                {
                    target.TakeHit(SwipeDamage, GlobalPosition);
                }

                WindUp = -1f;
                AttackCooldown = _angry ? 0.9f : 1.4f;
            }

            return Vector2.Zero;
        }

        if (InReach(target, 4f))
        {
            if (AttackCooldown <= 0)
            {
                WindUp = 0f;
            }

            return Vector2.Zero;
        }

        return (target.GlobalPosition - GlobalPosition).Normalized() * ChaseSpeed;
    }

    protected override void StopAttack()
    {
        base.StopAttack();
        _stomping = false;
    }
}

/// <summary>Loot for the Frostfang Fortress pair: each drops its own parts and a few things; whichever falls last also
/// drops the boss coin, and beating them counts as one boss (one more fuse slot, the Hoversled for the Vehicle Master).</summary>
public static class BossLoot
{
    public static bool PartnerBeaten(Node2D boss, NodePath? partner) =>
        partner is not null && !partner.IsEmpty && boss.GetNodeOrNull<Beast>(partner) is { IsHostile: false };

    public static void Drop(Beast boss, NodePath? partner)
    {
        var last = PartnerBeaten(boss, partner) || partner is null || partner.IsEmpty;
        Loot.DropFrom(boss, rolls: last ? 4 : 2, bossCoin: last, parts: boss is TuskerBoar ? new[] { MonsterPart.Tusk } : new[] { MonsterPart.Antler, MonsterPart.Hide });
        if (last)
        {
            Unlocks.BossBeaten(boss, FrostMoose.Boss);
        }
    }
}
