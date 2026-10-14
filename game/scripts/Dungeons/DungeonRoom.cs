using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Rooms;

namespace LizardWizard.Dungeons;

/// <summary>
/// One room of a dungeon. Needs a child <see cref="RoomBuilder"/> called "Layout"; doors, enemies, switches
/// and treasure are its other children. Only the room the players are in runs and is shown; the others wait.
/// </summary>
public partial class DungeonRoom : Node2D
{
    private bool _wasCleared;

    /// <summary>Shown when the players walk in for the first time.</summary>
    [Export] public string Title { get; set; } = "";

    /// <summary>Which dungeon this room belongs to (e.g. "The Old Burrow"); empty for the overworld.
    /// Walking into a different area shows its name.</summary>
    [Export] public string Area { get; set; } = "";

    /// <summary>Beating this room's enemies completes the dungeon.</summary>
    [Export] public bool IsBossRoom { get; set; }

    /// <summary>Raised once, when the last enemy in the room is beaten.</summary>
    public event Action? Cleared;

    public RoomBuilder Layout => GetNode<RoomBuilder>("Layout");

    public bool IsActive { get; private set; } = true;

    public bool Visited { get; set; }

    /// <summary>The walkable floor, in global coordinates.</summary>
    public Rect2 FloorRect => new(Layout.GlobalPosition + Layout.FloorRect.Position, Layout.FloorRect.Size);

    public IEnumerable<IEnemy> Enemies => FindChildren("*", "", true, false).OfType<IEnemy>();

    /// <summary>True when no enemy in the room is still under Mossimus's trick (also true for rooms with no enemies).</summary>
    public bool IsCleared => !Enemies.Any(e => e.IsHostile);

    public IEnumerable<Door> Doors => GetChildren().OfType<Door>();

    /// <summary>The room that contains <paramref name="node"/>, if any.</summary>
    public static DungeonRoom? Of(Node node)
    {
        for (var n = node.GetParent(); n is not null; n = n.GetParent())
        {
            if (n is DungeonRoom room)
            {
                return room;
            }
        }

        return null;
    }

    public override void _Ready()
    {
        YSortEnabled = true;
        _wasCleared = IsCleared;
        foreach (var door in Doors)
        {
            door.Setup(this);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_wasCleared && IsCleared)
        {
            _wasCleared = true;
            Cleared?.Invoke();
        }
    }

    /// <summary>After loading a save: a room that's already cleared doesn't announce it again.</summary>
    public void SyncCleared() => _wasCleared = IsCleared;

    /// <summary>Only the players' room runs and is drawn.</summary>
    public void SetActive(bool active)
    {
        IsActive = active;
        Visible = active;
        ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }
}
