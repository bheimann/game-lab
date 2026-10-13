using Godot;
using LizardWizard.Players;

namespace LizardWizard.UI;

/// <summary>
/// One player's corner of the HUD: "P1" in the player's colour and Zelda-style hearts (each heart is
/// 2 HP, so odd damage shows half a heart). While nobody is in that slot it shows how to join.
/// </summary>
public partial class HeartBar : Control
{
    private const string HeartPath = "res://assets/third_party/tiny_dungeon/Misc/Heart.png";
    private const int HpPerHeart = 2;
    private const int HeartScale = 2;
    private const int Gap = 2;
    private const int FontSize = 10;

    private Texture2D _heart = null!;

    /// <summary>0-based player slot (P1 = 0).</summary>
    [Export] public int PlayerIndex { get; set; }

    /// <summary>Draw from the right edge (for the panels in the right-hand corners).</summary>
    [Export] public bool AlignRight { get; set; }

    public override void _Ready()
    {
        _heart = GD.Load<Texture2D>(HeartPath);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var colour = PlayerManager.Colours[PlayerIndex];
        var label = $"P{PlayerIndex + 1}";
        var lizard = PlayerManager.Find(this)?.Players[PlayerIndex];

        if (lizard is null)
        {
            var hint = $"{label}: press A, J or . to join";
            var hintWidth = font.GetStringSize(hint, HorizontalAlignment.Left, -1, FontSize).X;
            var hintX = AlignRight ? Size.X - hintWidth : 0;
            DrawBacking(hintX, hintWidth);
            DrawString(font, new Vector2(hintX, FontSize + 2), hint, HorizontalAlignment.Left, -1, FontSize, colour with { A = 0.6f });
            return;
        }

        var health = lizard.Health;
        var hearts = (health.Max + HpPerHeart - 1) / HpPerHeart;
        var size = _heart.GetSize() * HeartScale;
        var labelWidth = font.GetStringSize(label, HorizontalAlignment.Left, -1, FontSize).X + 4;
        var total = labelWidth + hearts * (size.X + Gap);
        var x = AlignRight ? Size.X - total : 0;
        DrawBacking(x, total);

        DrawString(font, new Vector2(x, FontSize + 2), label, HorizontalAlignment.Left, -1, FontSize, colour);
        x += labelWidth;

        var empty = new Color(0.15f, 0.12f, 0.2f, 0.9f);
        for (var i = 0; i < hearts; i++)
        {
            var rect = new Rect2(new Vector2(x + i * (size.X + Gap), 0), size);
            DrawTextureRect(_heart, rect, false, empty);

            var filled = Mathf.Clamp(health.Current - i * HpPerHeart, 0, HpPerHeart);
            if (filled == HpPerHeart)
            {
                DrawTextureRect(_heart, rect, false);
            }
            else if (filled > 0)
            {
                // Half heart: draw just the left half.
                var half = _heart.GetSize() with { X = Mathf.Ceil(_heart.GetSize().X / 2) };
                DrawTextureRectRegion(_heart, new Rect2(rect.Position, half * HeartScale), new Rect2(Vector2.Zero, half));
            }
        }
    }

    /// <summary>Dark rounded box behind the panel so it's readable over walls and floor.</summary>
    private void DrawBacking(float x, float width)
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.18f, 0.7f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
        DrawStyleBox(box, new Rect2(x - 3, -2, width + 4, 18));
    }
}
