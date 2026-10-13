using System.Linq;
using Godot;
using LizardWizard.Actors;

namespace LizardWizard.UI;

/// <summary>Zelda-style hearts for the first player: each heart is 2 HP, so odd damage shows half a heart.</summary>
public partial class HeartBar : Control
{
    private const string HeartPath = "res://assets/third_party/tiny_dungeon/Misc/Heart.png";
    private const int HpPerHeart = 2;
    private const int HeartScale = 2;
    private const int Gap = 2;

    private Texture2D _heart = null!;
    private Lizard? _lizard;

    public override void _Ready()
    {
        _heart = GD.Load<Texture2D>(HeartPath);
    }

    public override void _Process(double delta)
    {
        if (_lizard is null)
        {
            _lizard = GetTree().GetNodesInGroup(Lizard.Group).OfType<Lizard>().FirstOrDefault();
            if (_lizard is not null)
            {
                _lizard.Health.Changed += QueueRedraw;
                QueueRedraw();
            }
        }
    }

    public override void _Draw()
    {
        if (_lizard is null)
        {
            return;
        }

        var health = _lizard.Health;
        var hearts = (health.Max + HpPerHeart - 1) / HpPerHeart;
        var size = _heart.GetSize() * HeartScale;
        var empty = new Color(0.15f, 0.12f, 0.2f, 0.9f);

        for (var i = 0; i < hearts; i++)
        {
            var rect = new Rect2(new Vector2(i * (size.X + Gap), 0), size);
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
}
