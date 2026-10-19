using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Dungeons;

namespace LizardWizard.Rooms;

/// <summary>What an outdoor area's ground and border look like.</summary>
public enum Ground
{
    /// <summary>Grass, a dirt path and a ring of bushes.</summary>
    Meadow,

    /// <summary>Darker grass with a ring of trees.</summary>
    Forest,

    /// <summary>Murky green ground, muddy paths and a ring of dull bushes.</summary>
    Swamp,

    /// <summary>Snow, packed-snow paths and a ring of snowy pines.</summary>
    Snow,

    /// <summary>Sand (a beach), and a ring of palm-green bushes.</summary>
    Sand,

    /// <summary>Red rock, dusty paths and a ring of boulders.</summary>
    Canyon,

    /// <summary>Grey-green mountain grass, stony paths and a ring of grey rocks.</summary>
    Mountain,

    /// <summary>Hot orange sand, pale paths and a ring of sandy rocks.</summary>
    Desert,
}

/// <summary>
/// The layout for an outdoor area (the overworld): ground with a ring of bushes or trees around the edge instead of
/// dungeon walls, and an optional path. Everything else (rocks, signs, ponds, enemies, more paths) is placed in the
/// scene as children of the area. Sizes work like <see cref="RoomBuilder"/>: <see cref="RoomBuilder.InteriorWidth"/>
/// and <see cref="RoomBuilder.InteriorHeight"/> are the walkable size in 8px tiles.
/// A <see cref="Door"/> with <see cref="Door.Path"/> on the area's edge leaves a gap in the border to walk out through;
/// enemies can't use the gap.
/// </summary>
public partial class OutdoorBuilder : RoomBuilder
{
    /// <summary>Physics layer 6: walls only enemies bump into (across path gaps, so deer stay in their area).</summary>
    public const uint EnemyWallLayer = 32;

    private const int Edge = 16; // the border

    [Export] public Ground Ground { get; set; } = Ground.Meadow;

    /// <summary>A path through these points (relative to this node), drawn under everything. (More paths: <see cref="DirtPath"/>.)</summary>
    [Export] public Vector2[] PathPoints { get; set; } = System.Array.Empty<Vector2>();

    public override Vector2 SizeInPixels => new(InteriorWidth * TileSize + Edge * 2, InteriorHeight * TileSize + Edge * 2);

    public override Rect2 FloorRect => new(Edge, Edge, InteriorWidth * TileSize, InteriorHeight * TileSize);

    /// <summary>Ground colours (Kenney's Tiny Town greens for the meadow, so the pond's grassy bank matches).</summary>
    public (Color Main, Color Dark) GroundColours => Ground switch
    {
        Ground.Forest => (Godot.Color.Color8(104, 172, 90), Godot.Color.Color8(78, 140, 74)),
        Ground.Swamp => (Godot.Color.Color8(112, 146, 84), Godot.Color.Color8(86, 116, 70)),
        Ground.Snow => (Godot.Color.Color8(236, 243, 250), Godot.Color.Color8(196, 214, 236)),
        Ground.Sand => (Godot.Color.Color8(240, 218, 160), Godot.Color.Color8(222, 192, 132)),
        Ground.Canyon => (Godot.Color.Color8(200, 128, 92), Godot.Color.Color8(170, 102, 76)),
        Ground.Mountain => (Godot.Color.Color8(146, 160, 140), Godot.Color.Color8(120, 134, 118)),
        Ground.Desert => (Godot.Color.Color8(236, 196, 120), Godot.Color.Color8(216, 172, 98)),
        _ => (Godot.Color.Color8(132, 198, 105), Godot.Color.Color8(101, 165, 86)),
    };

    /// <summary>Path colours: dirt, mud or packed snow.</summary>
    public (Color Main, Color Dark) PathColours => Ground switch
    {
        Ground.Swamp => (Godot.Color.Color8(128, 104, 74), Godot.Color.Color8(98, 78, 58)),
        Ground.Snow => (Godot.Color.Color8(212, 224, 240), Godot.Color.Color8(180, 196, 222)),
        Ground.Sand => (Godot.Color.Color8(250, 236, 196), Godot.Color.Color8(230, 210, 162)),
        Ground.Canyon => (Godot.Color.Color8(226, 170, 122), Godot.Color.Color8(204, 146, 104)),
        Ground.Mountain => (Godot.Color.Color8(186, 182, 170), Godot.Color.Color8(160, 156, 146)),
        Ground.Desert => (Godot.Color.Color8(248, 224, 168), Godot.Color.Color8(232, 204, 140)),
        _ => (Godot.Color.Color8(234, 165, 108), Godot.Color.Color8(207, 130, 84)),
    };

    private string BorderTexture => Ground switch
    {
        Ground.Forest => "res://assets/kenney/tree.png",
        Ground.Snow => "res://assets/kenney/snowpine.png",
        Ground.Canyon or Ground.Mountain or Ground.Desert => "res://assets/kenney/rock.png",
        _ => "res://assets/kenney/bush.png",
    };

    private Color BorderTint => Ground switch
    {
        Ground.Swamp => new Color(0.65f, 0.72f, 0.5f),
        Ground.Sand => new Color(0.9f, 1f, 0.75f),
        Ground.Canyon => new Color(1f, 0.75f, 0.65f),
        Ground.Desert => new Color(1f, 0.88f, 0.65f),
        _ => Colors.White,
    };

    public override void _Ready()
    {
        // Invisible walls along the border, with gaps for paths out; enemies get walls across the gaps too.
        var size = SizeInPixels;
        var walls = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        var gapWalls = new StaticBody2D { CollisionLayer = EnemyWallLayer, CollisionMask = 0 };
        foreach (var (side, start, end) in new[]
                 {
                     (DoorSide.Top, 0f, size.X), (DoorSide.Bottom, 0f, size.X), (DoorSide.Left, 0f, size.Y), (DoorSide.Right, 0f, size.Y),
                 })
        {
            foreach (var (a, b) in Cut(start, end, Gaps(side)))
            {
                walls.AddChild(BorderShape(side, a, b));
            }

            foreach (var (a, b) in Gaps(side))
            {
                gapWalls.AddChild(BorderShape(side, a, b));
            }
        }

        AddChild(walls);
        AddChild(gapWalls);
    }

    public override void _Draw()
    {
        var size = SizeInPixels;
        var (ground, groundDark) = GroundColours;
        DrawRect(new Rect2(Vector2.Zero, size), ground);

        // Little tufts of darker grass (or snow drifts), the same every time.
        var rng = new RandomNumberGenerator { Seed = 1234 };
        for (var i = 0; i < size.X * size.Y / 900; i++)
        {
            var at = new Vector2(rng.RandfRange(0, size.X), rng.RandfRange(0, size.Y)).Floor();
            DrawLine(at, at + new Vector2(-1, -2), groundDark);
            DrawLine(at + new Vector2(2, 0), at + new Vector2(3, -2), groundDark);
        }

        if (PathPoints.Length > 1)
        {
            DirtPath.DrawPath(this, PathPoints, PathColours);
        }

        // A ring of bushes or trees around the edge, leaving the path gaps open.
        var texture = GD.Load<Texture2D>(BorderTexture);
        var w = texture.GetWidth();
        var h = texture.GetHeight();
        var step = w - 2;
        for (var x = 0f; x < size.X; x += step)
        {
            if (!InGap(DoorSide.Top, x, x + w))
            {
                DrawTexture(texture, new Vector2(x, Edge + 1 - h), BorderTint);
            }

            if (!InGap(DoorSide.Bottom, x, x + w))
            {
                DrawTexture(texture, new Vector2(x, size.Y - h), BorderTint);
            }
        }

        for (var y = 12f; y < size.Y - 14; y += 12)
        {
            if (!InGap(DoorSide.Left, y, y + 16))
            {
                DrawTexture(texture, new Vector2(1, y + 16 - h), BorderTint);
            }

            if (!InGap(DoorSide.Right, y, y + 16))
            {
                DrawTexture(texture, new Vector2(size.X - w, y + 16 - h), BorderTint);
            }
        }
    }

    /// <summary>The path gaps on one side, as ranges along that side (relative to this node).</summary>
    private List<(float Start, float End)> Gaps(DoorSide side) =>
        GetParent().GetChildren().OfType<Door>().Where(d => d.Path && d.Side == side)
            .Select(d => d.Position - Position)
            .Select(p => side is DoorSide.Top or DoorSide.Bottom ? p.X : p.Y)
            .Select(c => (c - Door.PathWidth / 2, c + Door.PathWidth / 2))
            .OrderBy(g => g.Item1)
            .ToList();

    private bool InGap(DoorSide side, float start, float end) => Gaps(side).Any(g => start < g.End && end > g.Start);

    /// <summary>The parts of start..end that aren't in any gap.</summary>
    private static IEnumerable<(float, float)> Cut(float start, float end, List<(float Start, float End)> gaps)
    {
        var from = start;
        foreach (var (a, b) in gaps)
        {
            if (a > from)
            {
                yield return (from, a);
            }

            from = Mathf.Max(from, b);
        }

        if (end > from)
        {
            yield return (from, end);
        }
    }

    private CollisionShape2D BorderShape(DoorSide side, float a, float b)
    {
        var size = SizeInPixels;
        var rect = side switch
        {
            DoorSide.Top => new Rect2(a, 0, b - a, Edge),
            DoorSide.Bottom => new Rect2(a, size.Y - Edge, b - a, Edge),
            DoorSide.Left => new Rect2(0, a, Edge, b - a),
            _ => new Rect2(size.X - Edge, a, Edge, b - a),
        };
        return new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size }, Position = rect.GetCenter() };
    }
}
