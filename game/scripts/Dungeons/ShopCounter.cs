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

    /// <summary>Not open for business yet (the weapon and armor shop).</summary>
    ComingSoon,
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

    [Export] public float Width { get; set; } = 64f;

    [Export] public ShopOffer Offer { get; set; } = ShopOffer.None;

    /// <summary>In coins.</summary>
    [Export] public int Price { get; set; }

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

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, 12) }, Position = new Vector2(0, -6) });
        if (SignText != "")
        {
            AddChild(new Label
            {
                Text = SignText,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(-120, SignY),
                Size = new Vector2(240, 20),
                LabelSettings = new LabelSettings { FontSize = 8, FontColor = Gold, OutlineSize = 3, OutlineColor = Outline },
            });
        }
    }

    public override void _PhysicsProcess(double delta)
    {
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
            _atCounterFor[hero.PlayerIndex] = _atCounterFor.GetValueOrDefault(hero.PlayerIndex) + (float)delta;
            if (_atCounterFor[hero.PlayerIndex] >= StandSeconds && _served.Add(hero.PlayerIndex))
            {
                Serve(hero);
            }
        }
    }

    private void Serve(Hero hero)
    {
        switch (Offer)
        {
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
        }
    }

    /// <summary>Take the price from the team's purse, or say they can't afford it.</summary>
    private bool Pay(Hero hero)
    {
        if (Purse.Spend(Price))
        {
            return true;
        }

        Speak(hero, $"That's {Price} coins. You have {Purse.Coins}.");
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
    }
}
