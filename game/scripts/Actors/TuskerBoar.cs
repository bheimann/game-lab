using System.Linq;
using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Actors;

/// <summary>
/// <b>Tusker</b>, the boss boar of Frostfang Fortress, who fights alongside <see cref="FrostMoose"/>: a big boar whose
/// charges go for whichever hero the moose's frost has chilled (they can't get out of the way as fast). When the moose
/// is beaten it gets angry: faster charges and less rest in between.
/// </summary>
public partial class TuskerBoar : Boar, IBoss, Saving.ISaveable
{
    private bool _angry;

    [Export] public string BossName { get; set; } = "Tusker the Boar";

    /// <summary>The moose it fights alongside.</summary>
    [Export] public NodePath? Partner { get; set; }

    public TuskerBoar()
    {
        MinHealth = 100;
        MaxHealth = 100;
        SightRange = 220f;
        LoseSightRange = 500f;
        ChargeDamage = 5;
        ChargeSpeed = 190f;
        RestSeconds = 1.2f;
    }

    public bool IsFighting => CurrentMood == Mood.Chasing;

    public override bool CanBeCharmed => false;

    public override bool CanBePulled => false;

    public override float HurtRadius => 15f;

    protected override float BodyHeight => 30f;

    protected override Vector2 SpriteScale => new(1.5f, 1.5f);

    protected override Color BaseTint => _angry ? new Color(1f, 0.75f, 0.7f) : new Color(0.85f, 0.9f, 1.05f);

    public override int StealableCoins => 10;

    public override void Respawn()
    {
    }

    public string? SaveState() => IsHostile ? null : "Beaten";

    public void LoadState(string state) => SetBeaten();

    protected override void DropLoot() => BossLoot.Drop(this, Partner);

    /// <summary>Go for a chilled hero if there is one: the moose's frost sets them up for the charge.</summary>
    protected override IEnemyTarget ChargeAt(IEnemyTarget target) =>
        Heroes().Where(h => h.IsChilled && h.IsVisibleToEnemies && h.GlobalPosition.DistanceTo(GlobalPosition) < 220f)
            .OrderBy(h => h.GlobalPosition.DistanceTo(GlobalPosition))
            .Cast<IEnemyTarget>().FirstOrDefault() ?? target;

    protected override Vector2 Fight(float dt, IEnemyTarget target)
    {
        if (!_angry && BossLoot.PartnerBeaten(this, Partner))
        {
            _angry = true;
            ChargeSpeed *= 1.3f;
            RestSeconds *= 0.5f;
            WindUpSeconds *= 0.7f;
            FloatingText.Spawn(this, TextAt, "Tusker is angry!", new Color(1f, 0.5f, 0.4f), 2f, 9f);
        }

        return base.Fight(dt, target);
    }
}
