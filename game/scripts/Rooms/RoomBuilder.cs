using Godot;

namespace LizardWizard.Rooms;

/// <summary>Source IDs in assets/tilesets/room_tileset.tres (see tools/make_room_tileset.py).</summary>
public enum RoomColor
{
    Green = 0,
    Blue = 1,
    Brown = 2,
    Gray = 3,
    Red = 4,
}

/// <summary>
/// Builds a rectangular room of any size by stretching the room template drawn
/// in the Tiny Dungeon tileset sheets. Positions are in 8px tiles.
///
/// Fills the child TileMapLayers "Floor" and "Walls" when the game starts, but only if
/// they're empty, so a room painted by hand in the editor is left alone.
/// </summary>
public partial class RoomBuilder : Node2D
{
    public const int TileSize = 8;

    // Where the template's pieces sit on the sheet, in 8px cells.
    private const int LeftColumn = 1;
    private const int MiddleColumn = 2; // middle repeats every 2 cells (16px pattern)
    private const int RightColumn = 8;
    private static readonly int[] TopRows = { 2, 3, 4, 5 };
    private const int MiddleRow = 6;
    private static readonly int[] BottomRows = { 10, 11, 12 };
    private static readonly Vector2I FloorCell = new(2, 0); // 2x2 cells

    [Export] public RoomColor Color { get; set; } = RoomColor.Green;

    /// <summary>Floor width in 8px tiles. Must be even so the wall pattern lines up.</summary>
    [Export] public int InteriorWidth { get; set; } = 32;

    /// <summary>Floor height in 8px tiles, between the top and bottom walls.</summary>
    [Export] public int InteriorHeight { get; set; } = 12;

    /// <summary>Wall thickness at the top and bottom, in tiles.</summary>
    public static int TopWallTiles => TopRows.Length;

    public static int BottomWallTiles => BottomRows.Length;

    public int Columns => InteriorWidth + 2;

    public int Rows => TopRows.Length + InteriorHeight + BottomRows.Length;

    /// <summary>The Tiny Dungeon sheet this room's tiles come from (doors cut pieces out of it too).</summary>
    public Texture2D Sheet => GD.Load<Texture2D>($"res://assets/third_party/tiny_dungeon/Tilesets/Room/Room_{Color.ToString().ToLowerInvariant()}.png");

    public virtual Vector2 SizeInPixels =>
        new((InteriorWidth + 2) * TileSize, (TopRows.Length + InteriorHeight + BottomRows.Length) * TileSize);

    /// <summary>The walkable floor area, in pixels relative to this node.</summary>
    public virtual Rect2 FloorRect =>
        new(TileSize, TopRows.Length * TileSize, InteriorWidth * TileSize, InteriorHeight * TileSize);

    public override void _Ready()
    {
        var floor = GetNode<TileMapLayer>("Floor");
        var walls = GetNode<TileMapLayer>("Walls");
        if (floor.GetUsedCells().Count == 0 && walls.GetUsedCells().Count == 0)
        {
            Build(floor, walls);
        }
    }

    /// <summary>Remove the wall tile at <paramref name="cell"/> (for a doorway) and put floor there.</summary>
    public void Carve(Vector2I cell)
    {
        GetNode<TileMapLayer>("Walls").EraseCell(cell);
        GetNode<TileMapLayer>("Floor").SetCell(cell, (int)Color, FloorTile(cell));
    }

    private static Vector2I FloorTile(Vector2I cell) =>
        FloorCell + new Vector2I(Mathf.PosMod(cell.X - 1, 2), Mathf.PosMod(cell.Y, 2));

    public void Build(TileMapLayer floor, TileMapLayer walls)
    {
        floor.Clear();
        walls.Clear();

        var sourceId = (int)Color;
        var source = (TileSetAtlasSource)walls.TileSet.GetSource(sourceId);
        var columns = InteriorWidth + 2;
        var rows = TopRows.Length + InteriorHeight + BottomRows.Length;

        for (var y = 0; y < rows; y++)
        {
            var templateRow = y < TopRows.Length ? TopRows[y]
                : y < TopRows.Length + InteriorHeight ? MiddleRow
                : BottomRows[y - TopRows.Length - InteriorHeight];

            for (var x = 0; x < columns; x++)
            {
                var templateColumn = x == 0 ? LeftColumn
                    : x == columns - 1 ? RightColumn
                    : MiddleColumn + (x - 1) % 2;

                var wallCell = new Vector2I(templateColumn, templateRow);
                if (source.HasTile(wallCell))
                {
                    walls.SetCell(new Vector2I(x, y), sourceId, wallCell);
                }

                var insideWalls = x > 0 && x < columns - 1 && y >= 2 && y < rows - 1;
                if (insideWalls)
                {
                    floor.SetCell(new Vector2I(x, y), sourceId, FloorTile(new Vector2I(x, y)));
                }
            }
        }
    }
}
