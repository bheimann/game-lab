using System.Linq;
using Godot;
using LizardWizard.Dungeons;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// <b>Mossimus Maximus</b>, the final boss: the deer who tricked the kingdoms into fighting the lizards, waiting in
/// Mossmire Keep under Tailspin Swamp. He keeps his distance and flashes, then throws a fan of moss bolts; every few
/// seconds he vanishes in a puff of smoke and pops up somewhere else in the room (a trickster to the end); up close
/// he kicks. At half health he gets angry: faster, more bolts, and a ring of bolts all round him now and then.
/// </summary>
public partial class Mossimus : Beast, IBoss, Saving.ISaveable
{
    /// <summary>What beating him counts as for unlocks (one more fuse slot).</summary>
    public const string Boss = "Mossmire Keep";

    private const float BlinkSeconds = 1f;

    private static readonly string[] Defeated =
    {
        "Fine, FINE! No more tricks. The kingdoms are free!",
        "Beaten by lizards... I'll go back to being a plain old deer.",
    };

    private float _volleyWait = 2f;
    private float _blinkWait = 6f;
    private float _blink = -1f;
    private bool _volley;
    private int _volleys;
    private bool _angry;

    [Export] public string BossName { get; set; } = "Mossimus Maximus";

    [Export] public int BoltDamage { get; set; } = 3;

    [Export] public int KickDamage { get; set; } = 4;

    public Mossimus()
    {
        MinHealth = 220;
        MaxHealth = 220;
        ChaseSpeed = 34f;
        WanderSpeed = 8f;
        SightRange = 240f;
        LoseSightRange = 600f;
    }

    public bool IsFighting => CurrentMood == Mood.Chasing;

    public override bool CanBeCharmed => false;

    public override bool CanBePulled => false;

    public override float HurtRadius => 16f;

    /// <summary>From the swamp: weak to lightning, resists fire.</summary>
    protected override Combat.Climate? NativeClimate => Combat.Climate.Wet;

    protected override float BodyHeight => 52f;

    protected override string WalkStrip => "res://assets/sprites/mossimus/mossimus_walk.png";

    protected override Vector2I FrameSize => new(40, 42);

    protected override Vector2 SpriteScale => new(1.6f, 1.6f);

    protected override Color BaseTint => _angry ? new Color(1f, 0.8f, 0.85f) : Colors.White;

    /// <summary>Fades out and back in while he blinks across the room.</summary>
    protected override float Alpha => _blink < 0 ? 1f : Mathf.Abs(_blink - BlinkSeconds / 2) / (BlinkSeconds / 2);

    protected override string[] ThankLines => Defeated;

    protected override MonsterPart[] Parts => new[] { MonsterPart.Antler, MonsterPart.Hide };

    public override string StealableMove => Abilities.StolenMoveAbility.DeerKick;

    public override int StealableCoins => 15;

    /// <summary>Bosses stay beaten.</summary>
    public override void Respawn()
    {
    }

    public string? SaveState() => IsHostile ? null : "Beaten";

    public void LoadState(string state) => SetBeaten();

    protected override void DropLoot()
    {
        Loot.DropFrom(this, rolls: 4, bossCoin: true, parts: Parts);
        Unlocks.BossBeaten(this, Boss);
    }

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        if (!_angry && Health.Current <= Health.Max / 2)
        {
            _angry = true;
            ChaseSpeed *= 1.4f;
            FloatingText.Spawn(this, TextAt, "Enough games, lizards!", new Color(0.8f, 0.5f, 1f), 2.5f, 9f);
        }

        // Blinking: fade out, jump somewhere else in the room halfway through, fade back in.
        if (_blink >= 0)
        {
            var before = _blink;
            _blink += dt;
            if (before < BlinkSeconds / 2 && _blink >= BlinkSeconds / 2)
            {
                Puff();
                GlobalPosition = BlinkSpot();
                ResetPhysicsInterpolation();
                Puff();
            }

            if (_blink >= BlinkSeconds)
            {
                _blink = -1f;
                _volleyWait = Mathf.Min(_volleyWait, 0.6f); // straight into an attack
            }

            return Vector2.Zero;
        }

        // Winding up: a volley of moss bolts, or a kick.
        if (WindUp >= 0)
        {
            WindUp += dt;
            if (_volley && WindUp >= (_angry ? 0.5f : 0.7f))
            {
                Volley(target);
                WindUp = -1f;
                _volley = false;
                _volleyWait = _angry ? 1.6f : 2.4f;
            }
            else if (!_volley && WindUp >= 0.45f)
            {
                if (InReach(target, 8f))
                {
                    target.TakeHit(KickDamage, GlobalPosition);
                }

                WindUp = -1f;
                AttackCooldown = _angry ? 0.8f : 1.2f;
            }

            return Vector2.Zero;
        }

        _blinkWait -= dt;
        if (_blinkWait <= 0)
        {
            _blinkWait = _angry ? 4.5f : 6.5f;
            _blink = 0f;
            if (Rng.Randf() < 0.5f)
            {
                FloatingText.Spawn(this, TextAt, Rng.Randf() < 0.5f ? "Over here!" : "Hee hee!", new Color(0.8f, 0.5f, 1f), 1.2f, 8f);
            }

            return Vector2.Zero;
        }

        var toTarget = target.GlobalPosition - GlobalPosition;
        var distance = toTarget.Length();
        if (InReach(target, 4f) && AttackCooldown <= 0)
        {
            WindUp = 0f;
            return Vector2.Zero;
        }

        _volleyWait -= dt;
        if (_volleyWait <= 0 && distance < 200f)
        {
            _volley = true;
            WindUp = 0f;
            return Vector2.Zero;
        }

        // Between attacks: keep 70-120 pixels away, circling round.
        var dir = toTarget.Normalized();
        return (distance < 70f ? -dir : distance > 120f ? dir : dir.Orthogonal() * 0.6f) * ChaseSpeed;
    }

    protected override void StopAttack()
    {
        base.StopAttack();
        _volley = false;
        _blink = -1f;
    }

    /// <summary>A fan of bolts at the target (more when he's angry), and every other time an angry ring all round.</summary>
    private void Volley(IEnemyTarget target)
    {
        var aim = target.HurtCenter - HurtCenter;
        var count = _angry ? 5 : 3;
        for (var i = 0; i < count; i++)
        {
            EnemyShot.Fire(this, HurtCenter, aim.Rotated((i - (count - 1) / 2f) * 0.28f), BoltDamage, IsCharmed);
        }

        _volleys++;
        if (_angry && _volleys % 2 == 0)
        {
            for (var i = 0; i < 10; i++)
            {
                EnemyShot.Fire(this, HurtCenter, Vector2.Right.Rotated(i * Mathf.Tau / 10 + 0.15f), BoltDamage, IsCharmed);
            }
        }
    }

    /// <summary>Somewhere on the floor, well away from the heroes (but not the far corner of the room).</summary>
    private Vector2 BlinkSpot()
    {
        if (DungeonRoom.Of(this) is not { } room)
        {
            return GlobalPosition;
        }

        var floor = room.FloorRect.Grow(-36);
        var heroes = Heroes();
        var best = GlobalPosition;
        var bestScore = float.MinValue;
        for (var i = 0; i < 12; i++)
        {
            var spot = new Vector2(Rng.RandfRange(floor.Position.X, floor.End.X), Rng.RandfRange(floor.Position.Y + 20, floor.End.Y));
            var nearest = heroes.Length == 0 ? 100f : heroes.Min(h => h.GlobalPosition.DistanceTo(spot));
            var score = Mathf.Min(nearest, 150f) - spot.DistanceTo(GlobalPosition) * 0.05f;
            if (score > bestScore)
            {
                bestScore = score;
                best = spot;
            }
        }

        return best;
    }

    private void Puff() =>
        GetParent().AddChild(new SmokeCloud { Radius = 22f, Seconds = 0.5f, Position = Position });
}
