using Godot;

namespace LizardWizard.Rooms;

/// <summary>
/// An extra path in an outdoor area (the layout has one of its own): drawn on the ground through
/// <see cref="Points"/> (relative to the area), in the area's path colours (dirt, mud or packed snow).
/// </summary>
public partial class DirtPath : Node2D
{
    [Export] public Vector2[] Points { get; set; } = System.Array.Empty<Vector2>();

    public override void _Ready()
    {
        ZIndex = -1; // part of the ground
    }

    public override void _Draw()
    {
        var colours = GetParent().GetNodeOrNull<OutdoorBuilder>("Layout")?.PathColours
            ?? (Color.Color8(234, 165, 108), Color.Color8(207, 130, 84));
        DrawPath(this, Points, colours);
    }

    /// <summary>A 14px path with a darker edge and rounded corners.</summary>
    public static void DrawPath(CanvasItem canvas, Vector2[] points, (Color Main, Color Dark) colours)
    {
        if (points.Length < 2)
        {
            return;
        }

        canvas.DrawPolyline(points, colours.Dark, 18f);
        foreach (var point in points)
        {
            canvas.DrawCircle(point, 9f, colours.Dark);
        }

        canvas.DrawPolyline(points, colours.Main, 14f);
        foreach (var point in points)
        {
            canvas.DrawCircle(point, 7f, colours.Main);
        }
    }
}
