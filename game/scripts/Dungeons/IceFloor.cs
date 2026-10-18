using System.Linq;
using Godot;

namespace LizardWizard.Dungeons;

/// <summary>
/// A patch of slippery ice on a grid of 16px cells, drawn from <see cref="Map"/> ('.' ice, '#' an ice block, anything
/// else plain floor). Heroes who step onto the ice slide in a straight line until they bump into something (a block, a
/// wall) or slide off the edge, so getting somewhere is a puzzle (see Hero). The node's position is the top-left corner.
/// </summary>
public partial class IceFloor : Node2D
{
    public const string Group = "ice";
    public const int Cell = 16;

    private static readonly Color Ice = new(0.78f, 0.9f, 1f);
    private static readonly Color IceDark = new(0.62f, 0.8f, 0.95f);
    private static readonly Color Block = new(0.55f, 0.72f, 0.92f);
    private static readonly Color BlockLight = new(0.88f, 0.96f, 1f);
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);

    private string[] _rows = System.Array.Empty<string>();

    /// <summary>One line per row of cells: '.' ice, '#' an ice block, anything else plain floor.</summary>
    [Export(PropertyHint.MultilineText)] public string Map { get; set; } = "...\n.#.\n...";

    public override void _Ready()
    {
        AddToGroup(Group);
        ZIndex = -1; // part of the floor (the blocks stand up out of it, see below)
        _rows = Map.Replace("\r", "").Split('\n').Where(r => r.Length > 0).ToArray();

        // The ice blocks are solid.
        var body = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
        for (var y = 0; y < _rows.Length; y++)
        {
            for (var x = 0; x < _rows[y].Length; x++)
            {
                if (_rows[y][x] == '#')
                {
                    body.AddChild(new CollisionShape2D
                    {
                        Shape = new RectangleShape2D { Size = new Vector2(Cell, Cell) },
                        Position = new Vector2(x * Cell + Cell / 2f, y * Cell + Cell / 2f),
                    });
                }
            }
        }

        AddChild(body);
    }

    /// <summary>The ice floor (and cell) under <paramref name="point"/>, if it's on an ice cell (not a block).</summary>
    public static (IceFloor Floor, Vector2I Cell)? At(SceneTree tree, Vector2 point)
    {
        foreach (var floor in tree.GetNodesInGroup(Group).OfType<IceFloor>())
        {
            if (!floor.IsVisibleInTree())
            {
                continue; // in a room nobody's in
            }

            var local = point - floor.GlobalPosition;
            var cell = new Vector2I(Mathf.FloorToInt(local.X / Cell), Mathf.FloorToInt(local.Y / Cell));
            if (floor.IsIce(cell))
            {
                return (floor, cell);
            }
        }

        return null;
    }

    public bool IsIce(Vector2I cell) =>
        cell.Y >= 0 && cell.Y < _rows.Length && cell.X >= 0 && cell.X < _rows[cell.Y].Length && _rows[cell.Y][cell.X] == '.';

    /// <summary>The middle of a cell, where a hero's feet go when lined up with it.</summary>
    public Vector2 CellCenter(Vector2I cell) => GlobalPosition + new Vector2(cell.X * Cell + Cell / 2f, cell.Y * Cell + Cell / 2f + 4);

    public override void _Draw()
    {
        for (var y = 0; y < _rows.Length; y++)
        {
            for (var x = 0; x < _rows[y].Length; x++)
            {
                var rect = new Rect2(x * Cell, y * Cell, Cell, Cell);
                switch (_rows[y][x])
                {
                    case '.':
                        DrawRect(rect, (x + y) % 2 == 0 ? Ice : IceDark);
                        DrawLine(rect.Position + new Vector2(3, 4), rect.Position + new Vector2(7, 2), Colors.White with { A = 0.6f }, 1f);
                        break;
                    case '#':
                        DrawRect(rect, Ice);
                        DrawRect(new Rect2(rect.Position + new Vector2(1, -4), new Vector2(Cell - 2, Cell + 2)), Outline);
                        DrawRect(new Rect2(rect.Position + new Vector2(2, -3), new Vector2(Cell - 4, Cell)), Block);
                        DrawRect(new Rect2(rect.Position + new Vector2(2, -3), new Vector2(Cell - 4, 4)), BlockLight);
                        break;
                }
            }
        }
    }
}
