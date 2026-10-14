using Godot;

namespace LizardWizard.Rooms;

/// <summary>
/// The layout for an outdoor area (the overworld): grass with a ring of bushes around the edge instead of
/// dungeon walls, and an optional dirt path. Everything else (rocks, signs, ponds, enemies) is placed in the
/// scene as children of the area. Sizes work like <see cref="RoomBuilder"/>: <see cref="RoomBuilder.InteriorWidth"/>
/// and <see cref="RoomBuilder.InteriorHeight"/> are the walkable size in 8px tiles.
/// </summary>
public partial class OutdoorBuilder : RoomBuilder
{
    private const string BushTexture = "res://assets/third_party/tiny_dungeon/Misc/Bush.png";
    private const int Edge = 16; // the bush border

    private static readonly Color Grass = new(0.78f, 0.81f, 0.37f);
    private static readonly Color GrassDark = new(0.39f, 0.67f, 0.25f);
    private static readonly Color Dirt = new(0.82f, 0.62f, 0.42f);
    private static readonly Color DirtDark = new(0.66f, 0.45f, 0.33f);

    /// <summary>A dirt path through these points (relative to this node), drawn under everything.</summary>
    [Export] public Vector2[] PathPoints { get; set; } = System.Array.Empty<Vector2>();

    public override Vector2 SizeInPixels => new(InteriorWidth * TileSize + Edge * 2, InteriorHeight * TileSize + Edge * 2);

    public override Rect2 FloorRect => new(Edge, Edge, InteriorWidth * TileSize, InteriorHeight * TileSize);

    public override void _Ready()
    {
        // Invisible walls along the bush border.
        var size = SizeInPixels;
        var body = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        foreach (var rect in new[]
                 {
                     new Rect2(0, 0, size.X, Edge), new Rect2(0, size.Y - Edge, size.X, Edge),
                     new Rect2(0, 0, Edge, size.Y), new Rect2(size.X - Edge, 0, Edge, size.Y),
                 })
        {
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size }, Position = rect.GetCenter() });
        }

        AddChild(body);
    }

    public override void _Draw()
    {
        var size = SizeInPixels;
        DrawRect(new Rect2(Vector2.Zero, size), Grass);

        // Little tufts of darker grass, the same every time.
        var rng = new RandomNumberGenerator { Seed = 1234 };
        for (var i = 0; i < size.X * size.Y / 900; i++)
        {
            var at = new Vector2(rng.RandfRange(0, size.X), rng.RandfRange(0, size.Y)).Floor();
            DrawLine(at, at + new Vector2(-1, -2), GrassDark);
            DrawLine(at + new Vector2(2, 0), at + new Vector2(3, -2), GrassDark);
        }

        if (PathPoints.Length > 1)
        {
            DrawPolyline(PathPoints, DirtDark, 18f);
            DrawPolyline(PathPoints, Dirt, 14f);
            foreach (var point in PathPoints)
            {
                DrawCircle(point, 9f, DirtDark);
                DrawCircle(point, 7f, Dirt);
            }
        }

        // A ring of bushes around the edge.
        var bush = GD.Load<Texture2D>(BushTexture);
        for (var x = 0f; x < size.X; x += 14)
        {
            DrawTexture(bush, new Vector2(x, 1));
            DrawTexture(bush, new Vector2(x, size.Y - 14));
        }

        for (var y = 12f; y < size.Y - 14; y += 12)
        {
            DrawTexture(bush, new Vector2(1, y));
            DrawTexture(bush, new Vector2(size.X - 16, y));
        }
    }
}
