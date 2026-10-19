using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.UI;

/// <summary>
/// The dungeon's part of the screen: keys and the team's coins (top middle; the very middle, where the views meet,
/// with 3 or 4 split-screen views), the boss's health bar while it fights (at the bottom of the view of every hero in
/// its room), and big banners for room names (in the view of the hero who walked in) and "Dungeon complete!" (across
/// the whole screen).
/// </summary>
public partial class DungeonHud : Control
{
    private const string KeyIcon = "res://assets/kenney/key.png";
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);

    private Texture2D _key = null!;
    private readonly List<Banner> _banners = new();

    private sealed class Banner
    {
        public required Hero? Hero { get; init; }
        public required string Title { get; init; }
        public required string Subtitle { get; init; }
        public required float Seconds { get; init; }
        public float Age { get; set; }
    }

    public Dungeon Dungeon { get; set; } = null!;

    public override void _Ready() => _key = GD.Load<Texture2D>(KeyIcon);

    /// <summary>A big banner in <paramref name="hero"/>'s view, or across the whole screen (replacing every other banner)
    /// without one. An empty title just clears.</summary>
    public void ShowBanner(string title, string subtitle, float seconds, Hero? hero = null)
    {
        _banners.RemoveAll(b => hero is null || b.Hero == hero);
        if (title != "" && seconds > 0)
        {
            _banners.Add(new Banner { Hero = hero, Title = title, Subtitle = subtitle, Seconds = seconds });
        }
    }

    public override void _Process(double delta)
    {
        foreach (var banner in _banners)
        {
            banner.Age += (float)delta;
        }

        _banners.RemoveAll(b => b.Age >= b.Seconds);
        QueueRedraw();
    }

    /// <summary>The part of the screen that's <paramref name="hero"/>'s (the whole screen for nobody, or without split screen).</summary>
    private Rect2 AreaOf(Hero? hero) =>
        hero is not null && SplitScreen.Find(this)?.ViewOf(hero) is { } view ? view.ScreenRect : GetViewportRect();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var width = GetViewportRect().Size.X;
        var top = Players.SplitScreen.Find(this) is { ViewCount: >= 3 } ? GetViewportRect().Size.Y / 2 - 9 : 6;

        // Keys.
        if (Dungeon.Keys > 0)
        {
            var at = new Vector2(width / 2 - 18, top);
            DrawRect(new Rect2(at - new Vector2(4, 2), new Vector2(44, 18)), Outline with { A = 0.7f });
            DrawTextureRect(_key, new Rect2(at + new Vector2(0, 1), _key.GetSize()), false);
            Text($"x{Dungeon.Keys}", at + new Vector2(20, 12), Colors.White, 10, left: true);
        }

        // Coins (next to the keys, if there are any).
        if (Items.Purse.Coins > 0)
        {
            var label = $"x{Items.Purse.Coins}";
            var boxWidth = 24 + font.GetStringSize(label, HorizontalAlignment.Left, -1, 10).X;
            var at = new Vector2(Dungeon.Keys > 0 ? width / 2 + 32 : width / 2 - boxWidth / 2, top);
            DrawRect(new Rect2(at - new Vector2(4, 2), new Vector2(boxWidth + 4, 18)), Outline with { A = 0.7f });
            var coin = at + new Vector2(6, 7);
            DrawCircle(coin, 5f, Outline);
            DrawCircle(coin, 4f, new Color(0.72f, 0.48f, 0.12f));
            DrawCircle(coin - new Vector2(0.6f, 0.6f), 3f, new Color(0.98f, 0.8f, 0.25f));
            Text(label, at + new Vector2(16, 12), Colors.White, 10, left: true);
        }

        // The team's weapon material, under the coins.
        if (Items.WeaponTier.Level > 0)
        {
            var tier = Items.WeaponTier.Current;
            Text($"{tier.Name} weapons", new Vector2(width / 2, top + 28), tier.Colour, 8);
        }

        // Boss coins (to the left of the keys).
        if (Items.Purse.BossCoins > 0)
        {
            var at = new Vector2(width / 2 - 62, top);
            DrawRect(new Rect2(at - new Vector2(4, 2), new Vector2(40, 18)), Outline with { A = 0.7f });
            var coin = at + new Vector2(6, 7);
            DrawCircle(coin, 6f, Outline);
            DrawCircle(coin, 5f, new Color(0.98f, 0.8f, 0.25f));
            DrawCircle(coin, 2f, new Color(1f, 0.35f, 0.35f));
            Text($"x{Items.Purse.BossCoins}", at + new Vector2(16, 12), Colors.White, 10, left: true);
        }

        // Boss health, at the bottom of each view with a boss fighting in it (stacked if there are two).
        var areas = SplitScreen.Find(this) is { Shared: false } split
            ? split.Views.Where(v => v.Camera.Follow is not null).Select(v => (v.ScreenRect, Dungeon.RoomOf(v.Camera.Follow!)))
            : new[] { (GetViewportRect(), Dungeon.CurrentRoom) };
        foreach (var (area, room) in areas)
        {
            var bosses = room.FindChildren("*", "", true, false).OfType<IBoss>().Where(b => b.IsFighting).Take(2).ToList();
            var middle = area.GetCenter().X;
            var barWidth = Mathf.Min(220f, area.Size.X - 60f);
            for (var i = 0; i < bosses.Count; i++)
            {
                var boss = bosses[i];
                var y = area.End.Y - 26 + (i - bosses.Count + 1) * 18; // the last one at the bottom
                var bar = new Rect2(middle - barWidth / 2, y, barWidth, 6);
                Text(boss.BossName, new Vector2(middle, y - 4), new Color(1f, 0.85f, 0.4f), 10);
                DrawRect(bar.Grow(2), Outline with { A = 0.85f });
                DrawRect(bar, new Color(0.25f, 0.1f, 0.15f));
                DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * boss.Health.Current / boss.Health.Max, bar.Size.Y)), new Color(0.85f, 0.25f, 0.3f));
            }
        }

        // Banners: fade in, stay, fade out. One per part of the screen (the newest).
        foreach (var group in _banners.GroupBy(b => AreaOf(b.Hero)))
        {
            var banner = group.MinBy(b => b.Age)!;
            var area = group.Key;
            var middle = area.GetCenter().X;
            var bannerTop = area.Position.Y + area.Size.Y * 0.255f;
            var big = area.Size.X >= 400 ? 18 : 14;
            var alpha = Mathf.Min(1f, Mathf.Min(banner.Age / 0.3f, (banner.Seconds - banner.Age) / 0.6f));
            DrawRect(new Rect2(area.Position.X, bannerTop, area.Size.X, banner.Subtitle == "" ? 30 : 46), Outline with { A = 0.6f * alpha });
            Text(banner.Title, new Vector2(middle, bannerTop + 22), new Color(1f, 0.9f, 0.55f, alpha), big);
            if (banner.Subtitle != "")
            {
                Text(banner.Subtitle, new Vector2(middle, bannerTop + 40), new Color(1f, 1f, 1f, alpha), 10);
            }
        }
    }

    private void Text(string text, Vector2 at, Color colour, int size, bool left = false)
    {
        var font = ThemeDB.FallbackFont;
        var x = left ? at.X : at.X - font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X / 2;
        DrawStringOutline(font, new Vector2(x, at.Y), text, HorizontalAlignment.Left, -1, size, 4, Outline with { A = colour.A });
        DrawString(font, new Vector2(x, at.Y), text, HorizontalAlignment.Left, -1, size, colour);
    }
}
