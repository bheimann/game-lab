using System.Linq;
using Godot;
using LizardWizard.Players;

namespace LizardWizard.Effects;

/// <summary>
/// Text that pops up over something in the world, drifts upward and fades out (damage numbers, "Zzz", speech).
/// It's drawn on a screen-space layer rather than in the zoomed world, so it stays small and sharp,
/// and it's kept inside the screen so long lines don't get cut off. With split screen, every view that can see the spot
/// gets its own copy, kept inside that view.
/// </summary>
public partial class FloatingText : Label
{
    private const string LayerName = "FloatingTextLayer";
    private const float Margin = 4f;

    private Vector2 _worldPosition;
    private float _rise;
    private float _seconds;
    private float _age;
    private SplitScreen.View? _view;

    public static void Spawn(Node anyNode, Vector2 worldPosition, string text, Color color, float seconds = 0.8f, float rise = 14f)
    {
        if (SplitScreen.Find(anyNode) is not { } split)
        {
            Make(anyNode, worldPosition, text, color, seconds, rise, null);
            return;
        }

        foreach (var view in split.Views.Where(v => v.Shows(worldPosition)))
        {
            Make(anyNode, worldPosition, text, color, seconds, rise, view);
        }
    }

    private static void Make(Node anyNode, Vector2 worldPosition, string text, Color color, float seconds, float rise, SplitScreen.View? view)
    {
        var label = new FloatingText
        {
            Text = text,
            Modulate = color,
            HorizontalAlignment = HorizontalAlignment.Center,
            _worldPosition = worldPosition,
            _rise = rise,
            _seconds = seconds,
            _view = view,
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.09f, 0.18f));
        label.AddThemeConstantOverride("outline_size", 3);
        Layer(anyNode).AddChild(label);
        label.ResetSize();
        label.Place();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _seconds || _view is { IsShown: false })
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
        var at = _worldPosition + new Vector2(0, -lift);
        var screen = _view?.ToScreen(at) ?? GetViewport().GetCanvasTransform() * at;
        var visible = _view?.ScreenRect ?? GetViewportRect();

        var position = screen - new Vector2(Size.X / 2, Size.Y);
        position.X = Mathf.Clamp(position.X, visible.Position.X + Margin, visible.End.X - Size.X - Margin);
        position.Y = Mathf.Clamp(position.Y, visible.Position.Y + Margin, visible.End.Y - Size.Y - Margin);
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
