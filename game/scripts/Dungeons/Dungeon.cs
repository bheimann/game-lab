using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Players;
using LizardWizard.Saving;
using LizardWizard.UI;

namespace LizardWizard.Dungeons;

/// <summary>
/// Runs the world: which room (or overworld area) each hero is in, the keys they've found (shared by everyone),
/// and moving heroes through doors. Heroes go their own ways: a door, teleport pad or exit portal takes just the heroes
/// who used it, and every room with a hero in it runs (the others wait). A dungeon's beaten enemies stay beaten while
/// any hero is inside it, and are all back as soon as the last hero leaves; an overworld area's animals are back
/// whenever someone walks into it while it's empty.
/// When the boss room is cleared the dungeon is complete, the team earns a title, and an <see cref="ExitPortal"/> opens
/// in the boss room that takes them back out to the dungeon's entrance.
/// There can be several dungeons: each one's rooms share an <see cref="DungeonRoom.Area"/> name, and its boss room says
/// where the portal goes and which title it gives (falling back to <see cref="ReturnDoor"/> and <see cref="TitleEarned"/>).
/// </summary>
public partial class Dungeon : Node
{
    public const string Group = "dungeon";

    private const float FadeSeconds = 0.18f;

    private ColorRect _fade = null!;
    private readonly HashSet<string> _completed = new();
    private readonly Dictionary<Hero, DungeonRoom> _heroRooms = new();
    private readonly Dictionary<Hero, Vector2> _respawnPoints = new();
    private readonly HashSet<Hero> _moving = new();
    private HashSet<DungeonRoom> _occupied = new();
    private List<DungeonRoom> _rooms = new();
    private DungeonRoom _idleRoom = null!;

    /// <summary>Shown when the dungeon starts.</summary>
    [Export] public string DungeonName { get; set; } = "Dungeon";

    /// <summary>The title the heroes earn for freeing the boss (the designer picks the real ones).</summary>
    [Export] public string TitleEarned { get; set; } = "Squire";

    [Export] public DungeonRoom? StartRoom { get; set; }

    /// <summary>Where the heroes first appear (defaults to the middle of the start room).</summary>
    [Export] public Node2D? StartPoint { get; set; }

    /// <summary>Where the exit portal takes the team after beating the boss (the dungeon's entrance in the overworld).</summary>
    [Export] public Door? ReturnDoor { get; set; }

    /// <summary>Keys the team is carrying. Any player can use them.</summary>
    public int Keys { get; set; }

    /// <summary>The lead hero's room (the lowest player number playing), or where the last one was.</summary>
    public DungeonRoom CurrentRoom => Heroes().OrderBy(h => h.PlayerIndex).FirstOrDefault() is { } lead ? RoomOf(lead) : _idleRoom;

    /// <summary>Where newly joined heroes appear when nobody's playing: where a hero last came into a room.</summary>
    public Vector2 RespawnPoint { get; private set; }

    /// <summary>A hero went through a door (the one they left by), before they arrive on the other side.</summary>
    public event Action<Hero, Door>? HeroLeftRoom;

    /// <summary>Is the dungeon these rooms belong to (by <see cref="DungeonRoom.Area"/>) beaten?</summary>
    public bool IsDungeonComplete(string area) => _completed.Contains(area);

    /// <summary>Every dungeon beaten so far (by area name).</summary>
    public IEnumerable<string> CompletedDungeons => _completed;

    /// <summary>Loading a save: the boss was already beaten.</summary>
    public void RestoreCompleted(IEnumerable<string> areas)
    {
        _completed.Clear();
        _completed.UnionWith(areas);
    }

    public DungeonHud Hud { get; private set; } = null!;

    public static Dungeon? Find(Node anyNode) => anyNode.GetTree().GetFirstNodeInGroup(Group) as Dungeon;

    /// <summary>The room <paramref name="hero"/> is in.</summary>
    public DungeonRoom RoomOf(Hero hero)
    {
        if (_heroRooms.TryGetValue(hero, out var room))
        {
            return room;
        }

        // Just joined: wherever they appeared.
        room = RoomAt(hero.GlobalPosition) ?? _idleRoom;
        _heroRooms[hero] = room;
        _respawnPoints[hero] = hero.GlobalPosition;
        return room;
    }

    /// <summary>Is <paramref name="hero"/> in the same room as <paramref name="thing"/> (true for things outside any room)?</summary>
    public bool SharesRoom(Hero hero, Node thing) => DungeonRoom.Of(thing) is not { } room || RoomOf(hero) == room;

    /// <summary>Where a knocked-out hero gets up when nobody else in their room is standing: where they came in.</summary>
    public Vector2 RespawnPointFor(Hero hero) => _respawnPoints.TryGetValue(hero, out var point) ? point : RespawnPoint;

    /// <summary>Is this thing in a room someone's in? (Things in other rooms wait.)</summary>
    public bool IsCurrent(Node node) => DungeonRoom.Of(node) is { IsActive: true };

    public override void _Ready()
    {
        AddToGroup(Group);

        var layer = new CanvasLayer { Name = "DungeonLayer", Layer = 8 };
        AddChild(layer);
        Hud = new DungeonHud { Dungeon = this };
        Hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Hud.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.AddChild(Hud);
        _fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_fade);

        _rooms = GetTree().CurrentScene.FindChildren("*", "", true, false).OfType<DungeonRoom>().ToList();
        _idleRoom = DevStartRoom(_rooms) ?? StartRoom ?? _rooms.First();
        _occupied = new HashSet<DungeonRoom> { _idleRoom };
        foreach (var room in _rooms)
        {
            room.SetActive(room == _idleRoom);
            if (room.IsBossRoom)
            {
                room.Cleared += () => OnBossCleared(room);
            }
        }

        RespawnPoint = StartPoint is not null && DungeonRoom.Of(StartPoint) == _idleRoom
            ? StartPoint.GlobalPosition
            : _idleRoom.FloorRect.GetCenter();
        CallDeferred(MethodName.Begin);
    }

    /// <summary>Dev helper: run with <c>-- --room=PuzzleRoom</c> to start in that room (handy with --screenshot).</summary>
    private static DungeonRoom? DevStartRoom(List<DungeonRoom> rooms) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--room=")) is { } arg
            ? rooms.FirstOrDefault(r => r.Name == arg["--room=".Length..])
            : null;

    public override void _PhysicsProcess(double delta)
    {
        // Forget heroes who've left the game.
        foreach (var gone in _heroRooms.Keys.Where(h => !IsInstanceValid(h) || !h.IsInGame).ToList())
        {
            _heroRooms.Remove(gone);
            _respawnPoints.Remove(gone);
            _moving.Remove(gone);
        }

        foreach (var hero in Heroes().Where(h => !_moving.Contains(h)))
        {
            if (RoomOf(hero).Doors.FirstOrDefault(d => d.Partner is not null && d.IsLeaving(hero)) is { } door)
            {
                HeroLeftRoom?.Invoke(hero, door);
                MoveTo(new[] { hero }, door.Partner!);
            }
        }

        UpdateRooms();
    }

    private void Begin()
    {
        SaveGame.Apply(this);
        CoopCamera.SnapAll(this);

        _idleRoom.Visited = true;
        Hud.ShowBanner(_idleRoom.Area != "" ? _idleRoom.Area : _idleRoom.Title, _idleRoom.Area != "" ? _idleRoom.Title : "", 3f);
    }

    /// <summary>Fade the heroes' views out, bring them in through <paramref name="target"/>, fade back in.</summary>
    private void MoveTo(Hero[] heroes, Door target)
    {
        heroes = heroes.Where(h => !_moving.Contains(h)).ToArray();
        if (heroes.Length == 0)
        {
            return;
        }

        _moving.UnionWith(heroes);
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(a => SetFade(heroes, a)), 0f, 1f, FadeSeconds);
        tween.TweenCallback(Callable.From(() => Arrive(heroes, target)));
        tween.TweenMethod(Callable.From<float>(a => SetFade(heroes, a)), 1f, 0f, FadeSeconds * 1.3f);
        tween.TweenCallback(Callable.From(() => _moving.ExceptWith(heroes)));
    }

    /// <summary>Darken just these heroes' parts of the screen (the whole screen without split screen).</summary>
    private void SetFade(Hero[] heroes, float alpha)
    {
        if (SplitScreen.Find(this) is { } split)
        {
            foreach (var hero in heroes)
            {
                split.SetFade(hero, alpha);
            }
        }
        else
        {
            _fade.Color = _fade.Color with { A = alpha };
        }
    }

    private void Arrive(Hero[] heroes, Door door)
    {
        heroes = heroes.Where(h => IsInstanceValid(h) && h.IsInGame).ToArray();
        var previousAreas = heroes.ToDictionary(h => h, h => RoomOf(h).Area);

        // Line the heroes up just inside the door, side by side.
        var across = new Vector2(-door.Outward.Y, door.Outward.X);
        for (var i = 0; i < heroes.Length; i++)
        {
            var spread = (i - (heroes.Length - 1) / 2f) * 14f;
            heroes[i].ArriveAt(door.EntryPoint + across * spread, -door.Outward);
            _heroRooms[heroes[i]] = door.Room;
            _respawnPoints[heroes[i]] = door.EntryPoint;
        }

        RespawnPoint = door.EntryPoint;
        UpdateRooms(); // the new room wakes up (and an empty overworld area gets its animals back)

        var room = door.Room;
        foreach (var hero in heroes)
        {
            CoopCamera.SnapFor(hero);
        }

        SaveGame.Save(this);

        foreach (var hero in heroes)
        {
            if (room.Area != previousAreas[hero] && room.Area != "")
            {
                // Into a dungeon.
                Hud.ShowBanner(room.Area, room.Title, 3f, hero);
            }
            else if ((!room.Visited || room.Area == "") && room.Title != "")
            {
                // A dungeon room's name shows the first time; an overworld area's name shows every time you walk in.
                Hud.ShowBanner(room.Title, room.IsBossRoom ? "Boss!" : "", 2.2f, hero);
            }
        }

        room.Visited = true;
    }

    /// <summary>
    /// Wake the rooms that now have a hero in them and put the others to sleep. A room nobody's in any more loses its
    /// leftover bombs, smoke and vehicles; a dungeon nobody's in any more gets all its enemies back.
    /// </summary>
    private void UpdateRooms()
    {
        var heroes = Heroes();
        if (heroes.Length > 0)
        {
            _idleRoom = CurrentRoom;
        }

        var occupied = heroes.Select(RoomOf).ToHashSet();
        if (occupied.Count == 0)
        {
            occupied.Add(_idleRoom);
        }

        if (occupied.SetEquals(_occupied))
        {
            return;
        }

        var emptied = _occupied.Except(occupied).ToList();
        var entered = occupied.Except(_occupied).ToList();
        var leftAreas = _occupied.Select(r => r.Area).Except(occupied.Select(r => r.Area)).Where(a => a != "").ToList();
        _occupied = occupied;

        foreach (var room in emptied)
        {
            room.SetActive(false);
            ClearLeftovers(room);
        }

        // Everyone's out of the dungeon: its enemies are back (bosses stay beaten; doors stay open, chests stay opened).
        foreach (var room in _rooms.Where(r => leftAreas.Contains(r.Area) && r.Visited))
        {
            foreach (var enemy in room.Enemies)
            {
                enemy.Respawn();
            }
        }

        foreach (var room in entered)
        {
            room.SetActive(true);

            // Walking into an empty overworld area you've been to before: its animals are back.
            if (room.Area == "" && room.Visited)
            {
                foreach (var enemy in room.Enemies)
                {
                    enemy.Respawn();
                }
            }
        }
    }

    /// <summary>Bombs, smoke, shots and vehicles left behind in a room nobody's in any more go away.</summary>
    private void ClearLeftovers(DungeonRoom room)
    {
        var area = room.Bounds.Grow(32);
        var actors = PlayerManager.Find(this)?.ActorsParent;
        foreach (var leftover in actors?.GetChildren().OfType<Node2D>().Where(c => c is Bomb or SmokeCloud or Lure or Bullet or EnemyShot or Stampede or Vehicle) ?? Enumerable.Empty<Node2D>())
        {
            if (area.HasPoint(leftover.GlobalPosition))
            {
                leftover.QueueFree();
            }
        }
    }

    /// <summary>The room at this point of the world (rooms don't overlap, but an active one wins if they do).</summary>
    private DungeonRoom? RoomAt(Vector2 point) =>
        _rooms.Where(r => r.Bounds.HasPoint(point)).OrderByDescending(r => r.IsActive).FirstOrDefault();

    private async void OnBossCleared(DungeonRoom room)
    {
        // Let the boss wake up and say thanks first.
        await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);
        _completed.Add(room.Area);
        var title = room.TitleEarned != "" ? room.TitleEarned : TitleEarned;
        Hud.ShowBanner($"{room.Area} complete!", $"The heroes earned a title: {title}", 6f);
        SaveGame.Save(this);

        // An exit portal opens in the boss room (see ExitPortal); the heroes leave when they're ready.
    }

    /// <summary>A teleport pad: take the heroes standing on it to <paramref name="arrival"/> (another village's teleport room).</summary>
    public void Travel(Door arrival, IEnumerable<Hero> heroes) => MoveTo(heroes.ToArray(), arrival);

    /// <summary>Heroes stood on the exit portal in <paramref name="room"/>: take them back out to the overworld.</summary>
    public void LeaveThroughPortal(DungeonRoom room, IEnumerable<Hero> heroes)
    {
        if ((room.ReturnDoor ?? ReturnDoor) is { } back)
        {
            MoveTo(heroes.ToArray(), back);
        }
    }

    private Hero[] Heroes() => GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).ToArray();
}
