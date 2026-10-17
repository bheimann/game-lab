using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Items;

namespace LizardWizard.Effects;

/// <summary>
/// Something an enemy dropped when it was beaten: a few coins (most of the time), or an item: a healing herb or one of
/// the enemy's monster parts (for the moose Tinkerer). A boss also always drops one big boss coin. It pops out
/// onto the ground, and the first hero to walk over it picks it up (it slides over to a hero who's close).
/// Coins go into the team's <see cref="Purse"/>, a herb to whoever picked it up. Loot left lying around blinks and
/// disappears after a while, or when the team leaves the room.
/// </summary>
public partial class Loot : Node2D
{
    /// <summary>Chance a drop is coins rather than an item.</summary>
    public const float CoinChance = 0.8f;

    private const string HerbIcon = "res://assets/sprites/icons/herb.png";
    private const float LifeSeconds = 30f;
    private const float PickUpDistance = 9f;
    private const float PullDistance = 24f;

    private static readonly Color Gold = new(0.98f, 0.8f, 0.25f);
    private static readonly Color DarkGold = new(0.72f, 0.48f, 0.12f);
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static readonly Color BossRed = new(1f, 0.35f, 0.35f);

    private Vector2 _from;
    private Vector2 _to;
    private float _time;
    private Texture2D? _herb;

    /// <summary>Coins in this pile (0 = it's a healing herb).</summary>
    public int Coins { get; private set; }

    /// <summary>A boss's special coin (one per boss).</summary>
    public bool IsBossCoin { get; private set; }

    /// <summary>A monster part (goes into the team's bag).</summary>
    public MonsterPart? Part { get; private set; }

    public bool IsHerb => Coins == 0 && !IsBossCoin && Part is null;

    /// <summary>Drop <paramref name="rolls"/> pieces of loot around <paramref name="enemy"/>'s feet
    /// (80% a pile of 1-3 coins, 20% an item: half the time a healing herb, half the time one of <paramref name="parts"/>,
    /// the enemy's monster parts). With <paramref name="bossCoin"/>, one of them is always a boss coin instead.</summary>
    public static void DropFrom(Node2D enemy, int rolls = 1, bool bossCoin = false, MonsterPart[]? parts = null)
    {
        if (bossCoin)
        {
            Drop(enemy, enemy.GlobalPosition, 0, bossCoin: true);
            rolls--;
        }

        for (var i = 0; i < rolls; i++)
        {
            if (GD.Randf() < CoinChance)
            {
                Drop(enemy, enemy.GlobalPosition, GD.RandRange(1, 3));
            }
            else if (parts is { Length: > 0 } && GD.Randf() < 0.5f)
            {
                Drop(enemy, enemy.GlobalPosition, 0, part: parts[GD.RandRange(0, parts.Length - 1)]);
            }
            else
            {
                Drop(enemy, enemy.GlobalPosition, 0); // a herb
            }
        }
    }

    /// <summary>Drop one pile of <paramref name="coins"/> (0 = a healing herb), a boss coin or a monster part, that pops
    /// out from <paramref name="at"/>.</summary>
    public static Loot Drop(Node2D near, Vector2 at, int coins, bool bossCoin = false, MonsterPart? part = null)
    {
        var loot = new Loot { Coins = bossCoin || part is not null ? 0 : coins, IsBossCoin = bossCoin, Part = part, _from = at };
        loot._to = at + Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * (float)GD.RandRange(8.0, 18.0);
        near.GetParent().AddChild(loot);
        loot.GlobalPosition = at;
        return loot;
    }

    public override void _Ready()
    {
        // Herbs and parts show their icon.
        _herb = Part?.Icon ?? (IsHerb ? GD.Load<Texture2D>(HerbIcon) : null);
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        if (_time >= LifeSeconds)
        {
            QueueFree();
            return;
        }

        // Pop out in a little hop first; it can't be picked up mid-air.
        const float hop = 0.35f;
        if (_time < hop)
        {
            GlobalPosition = _from.Lerp(_to, _time / hop);
            QueueRedraw();
            return;
        }

        var hero = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .Where(h => h.IsInGame && !h.IsDown)
            .OrderBy(h => h.GlobalPosition.DistanceTo(GlobalPosition))
            .FirstOrDefault();
        if (hero is not null)
        {
            var distance = hero.GlobalPosition.DistanceTo(GlobalPosition);
            if (distance <= PickUpDistance)
            {
                PickUp(hero);
                return;
            }

            if (distance <= PullDistance)
            {
                GlobalPosition = GlobalPosition.MoveToward(hero.GlobalPosition, 90f * (float)delta);
            }
        }

        QueueRedraw();
    }

    private void PickUp(Hero hero)
    {
        GD.Print($"{hero.Name} picked up {(IsBossCoin ? "the boss coin" : Part is not null ? $"a part: {Part.Name}" : IsHerb ? "a healing herb" : $"{Coins} coin(s)")}");
        if (Part is not null)
        {
            MonsterParts.Add(Part);
            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"+1 {Part.Name.ToLowerInvariant()}", new Color(0.93f, 0.85f, 0.6f), 1.2f, 8f);
        }
        else if (IsBossCoin)
        {
            Purse.AddBossCoin();
            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -16), "Boss coin!", BossRed, 2f, 10f);
        }
        else if (IsHerb)
        {
            hero.AddHerbs(1);
            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), "+1 herb", new Color(0.5f, 1f, 0.5f), 1f, 8f);
        }
        else
        {
            Purse.Add(Coins);
            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), Coins == 1 ? "+1 coin" : $"+{Coins} coins", Gold, 1f, 8f);
        }

        QueueFree();
    }

    public override void _Draw()
    {
        // Blink for the last few seconds before it disappears.
        if (_time > LifeSeconds - 4f && (int)(_time * 8) % 2 == 0)
        {
            return;
        }

        const float hop = 0.35f;
        var lift = _time < hop ? Mathf.Sin(_time / hop * Mathf.Pi) * 10f : 0f;

        // Shadow.
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, 4f, new Color(0, 0, 0, 0.25f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        if (IsBossCoin)
        {
            // A big gold coin with a red star, bobbing and glinting.
            var centre = new Vector2(0, -7f - lift - Mathf.Sin(_time * 3f) * 1.5f);
            DrawCircle(centre, 6.5f, Outline);
            DrawCircle(centre, 5.5f, DarkGold);
            DrawCircle(centre - new Vector2(0.6f, 0.6f), 4.5f, Gold);
            var star = Enumerable.Range(0, 10)
                .Select(i => centre + Vector2.Up.Rotated(i * Mathf.Pi / 5) * (i % 2 == 0 ? 3.5f : 1.5f)).ToArray();
            DrawColoredPolygon(star, BossRed);
            var glint = (_time * 1.5f) % 2f;
            if (glint < 0.4f)
            {
                var at = centre + new Vector2(3, -3);
                var size = 2.5f * Mathf.Sin(glint / 0.4f * Mathf.Pi);
                DrawLine(at - new Vector2(size, 0), at + new Vector2(size, 0), Colors.White, 1f);
                DrawLine(at - new Vector2(0, size), at + new Vector2(0, size), Colors.White, 1f);
            }

            return;
        }

        if (_herb is not null)
        {
            var bob = lift + 1.5f + Mathf.Sin(_time * 4f) * 1f;
            DrawTexture(_herb, new Vector2(-_herb.GetWidth() / 2f, -_herb.GetHeight() - bob));
            return;
        }

        // A little stack of spinning coins (up to 3 drawn).
        for (var i = 0; i < Mathf.Min(Coins, 3); i++)
        {
            var at = new Vector2((i - (Mathf.Min(Coins, 3) - 1) / 2f) * 3f, -4f - lift - i % 2);
            var spin = Mathf.Max(0.25f, Mathf.Abs(Mathf.Cos(_time * 3f + i)));
            DrawSetTransform(at, 0, new Vector2(spin, 1f));
            DrawCircle(Vector2.Zero, 3.2f, Outline);
            DrawCircle(Vector2.Zero, 2.4f, DarkGold);
            DrawCircle(new Vector2(-0.4f, -0.4f), 1.8f, Gold);
            DrawRect(new Rect2(-1f, -1.5f, 0.8f, 1.2f), Colors.White with { A = 0.8f });
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
