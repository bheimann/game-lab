using Godot;

namespace LizardWizard.Effects;

/// <summary>
/// Text that pops up over something in the world, drifts upward and fades out (damage numbers, "Zzz", speech).
/// It's drawn on a screen-space layer rather than in the zoomed world, so it stays small and sharp,
/// and it's kept inside the screen so long lines don't get cut off.
/// </summary>
public partial class FloatingText : Label
{
    private const string LayerName = "FloatingTextLayer";
    private const float Margin = 4f;

    private Vector2 _worldPosition;
    private float _rise;
    private float _seconds;
    private float _age;

    public static FloatingText Spawn(Node anyNode, Vector2 worldPosition, string text, Color color, float seconds = 0.8f, float rise = 14f)
    {
        var label = new FloatingText
        {
            Text = text,
            Modulate = color,
            HorizontalAlignment = HorizontalAlignment.Center,
            _worldPosition = worldPosition,
            _rise = rise,
            _seconds = seconds,
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.09f, 0.18f));
        label.AddThemeConstantOverride("outline_size", 3);
        Layer(anyNode).AddChild(label);
        label.ResetSize();
        label.Place();
        return label;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _seconds)
        {
            QueueFree();
            return;
        }

        // Fade out over the last 40%.
        var fadeStart = _seconds * 0.6f;
        Modulate = Modulate with { A = _age < fadeStart ? 1f : 1f - (_age - fadeStart) / (_seconds - fadeStart) };
        Place();
    }

    private void Place()
    {
        // Ease out: rises quickly, then settles.
        var t = Mathf.Clamp(_age / _seconds, 0, 1);
        var lift = _rise * (1f - (1f - t) * (1f - t));
        var screen = GetViewport().GetCanvasTransform() * (_worldPosition + new Vector2(0, -lift));
        var visible = GetViewportRect().Size;

        var position = screen - new Vector2(Size.X / 2, Size.Y);
        position.X = Mathf.Clamp(position.X, Margin, visible.X - Size.X - Margin);
        position.Y = Mathf.Clamp(position.Y, Margin, visible.Y - Size.Y - Margin);
        Position = position;
    }

    private static CanvasLayer Layer(Node anyNode)
    {
        var root = anyNode.GetTree().CurrentScene;
        if (root.GetNodeOrNull<CanvasLayer>(LayerName) is { } existing)
        {
            return existing;
        }

        var layer = new CanvasLayer { Name = LayerName, Layer = 5 };
        root.AddChild(layer);
        return layer;
    }
}
