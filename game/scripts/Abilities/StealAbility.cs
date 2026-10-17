using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// The peacock Thief's quick grab: robs the enemy right in front of it. LB / RB (or the ring menu) pick what to take:
/// <b>gold</b> (coins for the team's purse), <b>gear</b> (a healing herb for now) or its <b>ability</b> (the Thief can
/// then use that enemy's move until it steals another, see <see cref="StolenMoveAbility"/>). Only enemies that are awake can be robbed, and
/// each one only once of each thing (until it comes back).
/// </summary>
public partial class StealAbility : Ability, IHasOptions
{
    private const string HerbIcon = "res://assets/sprites/icons/herb.png";

    private static readonly Color Grab = new(1f, 0.9f, 0.45f);

    private float _activeLeft;
    private float _flashLeft;
    private Vector2 _grabPoint;
    private int _selected;

    /// <summary>How far in front of the hero it reaches.</summary>
    [Export] public float Reach { get; set; } = 14f;

    [Export] public float Radius { get; set; } = 10f;

    [Export] public float ActiveSeconds { get; set; } = 0.25f;

    public override bool IsActive => _activeLeft > 0;

    public override float MoveFactor => 0.3f;

    public StealKind Kind => (StealKind)_selected;

    public string OptionsTitle => "Steal";

    public MenuOption[] Options => new[]
    {
        new MenuOption("Gold", "Steal coins for the team", null, new Color(0.98f, 0.8f, 0.25f)),
        new MenuOption("Gear", "Steal a healing herb", GD.Load<Texture2D>(HerbIcon), Colors.White),
        new MenuOption("Ability", "Steal the enemy's move: use it with Y", null, new Color(0.8f, 0.5f, 1f)),
    };

    public int Selected => _selected;

    public void Select(int index)
    {
        _selected = (index % 3 + 3) % 3;
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), $"Steal: {Kind}", Colors.White, 1f, 8f);
    }

    protected override void Use()
    {
        _activeLeft = ActiveSeconds;
        _flashLeft = 0.12f;
        _grabPoint = Hero.HurtCenter + Hero.Facing * Reach;
        Hero.Lunge(Hero.Facing * 4f);

        var victim = GetTree().GetNodesInGroup(Enemies.Group).OfType<IStealable>()
            .Where(e => e.IsHostile && e.HurtCenter.DistanceTo(_grabPoint) <= e.HurtRadius + Radius)
            .OrderBy(e => e.HurtCenter.DistanceTo(_grabPoint))
            .FirstOrDefault();
        var textAt = Hero.HurtCenter + new Vector2(0, -16);
        if (victim is null)
        {
            FloatingText.Spawn(this, textAt, "Missed!", new Color(0.8f, 0.8f, 0.8f), 0.7f, 7f);
            return;
        }

        if (!victim.TrySteal(Kind))
        {
            FloatingText.Spawn(this, textAt, Kind == StealKind.Ability ? "Already stolen!" : "Pockets empty!", new Color(0.8f, 0.8f, 0.8f), 0.9f, 7f);
            return;
        }

        switch (Kind)
        {
            case StealKind.Gold:
                Purse.Add(victim.StealableCoins);
                FloatingText.Spawn(this, textAt, $"Stole {victim.StealableCoins} coins!", new Color(0.98f, 0.8f, 0.25f), 1.2f, 8f);
                break;

            case StealKind.Gear:
                Hero.AddHerbs(1);
                FloatingText.Spawn(this, textAt, "Stole a herb!", new Color(0.5f, 1f, 0.5f), 1.2f, 8f);
                break;

            case StealKind.Ability:
                var move = Hero.GetChildren().OfType<StolenMoveAbility>().FirstOrDefault();
                move?.Learn(victim.StealableMove);
                FloatingText.Spawn(this, textAt, $"Stole {victim.StealableMove}!", new Color(0.8f, 0.5f, 1f), 1.4f, 8f);
                break;
        }
    }

    public override void Cancel() => _activeLeft = 0;

    protected override void Tick(float dt)
    {
        _activeLeft -= dt;
        _flashLeft -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_flashLeft > 0)
        {
            // A quick sparkle where the grab lands.
            var at = ToLocal(_grabPoint);
            var size = 2f + (0.12f - _flashLeft) * 30f;
            DrawLine(at + new Vector2(-size, 0), at + new Vector2(size, 0), Grab, 1f);
            DrawLine(at + new Vector2(0, -size), at + new Vector2(0, size), Grab, 1f);
            DrawCircle(at, 1.5f, Colors.White);
        }
    }
}
