using System;
using System.Linq;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// Shoots something that flies straight: a gun's bullet, spit, a thrown javelin, an arrow. A little aim assist
/// bends the shot toward an enemy that's nearly in front of the hero. Shots also flip levers.
/// With more than one of <see cref="Elements"/>, LB / RB (or the ring menu) switch the element.
/// "Explosive" is also allowed as an element: the shot makes a small explosion where it lands.
/// </summary>
public partial class GunAbility : Ability, IHasOptions
{
    private const string Explosive = "Explosive";

    private float _activeLeft;
    private float _flashLeft;
    private int _selected;

    [Export] public int Damage { get; set; } = 2;

    [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

    /// <summary>Elements to switch between, comma-separated (e.g. "Physical, Fire, Ice, Lightning, Arcane, Explosive").
    /// Empty = always <see cref="DamageType"/>.</summary>
    [Export] public string Elements { get; set; } = "";

    /// <summary>Name for the ring menu, e.g. "Bullets" or "Arrows".</summary>
    [Export] public string OptionsTitle { get; set; } = "Bullets";

    /// <summary>What the shot looks like.</summary>
    [Export] public ShotLook Look { get; set; } = ShotLook.Pellet;

    /// <summary>How far a shot flies, in pixels.</summary>
    [Export] public float Range { get; set; } = 160f;

    [Export] public float BulletSpeed { get; set; } = 320f;

    /// <summary>Explosive shots: blast size and damage.</summary>
    [Export] public float ExplosionRadius { get; set; } = 16f;

    [Export] public int ExplosionDamage { get; set; } = 4;

    /// <summary>Aim assist: shots bend toward an enemy within this many degrees of where the hero faces.</summary>
    [Export] public float AimAssistDegrees { get; set; } = 15f;

    /// <summary>How long the hero stands still after a shot.</summary>
    [Export] public float ActiveSeconds { get; set; } = 0.12f;

    [Export] public float MoveFactorWhileActive { get; set; } = 0.4f;

    [Export] public Color Colour { get; set; } = new(1f, 0.85f, 0.3f);

    public override bool IsActive => _activeLeft > 0;

    public override float MoveFactor => MoveFactorWhileActive;

    /// <summary>The element selected now (explosive shots do plain damage plus the blast).</summary>
    public DamageType Element => ElementList.Length == 0 ? DamageType : TypeOf(ElementList[_selected]);

    public bool IsExplosive => ElementList.Length > 0 && ElementList[_selected] == Explosive;

    public MenuOption[] Options => ElementList.Select(e => new MenuOption(NameOf(e), Describe(e), null, ColourOf(e))).ToArray();

    public int Selected => _selected;

    private string[] ElementList => Elements.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private Color ShotColour => ElementList.Length == 0 ? Colour : ColourOf(ElementList[_selected]);

    public void Select(int index)
    {
        var elements = ElementList;
        if (elements.Length == 0)
        {
            return;
        }

        _selected = (index % elements.Length + elements.Length) % elements.Length;
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), NameOf(elements[_selected]), ShotColour, 1f, 8f);
    }

    /// <summary>"Normal" for plain physical damage, otherwise the element's name.</summary>
    public static string ElementName(DamageType type) => type == DamageType.Physical ? "Normal" : type.ToString();

    public static Color ElementColour(DamageType type) => type switch
    {
        DamageType.Fire => new Color(1f, 0.45f, 0.2f),
        DamageType.Ice => new Color(0.55f, 0.85f, 1f),
        DamageType.Lightning => new Color(1f, 1f, 0.45f),
        DamageType.Arcane => new Color(0.8f, 0.5f, 1f),
        DamageType.Poison => new Color(0.5f, 0.9f, 0.3f),
        _ => new Color(1f, 0.85f, 0.3f),
    };

    protected override void Use()
    {
        _activeLeft = ActiveSeconds;
        _flashLeft = 0.06f;

        var muzzle = Hero.ToGlobal(Hero.AttackOrigin);
        var assisted = Hero.EnemyTargets()
            .Select(t => t.HurtCenter - muzzle)
            .Where(d => d.Length() <= Range && Mathf.RadToDeg(Mathf.Abs(Hero.Facing.AngleTo(d))) <= AimAssistDegrees)
            .Where(d => Hero.ClearPath(muzzle, muzzle + d * 0.9f))
            .OrderBy(d => d.Length())
            .FirstOrDefault();
        var direction = assisted == Vector2.Zero ? Hero.Facing : assisted.Normalized();

        Bullet.Fire(Hero, muzzle, direction, new Bullet.Shot(BulletSpeed, Range, Damage, Element, ShotColour, Look,
            IsExplosive ? ExplosionRadius : 0, ExplosionDamage));
        Hero.Lunge(-direction * 2f); // kick back
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
        if (_flashLeft > 0 && Look == ShotLook.Pellet)
        {
            var at = Hero.AttackOrigin + Hero.Facing * 2f;
            DrawCircle(at, 3f, ShotColour);
            DrawCircle(at, 1.5f, Colors.White);
        }
    }

    private static DamageType TypeOf(string element) => element == Explosive ? DamageType.Physical : Enum.Parse<DamageType>(element);

    private static string NameOf(string element) => element == Explosive ? Explosive : ElementName(TypeOf(element));

    private static Color ColourOf(string element) => element == Explosive ? new Color(1f, 0.55f, 0.2f) : ElementColour(TypeOf(element));

    private static string Describe(string element) => TypeOf(element) switch
    {
        _ when element == Explosive => "Makes a small explosion",
        DamageType.Poison => "Keeps hurting for a while",
        DamageType.Fire => "Burns for a while",
        DamageType.Ice => "Slows the enemy down",
        DamageType.Lightning or DamageType.Arcane => "Double damage",
        _ => "Normal damage",
    };
}
