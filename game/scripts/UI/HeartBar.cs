using System.Collections.Generic;
using Godot;
using LizardWizard.Items;
using LizardWizard.Players;

namespace LizardWizard.UI;

/// <summary>
/// One player's corner of the HUD: "P1" in the player's colour and Zelda-style hearts (each heart is
/// 2 HP, so odd damage shows half a heart). Under the hearts: the selected bomb / colour, herbs left
/// and the armor being worn. While nobody is in that slot it shows how to join
/// (and, while picking, the character and class).
/// </summary>
public partial class HeartBar : Control
{
    private const string HeartPath = "res://assets/kenney/heart.png";
    private const int HpPerHeart = 2;
    private const int HeartScale = 1;
    private const int Gap = 2;
    private const int FontSize = 10;
    private const string HerbIcon = "res://assets/sprites/icons/herb.png";

    private Texture2D _heart = null!;
    private Texture2D _herb = null!;

    /// <summary>0-based player slot (P1 = 0).</summary>
    [Export] public int PlayerIndex { get; set; }

    /// <summary>Draw from the right edge (for the panels in the right-hand corners).</summary>
    [Export] public bool AlignRight { get; set; }

    public override void _Ready()
    {
        _heart = GD.Load<Texture2D>(HeartPath);
        _herb = GD.Load<Texture2D>(HerbIcon);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var colour = PlayerManager.Colours[PlayerIndex];
        var label = $"P{PlayerIndex + 1}";
        var slot = PlayerManager.Find(this)?.Slots[PlayerIndex];
        var lizard = slot?.Hero;

        if (lizard is null)
        {
            // Picking: left / right for the character, up / down for its class.
            var choosing = slot is { IsChoosing: true };
            var hint = choosing
                ? $"{label}:  <  {slot!.Character.Name}  >   press A / J / . to start"
                : $"{label}: press A, J or . to join";
            var classHint = !choosing ? "" : slot!.Character.Classes.Count > 1 ? $"up / down:  {slot.Class}" : slot.Class;
            var hintWidth = font.GetStringSize(hint, HorizontalAlignment.Left, -1, FontSize).X;
            var classWidth = classHint == "" ? 0 : font.GetStringSize(classHint, HorizontalAlignment.Left, -1, FontSize).X;
            var hintX = AlignRight ? Size.X - hintWidth : 0;
            var classX = AlignRight ? Size.X - classWidth : 0;
            DrawBacking(hintX, hintWidth, classX, classWidth);
            DrawString(font, new Vector2(hintX, FontSize + 2), hint, HorizontalAlignment.Left, -1, FontSize, colour with { A = choosing ? 1f : 0.6f });
            if (classHint != "")
            {
                DrawString(font, new Vector2(classX, FontSize + 19), classHint, HorizontalAlignment.Left, -1, FontSize, Colors.White);
            }

            return;
        }

        var health = lizard.Health;
        var hearts = (health.Max + HpPerHeart - 1) / HpPerHeart;
        var size = _heart.GetSize() * HeartScale;
        var labelWidth = font.GetStringSize(label, HorizontalAlignment.Left, -1, FontSize).X + 4;
        var total = labelWidth + hearts * (size.X + Gap);
        var x = AlignRight ? Size.X - total : 0;

        // Second row: quick-swap choice, herbs, armor.
        var gear = new List<MenuOption>();
        if (lizard.QuickOptions is { } quick)
        {
            gear.Add(quick.Options[quick.Selected]);
        }

        gear.Add(new MenuOption("Herbs", "", _herb, Colors.White) { Count = $"x{lizard.Herbs}", Enabled = lizard.Herbs > 0 });
        if (lizard.Armor is { } armor)
        {
            gear.Add(armor.ToOption());
        }

        const float gearSlot = 24f;
        var gearX = AlignRight ? Size.X - gear.Count * gearSlot : 0;
        DrawBacking(x, total, gearX, gear.Count * gearSlot);
        for (var i = 0; i < gear.Count; i++)
        {
            gear[i].Draw(this, new Vector2(gearX + 7 + i * gearSlot, 25), 1f);
        }

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

    /// <summary>Dark rounded box behind the panel so it's readable over walls and floor (two rows when there's gear).</summary>
    private void DrawBacking(float x, float width, float gearX, float gearWidth)
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
        if (gearWidth > 0)
        {
            DrawStyleBox(box, new Rect2(gearX - 3, 17, gearWidth + 4, 16));
        }
    }
}
