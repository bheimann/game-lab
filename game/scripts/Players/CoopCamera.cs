using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Rooms;

namespace LizardWizard.Players;

/// <summary>
/// The camera for one part of the screen (see <see cref="SplitScreen"/>). With <see cref="Follow"/> set it follows
/// that one hero (a split-screen view); without it, it's the shared view: it centres on all the players and zooms out
/// as they spread apart, down to <see cref="MinZoom"/>. At that point <see cref="AllowedRect"/> stops players walking
/// off-screen. The view never shows past the edges of the room (its hero's room; the lead hero's for the shared view).
/// </summary>
public partial class CoopCamera : Camera2D
{
    public const string Group = "coop_camera";

    private readonly RandomNumberGenerator _rng = new();
    private Vector2 _targetCenter;
    private float _shake;

    /// <summary>Closest zoom (used when players are together).</summary>
    [Export] public float MaxZoom { get; set; } = 2f;

    /// <summary>Furthest zoom (players far apart). 1 = the whole 640x360 view.</summary>
    [Export] public float MinZoom { get; set; } = 1f;

    /// <summary>Space to keep around the players, in world pixels.</summary>
    [Export] public float Margin { get; set; } = 56f;

    /// <summary>How quickly the camera catches up (higher = snappier).</summary>
    [Export] public float FollowSpeed { get; set; } = 5f;

    [Export] public RoomBuilder? Room { get; set; }

    /// <summary>The hero this view belongs to (split screen), or null to show everyone (the shared view).</summary>
    public Hero? Follow { get; set; }

    /// <summary>Players are kept inside this rectangle (world coordinates). Only the shared view holds them back.</summary>
    public Rect2 AllowedRect { get; private set; } = Everywhere;

    private static readonly Rect2 Everywhere = new(-1e6f, -1e6f, 2e6f, 2e6f);

    /// <summary>The first view's camera (P1's, or the shared one).</summary>
    public static CoopCamera? Find(Node anyNode) =>
        anyNode.GetTree().GetFirstNodeInGroup(Group) as CoopCamera;

    public static IEnumerable<CoopCamera> All(Node anyNode) =>
        anyNode.GetTree().GetNodesInGroup(Group).OfType<CoopCamera>();

    /// <summary>Shake every view looking at the room <paramref name="anyNode"/> is in by up to <paramref name="pixels"/>
    /// (e.g. 3 for a bomb).</summary>
    public static void ShakeAll(Node anyNode, float pixels)
    {
        foreach (var camera in All(anyNode))
        {
            if (anyNode is Node2D thing && camera.Room is { } room
                && !new Rect2(room.GlobalPosition, room.SizeInPixels).HasPoint(thing.GlobalPosition))
            {
                continue;
            }

            camera.Shake(pixels);
        }
    }

    /// <summary>Every view jumps straight to where it should be (the game just started).</summary>
    public static void SnapAll(Node anyNode)
    {
        foreach (var camera in All(anyNode))
        {
            camera.Snap();
        }
    }

    /// <summary><paramref name="hero"/> just came into a room: their view (or the shared one) jumps there.</summary>
    public static void SnapFor(Hero hero)
    {
        foreach (var camera in All(hero).Where(c => c.Follow == hero || c.Follow is null))
        {
            camera.Snap();
        }
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        ProcessCallback = Camera2DProcessCallback.Physics;
        var (center, zoom) = Target();
        Position = center;
        Zoom = Vector2.One * zoom;
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateRoom();
        var (center, zoom) = Target();
        var t = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
        Zoom = Vector2.One * Mathf.Lerp(Zoom.X, zoom, t);
        Position = Clamp(Position.Lerp(center, t), Zoom.X);
        _targetCenter = center;

        // Screen shake (explosions): a random nudge that dies away quickly.
        _shake = Mathf.MoveToward(_shake, 0, 12f * (float)delta);
        Offset = new Vector2(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1)) * _shake;

        UpdateAllowedRect();
    }

    /// <summary>The widest view the players are allowed to stretch: the min-zoom view around the players.</summary>
    private void UpdateAllowedRect()
    {
        if (Follow is not null)
        {
            AllowedRect = Everywhere;
            return;
        }

        var maxView = ViewSize(MinZoom);
        AllowedRect = new Rect2(_targetCenter - maxView / 2, maxView).Grow(-10f);
    }

    /// <summary>Jump straight to where the camera wants to be (after moving to another room).</summary>
    public void Snap()
    {
        UpdateRoom();
        var (center, zoom) = Target();
        Zoom = Vector2.One * zoom;
        Position = center;
        _targetCenter = center;
        UpdateAllowedRect();
        ResetPhysicsInterpolation();
    }

    /// <summary>Look at the room this view's hero is in (in a dungeon scene; otherwise <see cref="Room"/> stays as set).</summary>
    private void UpdateRoom()
    {
        if (Dungeon.Find(this) is not { } dungeon)
        {
            return;
        }

        if (Follow is not null && !IsInstanceValid(Follow))
        {
            Follow = null;
        }

        Room = (Follow is not null ? dungeon.RoomOf(Follow) : dungeon.CurrentRoom).Layout;
    }

    /// <summary>Shake this view by up to <paramref name="pixels"/>.</summary>
    public void Shake(float pixels) => _shake = Mathf.Max(_shake, pixels);

    private (Vector2 Center, float Zoom) Target()
    {
        if (Follow is not null && !IsInstanceValid(Follow))
        {
            Follow = null;
        }

        var players = Follow is not null
            ? new List<Hero> { Follow }
            : GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(l => !l.IsDown).ToList();
        if (players.Count == 0)
        {
            var roomCenter = Room is null ? Position : Room.GlobalPosition + Room.SizeInPixels / 2;
            return (Clamp(roomCenter, MinZoom), MinZoom);
        }

        var box = new Rect2(players[0].HurtCenter, Vector2.Zero);
        foreach (var p in players.Skip(1))
        {
            box = box.Expand(p.HurtCenter);
        }

        var needed = box.Size + Vector2.One * Margin * 2;
        var screen = GetViewportRect().Size;
        var zoom = Mathf.Clamp(Mathf.Min(screen.X / needed.X, screen.Y / needed.Y), MinZoom, MaxZoom);
        return (Clamp(box.GetCenter(), zoom), zoom);
    }

    private Vector2 ViewSize(float zoom) => GetViewportRect().Size / zoom;

    /// <summary>Keep the view inside the room (centre the room if it's smaller than the view).</summary>
    private Vector2 Clamp(Vector2 center, float zoom)
    {
        if (Room is null)
        {
            return center;
        }

        var room = new Rect2(Room.GlobalPosition, Room.SizeInPixels);
        var half = ViewSize(zoom) / 2;
        return new Vector2(
            room.Size.X <= half.X * 2 ? room.GetCenter().X : Mathf.Clamp(center.X, room.Position.X + half.X, room.End.X - half.X),
            room.Size.Y <= half.Y * 2 ? room.GetCenter().Y : Mathf.Clamp(center.Y, room.Position.Y + half.Y, room.End.Y - half.Y));
    }
}
