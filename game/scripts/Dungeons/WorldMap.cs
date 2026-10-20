using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Rooms;

namespace LizardWizard.Dungeons;

/// <summary>
/// What the map knows about the world (see <see cref="UI.MapView"/>): which rooms go where on it and where a hero
/// shows up. Rooms sit all over the place in the world, so the map lays them out on a grid by how they connect: each
/// door's side says which way the next room is. The overworld map has the outdoor areas (joined by paths); each
/// dungeon's map has its rooms (joined by doors), from the foyer up.
/// </summary>
public static class WorldMap
{
    /// <summary>Outdoors (an area of the overworld or a village), not a building or a dungeon.</summary>
    public static bool IsOutdoors(DungeonRoom room) => room.Area == "" && room.Layout is OutdoorBuilder;

    /// <summary>The overworld's areas on a grid, starting from <paramref name="home"/>.</summary>
    public static Dictionary<DungeonRoom, Vector2I> Overworld(DungeonRoom home) =>
        Grid(home, d => d.Path && d.Partner!.Room is var next && IsOutdoors(next));

    /// <summary>A dungeon's rooms on a grid, starting from <paramref name="foyer"/> (or any room of it).</summary>
    public static Dictionary<DungeonRoom, Vector2I> Dungeon(DungeonRoom foyer) =>
        Grid(foyer, d => IsWallDoor(d) && d.Partner!.Room.Area == foyer.Area);

    /// <summary>A door in a room's wall (not stairs, a path or a building's front door): its side is a direction.</summary>
    public static bool IsWallDoor(Door door) => !door.Stairs && !door.Path && !door.Doorway;

    /// <summary>
    /// Where something in <paramref name="room"/> at <paramref name="position"/> shows on the overworld map: outdoors,
    /// right there; in a building, at its front door in the village; in a dungeon, at its stairs.
    /// </summary>
    public static (DungeonRoom Area, Vector2 Position)? OnOverworld(DungeonRoom room, Vector2 position, IEnumerable<DungeonRoom> rooms)
    {
        if (IsOutdoors(room))
        {
            return (room, position);
        }

        if (room.Area != "")
        {
            return StairsInto(room.Area, rooms) is { } stairs ? (stairs.Room, stairs.GlobalPosition) : null;
        }

        // A building: go out through its doors until you're outside.
        var seen = new HashSet<DungeonRoom> { room };
        var queue = new Queue<DungeonRoom>();
        queue.Enqueue(room);
        while (queue.Count > 0)
        {
            foreach (var door in queue.Dequeue().Doors.Where(d => d.Partner is not null))
            {
                var next = door.Partner!.Room;
                if (IsOutdoors(next))
                {
                    return (next, door.Partner.GlobalPosition);
                }

                if (next.Area == "" && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    /// <summary>The stairs down into a dungeon, out in the overworld.</summary>
    public static Door? StairsInto(string area, IEnumerable<DungeonRoom> rooms) =>
        rooms.Where(IsOutdoors).SelectMany(r => r.Doors).FirstOrDefault(d => d.Stairs && d.Partner?.Room.Area == area);

    /// <summary>What to call where a hero is ("Tailspin Swamp", "Scalebrook Village", "The Old Burrow").</summary>
    public static string PlaceName(DungeonRoom room, IEnumerable<DungeonRoom> rooms) =>
        room.Area != "" ? room.Area
        : IsOutdoors(room) ? room.Title
        : OnOverworld(room, room.GlobalPosition, rooms) is { } outside ? outside.Area.Title
        : room.Title;

    /// <summary>Lay rooms out on a grid by following <paramref name="follow"/>ed doors (a taken cell pushes further out).</summary>
    private static Dictionary<DungeonRoom, Vector2I> Grid(DungeonRoom start, Func<Door, bool> follow)
    {
        var cells = new Dictionary<DungeonRoom, Vector2I> { [start] = Vector2I.Zero };
        var taken = new HashSet<Vector2I> { Vector2I.Zero };
        var queue = new Queue<DungeonRoom>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            foreach (var door in room.Doors.Where(d => d.Partner is not null && follow(d)))
            {
                var next = door.Partner!.Room;
                if (cells.ContainsKey(next))
                {
                    continue;
                }

                var step = Step(door.Side);
                var cell = cells[room] + step;
                while (taken.Contains(cell))
                {
                    cell += step;
                }

                cells[next] = cell;
                taken.Add(cell);
                queue.Enqueue(next);
            }
        }

        return cells;
    }

    public static Vector2I Step(DoorSide side) => side switch
    {
        DoorSide.Top => Vector2I.Up,
        DoorSide.Bottom => Vector2I.Down,
        DoorSide.Left => Vector2I.Left,
        _ => Vector2I.Right,
    };
}
