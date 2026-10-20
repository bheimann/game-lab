using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Players;
using LizardWizard.Rooms;

namespace LizardWizard.UI;

/// <summary>
/// One player's map (Start menu, Map). It covers just that player's part of the screen and the game keeps running
/// (the hero stands still while it's open). Pages: the overworld, then each dungeon someone's been in; it opens on
/// where the hero is, and left / right flip between pages.
/// The overworld page shows every area, greyed out until someone's been there, the villages' names and the dungeons'
/// stairs (ticked once beaten). Each hero is a dot in their colour: outdoors where they are, in a building at its door,
/// in a dungeon at its stairs (with a little arrow down).
/// A dungeon's page shows the rooms someone's been in, with every door out of them (locked ones with a key) so you can
/// see where you haven't been yet; the boss room is marked. Heroes elsewhere are listed underneath.
/// </summary>
public partial class MapView : Node2D
{
    private const string LayerName = "MapLayer";
    private const int FontSize = 8;
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);
    private static readonly Color Paper = new(0.12f, 0.13f, 0.22f, 0.98f);
    private static readonly Color Unknown = new(0.45f, 0.47f, 0.55f);
    private static readonly Color PathColour = new(0.85f, 0.72f, 0.5f);
    private static readonly Color Floor = new(0.82f, 0.68f, 0.5f);
    private static readonly Color Gold = new(0.98f, 0.8f, 0.25f);

    private readonly Hero _hero;
    private List<string> _pages = new(); // "" = the overworld, otherwise a dungeon's area name
    private int _page;
    private float _time;

    public MapView(Hero hero)
    {
        _hero = hero;
        Visible = false;
    }

    public bool IsOpen { get; private set; }

    /// <summary>Make the map for <paramref name="hero"/> on the screen-space map layer.</summary>
    public static MapView Create(Hero hero)
    {
        var map = new MapView(hero);
        Layer(hero).AddChild(map);
        return map;
    }

    public void Open()
    {
        if (Dungeon.Find(this) is not { } dungeon)
        {
            return;
        }

        var here = dungeon.RoomOf(_hero).Area;
        _pages = new List<string> { "" };
        _pages.AddRange(dungeon.Rooms.Where(r => r.Area != "" && (r.Visited || r.Area == here)).Select(r => r.Area).Distinct());
        _page = _pages.IndexOf(here);
        IsOpen = true;
        Visible = true;
    }

    public void Close()
    {
        IsOpen = false;
        Visible = false;
    }

    /// <summary>Left / right: another page; any button: close.</summary>
    public void HandleInput(PlayerInput input)
    {
        if (input.NavigateX != 0 && _pages.Count > 1)
        {
            _page = Mathf.PosMod(_page + input.NavigateX, _pages.Count);
        }
        else if (input.PrimaryPressed || input.SecondaryPressed || input.MenuPressed || input.ItemPressed)
        {
            Close();
        }
    }

    public override void _Process(double delta)
    {
        if (IsOpen)
        {
            _time += (float)delta;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!IsOpen || Dungeon.Find(this) is not { } dungeon || !IsInstanceValid(_hero))
        {
            return;
        }

        var view = SplitScreen.Find(this)?.ViewOf(_hero)?.ScreenRect ?? GetViewportRect();
        var panel = view.Grow(-6);
        DrawRect(panel, Paper);
        DrawRect(panel, _hero.PlayerColour, false, 1.5f);
        var grid = new Rect2(panel.Position + new Vector2(8, 20), panel.Size - new Vector2(16, 40));

        var area = _pages[_page];
        var title = area == "" ? DrawOverworld(dungeon, grid) : DrawDungeon(dungeon, area, grid);
        Text(title, new Vector2(panel.GetCenter().X, panel.Position.Y + 13), Colors.White, FontSize + 1);
        Text(Hints(), new Vector2(panel.GetCenter().X, panel.End.Y - 6), new Color(0.7f, 0.72f, 0.82f), FontSize - 1);
    }

    /// <summary>The overworld page; returns its title.</summary>
    private string DrawOverworld(Dungeon dungeon, Rect2 grid)
    {
        var cells = WorldMap.Overworld(dungeon.HomeVillage);
        var place = Placer(cells, grid, maxCell: 52f);

        // Paths between areas first (under the boxes).
        foreach (var (room, cell) in cells)
        {
            foreach (var door in room.Doors.Where(d => d.Path && d.Partner is not null && cells.ContainsKey(d.Partner.Room)))
            {
                var other = cells[door.Partner!.Room];
                if (room.Visited || door.Partner.Room.Visited)
                {
                    DrawLine(place(cell).GetCenter(), place(other).GetCenter(), PathColour, 2f);
                }
            }
        }

        foreach (var (room, cell) in cells)
        {
            var box = place(cell);
            if (room.Visited)
            {
                var ground = room.Layout is OutdoorBuilder outdoor ? outdoor.GroundColours.Main : Floor;
                DrawRect(box, ground);
                DrawRect(box, Outline, false, 1f);
                if (room.IsVillage)
                {
                    DrawRect(new Rect2(box.GetCenter() - new Vector2(4, 4), new Vector2(8, 7)), new Color(0.75f, 0.35f, 0.3f)); // a little house
                    Text(room.Title, new Vector2(box.GetCenter().X, box.End.Y - 2), Colors.White, FontSize - 2);
                }
            }
            else
            {
                DrawRect(box, Outline with { A = 0.6f });
                DrawRect(box, Unknown, false, 1f);
            }
        }

        // The dungeons' stairs (once someone's found them), ticked when beaten.
        foreach (var name in dungeon.Rooms.Select(r => r.Area).Where(a => a != "").Distinct())
        {
            if (WorldMap.StairsInto(name, dungeon.Rooms) is { } stairs && stairs.Room.Visited && cells.ContainsKey(stairs.Room))
            {
                var at = Point(place, cells, stairs.Room, stairs.GlobalPosition);
                DrawRect(new Rect2(at - new Vector2(4, 4), new Vector2(8, 8)), Outline);
                DrawColoredPolygon(new[] { at + new Vector2(-3, -2), at + new Vector2(3, -2), at + new Vector2(0, 3) }, Unknown.Lightened(0.4f));
                if (dungeon.IsDungeonComplete(name))
                {
                    DrawPolyline(new[] { at + new Vector2(2, -1), at + new Vector2(4, 2), at + new Vector2(9, -5) }, new Color(0.4f, 1f, 0.45f), 2f);
                }
            }
        }

        // Everyone: outdoors where they are, in a building at its door, in a dungeon at its stairs.
        var spots = new Dictionary<Vector2, int>();
        foreach (var hero in Heroes())
        {
            var room = dungeon.RoomOf(hero);
            if (WorldMap.OnOverworld(room, hero.GlobalPosition, dungeon.Rooms) is not { } spot || !cells.ContainsKey(spot.Area))
            {
                continue;
            }

            var at = Point(place, cells, spot.Area, spot.Position);
            var key = at.Snapped(Vector2.One * 6);
            var crowd = spots.GetValueOrDefault(key);
            spots[key] = crowd + 1;
            Dot(hero, at + new Vector2(crowd * 5, 0), inside: room.Area != "");
        }

        var here = dungeon.RoomOf(_hero);
        return $"Overworld: you're in {WorldMap.PlaceName(here, dungeon.Rooms)}";
    }

    /// <summary>A dungeon's page; returns its title.</summary>
    private string DrawDungeon(Dungeon dungeon, string area, Rect2 grid)
    {
        var start = dungeon.FoyerOf(area) ?? dungeon.Rooms.First(r => r.Area == area);
        var cells = WorldMap.Dungeon(start);
        var known = cells.Where(c => c.Key.Visited).ToDictionary(c => c.Key, c => c.Value);
        var place = Placer(cells, grid, maxCell: 40f);

        // Corridors between rooms someone's been in.
        foreach (var (room, cell) in known)
        {
            foreach (var door in room.Doors.Where(d => WorldMap.IsWallDoor(d) && d.Partner is not null && known.ContainsKey(d.Partner.Room)))
            {
                DrawLine(place(cell).GetCenter(), place(known[door.Partner!.Room]).GetCenter(), Floor.Darkened(0.3f), 3f);
            }
        }

        var here = dungeon.RoomOf(_hero);
        foreach (var (room, cell) in known)
        {
            var box = place(cell);
            DrawRect(box, Floor);
            DrawRect(box, room == here ? _hero.PlayerColour : Outline, false, room == here ? 2f : 1f);
            if (room.IsBossRoom)
            {
                // The boss: a red mark, or a green tick once it's beaten.
                if (dungeon.IsDungeonComplete(area))
                {
                    var c = box.GetCenter();
                    DrawPolyline(new[] { c + new Vector2(-5, 0), c + new Vector2(-1, 4), c + new Vector2(6, -5) }, new Color(0.25f, 0.75f, 0.3f), 2.5f);
                }
                else
                {
                    DrawCircle(box.GetCenter(), 5f, new Color(0.85f, 0.25f, 0.3f));
                    DrawCircle(box.GetCenter(), 2f, Outline);
                }
            }

            // Every way out of the room: to a room nobody's seen yet, a stub; locked, with a key; out of the dungeon, an arrow.
            foreach (var door in room.Doors.Where(d => d.Partner is not null && !d.Path))
            {
                var side = (Vector2)WorldMap.Step(door.Side);
                var edge = box.GetCenter() + side * box.Size / 2;
                var leadsOut = door.Partner!.Room.Area != area;
                if (!leadsOut && known.ContainsKey(door.Partner.Room) && door.State != DoorState.Locked)
                {
                    continue;
                }

                var end = edge + side * 7f;
                DrawLine(edge, end, leadsOut ? PathColour : Floor.Lightened(0.2f), 3f);
                if (door.State == DoorState.Locked)
                {
                    DrawCircle(edge + side * 4f, 2.5f, Gold);
                }

                if (leadsOut)
                {
                    DrawColoredPolygon(new[] { end + side * 3f, end + new Vector2(-side.Y, side.X) * 3f, end - new Vector2(-side.Y, side.X) * 3f }, PathColour);
                }
            }
        }

        // Heroes in this dungeon: in their rooms. Anyone else: listed underneath.
        var elsewhere = new List<string>();
        foreach (var hero in Heroes())
        {
            var room = dungeon.RoomOf(hero);
            if (room.Area == area && known.ContainsKey(room))
            {
                Dot(hero, Point(place, cells, room, hero.GlobalPosition), inside: false);
            }
            else
            {
                elsewhere.Add($"{hero.Name}: {WorldMap.PlaceName(room, dungeon.Rooms)}");
            }
        }

        if (elsewhere.Count > 0)
        {
            Text(string.Join("   ", elsewhere), new Vector2(grid.GetCenter().X, grid.End.Y + 6), new Color(0.85f, 0.87f, 0.95f), FontSize - 1);
        }

        return here.Area == area ? $"{area}: {here.Title}" : area;
    }

    /// <summary>Where each grid cell's box goes inside <paramref name="grid"/> (cells as big as fit, up to <paramref name="maxCell"/>).</summary>
    private static System.Func<Vector2I, Rect2> Placer(Dictionary<DungeonRoom, Vector2I> cells, Rect2 grid, float maxCell)
    {
        var min = new Vector2I(cells.Values.Min(c => c.X), cells.Values.Min(c => c.Y));
        var max = new Vector2I(cells.Values.Max(c => c.X), cells.Values.Max(c => c.Y));
        var count = max - min + Vector2I.One;
        var cell = Mathf.Min(maxCell, Mathf.Min(grid.Size.X / count.X, grid.Size.Y / count.Y));
        var origin = grid.GetCenter() - new Vector2(count.X, count.Y) * cell / 2;
        var gap = Mathf.Max(2f, cell * 0.14f);
        return c => new Rect2(origin + (Vector2)(c - min) * cell + Vector2.One * gap / 2, Vector2.One * (cell - gap));
    }

    /// <summary>A spot in a room, on the map: the same place inside its box.</summary>
    private static Vector2 Point(System.Func<Vector2I, Rect2> place, Dictionary<DungeonRoom, Vector2I> cells, DungeonRoom room, Vector2 position)
    {
        var box = place(cells[room]);
        var bounds = room.Bounds;
        var fraction = ((position - bounds.Position) / bounds.Size).Clamp(Vector2.Zero, Vector2.One);
        return box.Position + fraction * box.Size;
    }

    /// <summary>A hero's dot in their colour (yours pulses); <paramref name="inside"/>: they're down in a dungeon.</summary>
    private void Dot(Hero hero, Vector2 at, bool inside)
    {
        if (hero == _hero)
        {
            DrawArc(at, 5f + 1.5f * Mathf.Sin(_time * 5f), 0, Mathf.Tau, 16, hero.PlayerColour with { A = 0.7f }, 1.5f);
        }

        DrawCircle(at, 3.5f, Outline);
        DrawCircle(at, 2.5f, hero.PlayerColour);
        if (inside)
        {
            DrawColoredPolygon(new[] { at + new Vector2(-2.5f, 5), at + new Vector2(2.5f, 5), at + new Vector2(0, 8.5f) }, hero.PlayerColour);
        }
    }

    private IEnumerable<Hero> Heroes() =>
        GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).OrderBy(h => h.PlayerIndex);

    private string Hints()
    {
        var pages = _pages.Count > 1 ? $"({_page + 1}/{_pages.Count})  " : "";
        return _hero.Controls.Device.Kind switch
        {
            InputDeviceKind.KeyboardLeft => $"{pages}{(_pages.Count > 1 ? "A D other maps   " : "")}J close",
            InputDeviceKind.KeyboardRight => $"{pages}{(_pages.Count > 1 ? "left right other maps   " : "")}. close",
            _ => $"{pages}{(_pages.Count > 1 ? "left right other maps   " : "")}A close",
        };
    }

    private void Text(string text, Vector2 center, Color colour, int size)
    {
        var font = ThemeDB.FallbackFont;
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var at = center + new Vector2(-width / 2, 0);
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, 3, Outline);
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, colour);
    }

    private static CanvasLayer Layer(Node anyNode)
    {
        var root = anyNode.GetTree().CurrentScene;
        if (root.GetNodeOrNull<CanvasLayer>(LayerName) is { } existing)
        {
            return existing;
        }

        var layer = new CanvasLayer { Name = LayerName, Layer = 9 }; // over the room-name banners
        root.AddChild(layer);
        return layer;
    }
}
