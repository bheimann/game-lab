using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;

namespace LizardWizard.Abilities;

/// <summary>
/// The peacock Thief uses the move it stole last (see <see cref="StealAbility"/>), as often as it likes. It keeps the
/// move (the save remembers it) until it steals another enemy's ability, which swaps the move. Moves: the deer's <see cref="DeerKick"/> (a strong kick in front)
/// and the Giant Beetle's <see cref="BeetleCharge"/> (a dash that rams every enemy in the way); also the boar's charge, the arctic
/// fox's pounce (a short, fast dash), the frog's spit (a poison blob) and the snake's poison bite.
/// </summary>
public partial class StolenMoveAbility : Ability
{
    public const string DeerKick = "Deer Kick";
    public const string BeetleCharge = "Beetle Charge";
    public const string BoarCharge = "Boar Charge";
    public const string FoxPounce = "Fox Pounce";
    public const string FrogSpit = "Frog Spit";
    public const string PoisonBite = "Poison Bite";

    private readonly HashSet<IHittable> _hit = new();
    private float _activeLeft;
    private float _flashLeft;
    private Vector2 _hitPoint;
    private Vector2 _direction;

    public StolenMoveAbility()
    {
        Slot = AbilitySlot.Special;
        CooldownSeconds = 0.6f;
    }

    [Export] public int KickDamage { get; set; } = 7;

    [Export] public int ChargeDamage { get; set; } = 6;

    [Export] public float ChargeSpeed { get; set; } = 230f;

    [Export] public float ChargeSeconds { get; set; } = 0.4f;

    /// <summary>The move stolen last ("" = nothing yet).</summary>
    public string Move { get; private set; } = "";

    public override bool IsActive => _activeLeft > 0;

    public override float MoveFactor => Move == DeerKick ? 0.2f : 1f;

    /// <summary>The moves that dash the Thief forward, ramming what's in the way.</summary>
    private bool IsCharge => Move is BeetleCharge or BoarCharge or FoxPounce;

    public override Vector2? Motion => IsActive && IsCharge ? _direction * (Move == FoxPounce ? ChargeSpeed * 1.4f : ChargeSpeed) : null;

    public void Learn(string move) => Move = move;

    protected override void Use()
    {
        var textAt = Hero.HurtCenter + new Vector2(0, -16);
        if (Move == "")
        {
            FloatingText.Spawn(this, textAt, "Steal an ability first (X)", new Color(0.8f, 0.8f, 0.8f), 1f, 7f);
            CooldownLeft = 0.2f;
            return;
        }

        _hit.Clear();
        FloatingText.Spawn(this, textAt, Move, new Color(0.8f, 0.5f, 1f), 0.9f, 7f);

        if (Move == FrogSpit)
        {
            // A slime blob that poisons.
            _activeLeft = 0.2f;
            Bullet.Fire(Hero, Hero.ToGlobal(Hero.AttackOrigin), Hero.Facing,
                new Bullet.Shot(220f, 150f, 3, DamageType.Poison, new Color(0.55f, 0.85f, 0.3f), ShotLook.Pellet));
            return;
        }

        if (IsCharge)
        {
            _activeLeft = Move == FoxPounce ? ChargeSeconds * 0.5f : ChargeSeconds;
            _direction = Hero.Controls.Move.Length() > 0.2f ? Hero.Controls.Move.Normalized() : Hero.Facing;
            Hero.Face(_direction);
            return;
        }

        // Deer Kick (a big hit right in front) or Poison Bite (a smaller one that poisons).
        _activeLeft = 0.3f;
        _flashLeft = 0.15f;
        _hitPoint = Hero.HurtCenter + Hero.Facing * 14f;
        var bite = Move == PoisonBite;
        foreach (var enemy in Hero.EnemyTargets().Where(t => Hits.Touches(t, _hitPoint, 11f)))
        {
            Hero.Hit(enemy, bite ? 3 : KickDamage, Hero.GlobalPosition, bite ? DamageType.Poison : DamageType.Physical);
        }

        Hero.Lunge(Hero.Facing * 5f);
    }

    public override void Cancel() => _activeLeft = 0;

    protected override void Tick(float dt)
    {
        _activeLeft -= dt;
        _flashLeft -= dt;
        if (IsActive && IsCharge)
        {
            // Ram everything in the way, once each.
            foreach (var enemy in Hero.EnemyTargets().Where(t => !_hit.Contains(t) && Hits.Touches(t, Hero.HurtCenter, 8f)))
            {
                _hit.Add(enemy);
                Hero.Hit(enemy, ChargeDamage, Hero.GlobalPosition - _direction * 10f, DamageType.Physical);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_flashLeft > 0)
        {
            var at = ToLocal(_hitPoint);
            DrawCircle(at, 7f, new Color(1, 1, 1, 0.8f));
            DrawArc(at, 9f, 0, Mathf.Tau, 16, new Color(0.8f, 0.5f, 1f), 1.5f);
        }

        if (IsActive && IsCharge)
        {
            // Purple streaks behind the charging peacock.
            for (var i = 1; i <= 3; i++)
            {
                var back = -_direction * (6f * i) + new Vector2(0, -10);
                DrawCircle(back, 3f - i * 0.6f, new Color(0.6f, 0.4f, 0.9f, 0.6f - i * 0.15f));
            }
        }
    }
}
