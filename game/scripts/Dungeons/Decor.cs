using Godot;

namespace LizardWizard.Dungeons;

public enum DecorKind
{
    /// <summary>A gold throne with a red cushion (solid).</summary>
    Throne,

    /// <summary>A long red carpet with gold edges, flat on the floor (<see cref="Decor.Size"/>).</summary>
    Carpet,

    /// <summary>A wooden table (solid, <see cref="Decor.Size"/> wide).</summary>
    Table,

    /// <summary>A bed with a blanket (solid).</summary>
    Bed,

    /// <summary>A red and gold banner hanging on a wall (not solid).</summary>
    Banner,

    /// <summary>A rack of spears and shields against a wall (solid).</summary>
    WeaponRack,

    /// <summary>A shelf of paint pots (solid).</summary>
    PaintShelf,

    /// <summary>A shelf of herb pots (solid).</summary>
    HerbShelf,
}

/// <summary>
/// Simple furniture drawn in code, for shops and the castle. The node's position is the middle of the bottom edge
/// (where it stands on the floor), like <see cref="Prop"/>; a carpet's is its top-left corner.
/// </summary>
public partial class Decor : StaticBody2D
{
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static readonly Color Gold = new(0.98f, 0.8f, 0.25f);
    private static readonly Color DarkGold = new(0.72f, 0.48f, 0.12f);
    private static readonly Color Red = new(0.78f, 0.2f, 0.25f);
    private static readonly Color DarkRed = new(0.52f, 0.12f, 0.2f);
    private static readonly Color Wood = new(0.66f, 0.42f, 0.26f);
    private static readonly Color DarkWood = new(0.45f, 0.27f, 0.2f);

    [Export] public DecorKind Kind { get; set; } = DecorKind.Table;

    /// <summary>Carpets: width and length; tables: width (x).</summary>
    [Export] public Vector2 Size { get; set; } = new(32, 16);

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        if (Kind == DecorKind.Carpet)
        {
            ZIndex = -1; // flat on the floor, under everyone
        }

        var solid = Kind switch
        {
            DecorKind.Throne => new Vector2(20, 8),
            DecorKind.Table => new Vector2(Size.X, 10),
            DecorKind.Bed => new Vector2(20, 14),
            DecorKind.WeaponRack or DecorKind.PaintShelf or DecorKind.HerbShelf => new Vector2(28, 6),
            _ => Vector2.Zero,
        };
        if (solid != Vector2.Zero)
        {
            AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = solid }, Position = new Vector2(0, -solid.Y / 2) });
        }
    }

    public override void _Draw()
    {
        switch (Kind)
        {
            case DecorKind.Throne:
                Box(new Rect2(-10, -34, 20, 34), DarkGold);
                Box(new Rect2(-8, -32, 16, 18), Red);
                DrawCircle(new Vector2(0, -36), 3f, Outline);
                DrawCircle(new Vector2(0, -36), 2f, Gold);
                Box(new Rect2(-11, -14, 22, 8), Gold);
                Box(new Rect2(-8, -16, 16, 4), DarkRed);
                break;

            case DecorKind.Carpet:
                DrawRect(new Rect2(Vector2.Zero, Size), Gold);
                DrawRect(new Rect2(new Vector2(2, 2), Size - new Vector2(4, 4)), Red);
                break;

            case DecorKind.Table:
                Box(new Rect2(-Size.X / 2, -14, Size.X, 7), Wood);
                Box(new Rect2(-Size.X / 2 + 2, -8, 3, 8), DarkWood);
                Box(new Rect2(Size.X / 2 - 5, -8, 3, 8), DarkWood);
                break;

            case DecorKind.Bed:
                Box(new Rect2(-10, -26, 20, 26), DarkWood);
                Box(new Rect2(-8, -24, 16, 6), Colors.White);
                Box(new Rect2(-8, -18, 16, 16), new Color(0.3f, 0.45f, 0.8f));
                break;

            case DecorKind.Banner:
                Box(new Rect2(-6, -24, 12, 20), Red);
                DrawColoredPolygon(new[] { new Vector2(-6, -4), new Vector2(0, 2), new Vector2(6, -4) }, Red);
                DrawCircle(new Vector2(0, -14), 3f, Gold);
                DrawLine(new Vector2(-8, -24), new Vector2(8, -24), DarkGold, 2f);
                break;

            case DecorKind.WeaponRack:
                Box(new Rect2(-14, -6, 28, 6), DarkWood);
                foreach (var x in new[] { -9f, -3f, 3f })
                {
                    DrawLine(new Vector2(x, -6), new Vector2(x, -30), Wood, 2f);
                    DrawColoredPolygon(new[] { new Vector2(x - 2, -30), new Vector2(x, -35), new Vector2(x + 2, -30) }, new Color(0.8f, 0.82f, 0.9f));
                }

                DrawCircle(new Vector2(10, -16), 5f, Outline);
                DrawCircle(new Vector2(10, -16), 4f, new Color(0.6f, 0.62f, 0.7f));
                break;

            case DecorKind.PaintShelf:
            case DecorKind.HerbShelf:
                Box(new Rect2(-14, -28, 28, 28), DarkWood);
                DrawLine(new Vector2(-13, -15), new Vector2(13, -15), Wood, 2f);
                var colours = Kind == DecorKind.PaintShelf
                    ? new[] { Red, Gold, new Color(0.39f, 0.67f, 0.25f), new Color(0f, 0.58f, 0.91f), new Color(0.6f, 0.35f, 0.8f), Colors.White }
                    : new[] { new Color(0.39f, 0.67f, 0.25f), new Color(0.3f, 0.55f, 0.25f), new Color(0.5f, 0.8f, 0.3f), new Color(0.39f, 0.67f, 0.25f), new Color(0.3f, 0.55f, 0.25f), new Color(0.5f, 0.8f, 0.3f) };
                for (var i = 0; i < 6; i++)
                {
                    var at = new Vector2(-9 + i % 3 * 9, i < 3 ? -20 : -6);
                    Box(new Rect2(at - new Vector2(3, 3), new Vector2(6, 6)), colours[i]);
                }

                break;
        }
    }

    /// <summary>A filled rectangle with a dark outline.</summary>
    private void Box(Rect2 rect, Color colour)
    {
        DrawRect(rect, Outline);
        DrawRect(rect.Grow(-1), colour);
    }
}
