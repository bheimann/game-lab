using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// Bomb Master: lob a bomb in front of the hero. Switch bomb type with LB / RB or the ring menu.
/// Remote bombs wait on the ground until the bomb button is pressed again.
/// The fox starts with these bomb types; new ones get unlocked by beating bosses (see <see cref="Unlocks"/>).
/// </summary>
public partial class BombAbility : Ability, IHasOptions
{
    private const string BombTexture = "res://assets/kenney/bomb.png";

    /// <summary>Every bomb type, and which boss unlocks it (null = the fox has it from the start).
    /// Add new bombs here with the boss that drops them, e.g. "Giant Beetle".</summary>
    private static readonly (BombKind Kind, string Name, string Description, string? UnlockedBy)[] Kinds =
    {
        (BombKind.Bomb, "Bomb", "Short fuse, big bang", null),
        (BombKind.Smoke, "Smoke Bomb", "Enemies can't see you in the smoke", null),
        (BombKind.Sticky, "Sticky Bomb", "Sticks to the enemy it hits", null),
        (BombKind.Time, "Time Bomb", "Long fuse, bigger blast", null),
        (BombKind.Remote, "Remote Bomb", "Press the bomb button again to blow it up", null),
        (BombKind.Proximity, "Proximity Bomb", "Goes off when an enemy walks near", null),
    };

    private int _selected;

    public BombAbility()
    {
        Slot = AbilitySlot.Secondary;
        CooldownSeconds = 0.6f;
    }

    /// <summary>How far the bomb is thrown, in pixels (shorter if a wall is in the way).</summary>
    [Export] public float ThrowDistance { get; set; } = 48f;

    [Export] public float FlightSeconds { get; set; } = 0.4f;

    [Export] public float FuseSeconds { get; set; } = 1.0f;

    /// <summary>Time bombs' fuse.</summary>
    [Export] public float TimeBombFuseSeconds { get; set; } = 4f;

    /// <summary>Sticky bombs' fuse once they've stuck to something.</summary>
    [Export] public float StickyFuseSeconds { get; set; } = 1.5f;

    /// <summary>Blast radius in pixels.</summary>
    [Export] public float Radius { get; set; } = 28f;

    [Export] public int Damage { get; set; } = 8;

    /// <summary>Time bombs hit harder.</summary>
    [Export] public int TimeBombDamage { get; set; } = 12;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>How many of this hero's bombs can be out at once (armor can add more).</summary>
    [Export] public int MaxBombs { get; set; } = 2;

    public BombKind Kind => Kinds[_selected].Kind;

    public string OptionsTitle => "Bombs";

    public MenuOption[] Options =>
        Kinds.Select((k, i) => IsUnlocked(i)
            ? new MenuOption(k.Name, k.Description, GD.Load<Texture2D>(BombTexture), Bomb.Tint(k.Kind))
            : new MenuOption("???", $"Beat {(k.UnlockedBy == Unlocks.LaterBoss ? k.UnlockedBy : "the " + k.UnlockedBy)} to unlock", GD.Load<Texture2D>(BombTexture), Colors.Black) { Enabled = false })
        .ToArray();

    public int Selected => _selected;

    /// <summary>Names of the bomb types <paramref name="boss"/> unlocks.</summary>
    public static IEnumerable<string> UnlockedBy(string boss) => Kinds.Where(k => k.UnlockedBy == boss).Select(k => k.Name);

    public void Select(int index)
    {
        // Step past locked types in the direction we're going (LB goes back, RB forward).
        var step = index < _selected ? -1 : 1;
        for (var tries = 0; tries < Kinds.Length; tries++)
        {
            index = (index % Kinds.Length + Kinds.Length) % Kinds.Length;
            if (IsUnlocked(index))
            {
                break;
            }

            index += step;
        }

        _selected = index;
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), Kinds[_selected].Name, Bomb.Tint(Kind), 1f, 8f);
    }

    private static bool IsUnlocked(int index) => Kinds[index].UnlockedBy is not { } boss || Unlocks.IsBeaten(boss);

    protected override void Use()
    {
        var mine = Hero.GetParent().GetChildren().OfType<Bomb>().Where(b => b.Thrower == Hero).ToList();

        // Remote bombs out there? This press sets them off instead of throwing.
        var waiting = mine.Where(b => b.IsWaitingForRemote).ToList();
        if (waiting.Count > 0)
        {
            waiting.ForEach(b => b.Detonate());
            return;
        }

        if (mine.Count >= MaxBombs + (Hero.Armor?.ExtraBombs ?? 0))
        {
            CooldownLeft = 0;
            return;
        }

        var from = Hero.GlobalPosition;
        var to = from + Hero.Facing * ThrowDistance;

        // Land in front of a wall rather than going through it.
        var query = PhysicsRayQueryParameters2D.Create(from, to, collisionMask: 1);
        var hit = Hero.GetWorld2D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            to = (Vector2)hit["position"] - Hero.Facing * 6f;
        }

        var bomb = new Bomb
        {
            Thrower = Hero,
            Kind = Kind,
            From = from,
            To = to,
            FlightSeconds = FlightSeconds,
            FuseSeconds = Kind switch
            {
                BombKind.Time => TimeBombFuseSeconds,
                BombKind.Sticky => StickyFuseSeconds,
                _ => FuseSeconds,
            },
            Radius = Kind == BombKind.Time ? Radius * 1.3f : Radius,
            Damage = Kind == BombKind.Time ? TimeBombDamage : Damage,
            DamageType = DamageType,
        };
        Hero.GetParent().AddChild(bomb);
        Hero.Lunge(Hero.Facing * 2f);
    }
}
