using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;
using LizardWizard.Players;

namespace LizardWizard.Dungeons;

/// <summary>What a shop counter sells.</summary>
public enum ShopOffer
{
    /// <summary>Nothing at the counter itself (the paint shop's painter mat does the work).</summary>
    None,

    /// <summary>The inn: a rest that fills your hearts.</summary>
    Rest,

    /// <summary>A healing herb.</summary>
    Herb,

    /// <summary>Not open for business yet.</summary>
    ComingSoon,

    /// <summary>The next weapon material for the whole team (see <see cref="WeaponTier"/>).</summary>
    WeaponUpgrade,

    /// <summary>
    /// The herb shop: three spots along the counter, healing herbs on the left (<see cref="ShopCounter.Price"/>),
    /// smelling salts in the middle and fairies on the right (see <see cref="ShopCounter.SaltsPrice"/>,
    /// <see cref="ShopCounter.FairyPrice"/>). Stand at the one you want.
    /// </summary>
    Remedies,
}

/// <summary>
/// A shop's counter, with the shopkeeper (an <see cref="Npc"/> whose Counter is this) behind it. Step up to the front
/// of the counter: the shopkeeper comes over, and after a moment you get what's on offer, paid from the team's
/// <see cref="Purse"/>. Step away and back for another. The node's position is the middle of the counter's front edge.
/// </summary>
public partial class ShopCounter : StaticBody2D
{
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static readonly Color Top = new(0.78f, 0.55f, 0.33f);
    private static readonly Color Front = new(0.55f, 0.33f, 0.22f);
    private static readonly Color Gold = new(0.98f, 0.8f, 0.25f);

    private readonly Dictionary<int, float> _atCounterFor = new();
    private readonly HashSet<int> _served = new();
    private readonly Dictionary<int, int> _spotFor = new();

    [Export] public float Width { get; set; } = 64f;

    [Export] public ShopOffer Offer { get; set; } = ShopOffer.None;

    /// <summary>In coins.</summary>
    [Export] public int Price { get; set; }

    /// <summary>Remedies counter: the price of smelling salts (the herb is <see cref="Price"/>).</summary>
    [Export] public int SaltsPrice { get; set; } = 100;

    /// <summary>Remedies counter: the price of a fairy.</summary>
    [Export] public int FairyPrice { get; set; } = 150;

    /// <summary>The sign over the counter (empty: none).</summary>
    [Export] public string SignText { get; set; } = "";

    /// <summary>How far below the counter's front the sign is.</summary>
    [Export] public float SignY { get; set; } = 16f;

    /// <summary>How long you stand at the counter before buying.</summary>
    [Export] public float StandSeconds { get; set; } = 0.7f;

    /// <summary>The shopkeeper (set by the <see cref="Npc"/> that tends this counter).</summary>
    public Npc? Keeper { get; set; }

    /// <summary>A hero is standing at the counter.</summary>
    public bool HasCustomer { get; private set; }

    private Label? _sign;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, 12) }, Position = new Vector2(0, -6) });
        if (SignText != "" || Offer == ShopOffer.WeaponUpgrade)
        {
            AddChild(_sign = new Label
            {
                Text = SignText,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(-120, SignY),
                Size = new Vector2(240, 20),
                LabelSettings = new LabelSettings { FontSize = 8, FontColor = Gold, OutlineSize = 3, OutlineColor = Outline },
            });
        }

        if (Offer == ShopOffer.Remedies)
        {
            // A price under each spot.
            string[] names = { $"Herb\n{Price}", $"Salts\n{SaltsPrice}", $"Fairy\n{FairyPrice}" };
            for (var i = 0; i < 3; i++)
            {
                AddChild(new Label
                {
                    Text = names[i],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Position = new Vector2((i - 1) * Width / 3 - 18, SignY - 4),
                    Size = new Vector2(36, 24),
                    LabelSettings = new LabelSettings { FontSize = 8, FontColor = Gold, OutlineSize = 3, OutlineColor = Outline, LineSpacing = -2 },
                });
            }
        }
    }

    /// <summary>What the sign says right now (the weapon upgrade's changes as you buy).</summary>
    private string CurrentSign => Offer != ShopOffer.WeaponUpgrade ? SignText
        : WeaponTier.Next is { } next ? $"Step up: {next.Name} weapons for the team, {next.Price} coins"
        : "Your weapons are mithril, the best there is!";

    public override void _PhysicsProcess(double delta)
    {
        if (_sign is not null)
        {
            _sign.Text = CurrentSign;
        }

        var heroes = PlayerManager.Find(this)?.Players.OfType<Hero>().Where(h => h.IsInGame && !h.IsDown).ToList() ?? new List<Hero>();
        HasCustomer = false;
        foreach (var hero in heroes)
        {
            var offset = hero.GlobalPosition - GlobalPosition;
            var atCounter = Mathf.Abs(offset.X) < Width / 2 && offset.Y is > -2 and < 16;
            if (!atCounter)
            {
                _atCounterFor.Remove(hero.PlayerIndex);
                _served.Remove(hero.PlayerIndex);
                continue;
            }

            HasCustomer = true;

            // Moving along to another spot (remedies counter) is a new purchase.
            var spot = offset.X < -Width / 6 ? 0 : offset.X > Width / 6 ? 2 : 1;
            if (_spotFor.GetValueOrDefault(hero.PlayerIndex, spot) != spot)
            {
                _atCounterFor.Remove(hero.PlayerIndex);
                _served.Remove(hero.PlayerIndex);
            }

            _spotFor[hero.PlayerIndex] = spot;
            _atCounterFor[hero.PlayerIndex] = _atCounterFor.GetValueOrDefault(hero.PlayerIndex) + (float)delta;
            if (_atCounterFor[hero.PlayerIndex] >= StandSeconds && _served.Add(hero.PlayerIndex))
            {
                Serve(hero, spot);
            }
        }
    }

    private void Serve(Hero hero, int spot)
    {
        switch (Offer)
        {
            case ShopOffer.Remedies when spot == 0 && Pay(hero, Price):
                hero.AddHerbs(1);
                Speak(hero, "One fresh herb! Thank you!");
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"+1 herb  -{Price} coins", new Color(0.5f, 1f, 0.5f), 1.5f, 8f);
                break;

            case ShopOffer.Remedies when spot == 1 && Pay(hero, SaltsPrice):
                Purse.AddSalts(1);
                Speak(hero, "Smelling salts! Wave them under a knocked-out friend's nose.");
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"+1 smelling salts  -{SaltsPrice} coins", new Color(1f, 0.95f, 0.6f), 1.5f, 8f);
                break;

            case ShopOffer.Remedies when spot == 2 && Pay(hero, FairyPrice):
                Purse.AddFairies(1);
                Speak(hero, "A fairy in a jar! Let it out when you're knocked out.");
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"+1 fairy  -{FairyPrice} coins", new Color(1f, 0.6f, 0.85f), 1.5f, 8f);
                break;

            case ShopOffer.Rest when hero.Health.Current >= hero.Health.Max:
                Speak(hero, "You look well rested already!");
                break;

            case ShopOffer.Rest when Pay(hero):
                hero.Health.Refill();
                Speak(hero, "Sleep well! Your hearts are full.");
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"Rested! -{Price} coins", new Color(0.5f, 1f, 0.5f), 1.5f, 8f);
                break;

            case ShopOffer.Herb when Pay(hero):
                hero.AddHerbs(1);
                Speak(hero, "One fresh herb! Thank you!");
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"+1 herb  -{Price} coins", new Color(0.5f, 1f, 0.5f), 1.5f, 8f);
                break;

            case ShopOffer.ComingSoon:
                Speak(hero, "New weapons and armor are on the way. Come back soon!");
                break;

            case ShopOffer.WeaponUpgrade when WeaponTier.Next is not { } next:
                Speak(hero, "Mithril! Nothing I can make is better than that.");
                break;

            case ShopOffer.WeaponUpgrade when WeaponTier.Upgrade():
                Speak(hero, $"There! Everyone's weapons are {WeaponTier.Current.Name} now!");
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"{WeaponTier.Current.Name} weapons! -{WeaponTier.Current.Price} coins",
                    WeaponTier.Current.Colour, 2f, 8f);
                break;

            case ShopOffer.WeaponUpgrade:
                Speak(hero, $"{WeaponTier.Next!.Name} costs {WeaponTier.Next.Price} coins. You have {Purse.Coins}.");
                break;
        }
    }

    /// <summary>Take the price from the team's purse, or say they can't afford it.</summary>
    private bool Pay(Hero hero) => Pay(hero, Price);

    private bool Pay(Hero hero, int price)
    {
        if (Purse.Spend(price))
        {
            return true;
        }

        Speak(hero, $"That's {price} coins. You have {Purse.Coins}.");
        return false;
    }

    private void Speak(Hero hero, string text)
    {
        if (Keeper is not null)
        {
            Keeper.Say(text);
        }
        else
        {
            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -20), text, Colors.White, 2f, 7f);
        }
    }

    public override void _Draw()
    {
        // A wooden counter: the top, then the front panel down to the floor.
        var half = Width / 2;
        DrawRect(new Rect2(-half, -16, Width, 16), Outline);
        DrawRect(new Rect2(-half + 1, -15, Width - 2, 5), Top);
        DrawRect(new Rect2(-half + 1, -10, Width - 2, 9), Front);
        for (var x = -half + 8; x < half - 4; x += 12)
        {
            DrawLine(new Vector2(x, -9), new Vector2(x, -2), Front.Darkened(0.25f), 1f);
        }

        if (Offer == ShopOffer.Remedies)
        {
            // What's for sale on top of the counter: a herb, a bottle of salts, a fairy in a jar.
            var third = Width / 3;
            DrawCircle(new Vector2(-third, -14), 3f, new Color(0.35f, 0.75f, 0.35f));
            DrawRect(new Rect2(-3, -19, 6, 7), new Color(0.95f, 0.95f, 0.85f));
            DrawRect(new Rect2(-2, -21, 4, 2), new Color(0.55f, 0.35f, 0.25f));
            DrawRect(new Rect2(third - 3, -20, 6, 8), new Color(0.75f, 0.85f, 1f, 0.6f));
            DrawCircle(new Vector2(third, -16), 1.8f, new Color(1f, 0.6f, 0.85f));
            DrawLine(new Vector2(-third / 2, -15), new Vector2(-third / 2, -2), Outline, 1f);
            DrawLine(new Vector2(third / 2, -15), new Vector2(third / 2, -2), Outline, 1f);
        }
    }
}
