using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;

namespace LizardWizard.UI;

/// <summary>
/// The dungeon's part of the screen: keys carried (top middle), the boss's health bar while it fights,
/// and big banners for room names and "Dungeon complete!".
/// </summary>
public partial class DungeonHud : Control
{
    private const string KeyIcon = "res://assets/kenney/key.png";
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);

    private Texture2D _key = null!;
    private string _banner = "";
    private string _subtitle = "";
    private float _bannerSeconds;
    private float _bannerAge = 999f;

    public Dungeon Dungeon { get; set; } = null!;

    public override void _Ready() => _key = GD.Load<Texture2D>(KeyIcon);

    public void ShowBanner(string title, string subtitle, float seconds)
    {
        _banner = title;
        _subtitle = subtitle;
        _bannerSeconds = seconds;
        _bannerAge = 0;
    }

    public override void _Process(double delta)
    {
        _bannerAge += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var width = GetViewportRect().Size.X;

        // Keys.
        if (Dungeon.Keys > 0)
        {
            var at = new Vector2(width / 2 - 18, 6);
            DrawRect(new Rect2(at - new Vector2(4, 2), new Vector2(44, 18)), Outline with { A = 0.7f });
            DrawTextureRect(_key, new Rect2(at + new Vector2(0, 1), _key.GetSize()), false);
            Text($"x{Dungeon.Keys}", at + new Vector2(20, 12), Colors.White, 10, left: true);
        }

        // Boss health.
        var boss = Dungeon.CurrentRoom.FindChildren("*", "", true, false).OfType<IBoss>().FirstOrDefault(b => b.IsFighting);
        if (boss is not null)
        {
            var bar = new Rect2(width / 2 - 110, 334, 220, 6);
            Text(boss.BossName, new Vector2(width / 2, 330), new Color(1f, 0.85f, 0.4f), 10);
            DrawRect(bar.Grow(2), Outline with { A = 0.85f });
            DrawRect(bar, new Color(0.25f, 0.1f, 0.15f));
            DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * boss.Health.Current / boss.Health.Max, bar.Size.Y)), new Color(0.85f, 0.25f, 0.3f));
        }

        // Banner: fades in, stays, fades out.
        if (_bannerAge < _bannerSeconds)
        {
            var alpha = Mathf.Min(1f, Mathf.Min(_bannerAge / 0.3f, (_bannerSeconds - _bannerAge) / 0.6f));
            DrawRect(new Rect2(0, 92, width, _subtitle == "" ? 30 : 46), Outline with { A = 0.6f * alpha });
            Text(_banner, new Vector2(width / 2, 114), new Color(1f, 0.9f, 0.55f, alpha), 18);
            if (_subtitle != "")
            {
                Text(_subtitle, new Vector2(width / 2, 132), new Color(1f, 1f, 1f, alpha), 10);
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
