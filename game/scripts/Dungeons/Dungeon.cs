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
/// whenever someone walks into it while it's empty. A hero waiting on a <see cref="GatherMarker"/> (dungeon foyers,
/// teleport rooms) can be joined by the others from their menus; the last marker anyone reached is where the team
/// starts after loading a save.
/// Everyone goes in to a boss together: walking into the boss door holds you in the doorway (pushing back steps out
/// again) until the whole team is there.
/// A knocked-out hero gets back up in the dungeon's foyer, or (in the overworld) in the last village they visited.
/// Knocked out fighting a boss, they wait in the boss hall until the fight's over; if everyone's knocked out, the
/// boss starts over and the team can try again. Heroes who join the game start in Scalebrook Village (or, during a
/// boss fight, waiting in that boss hall).
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
    private readonly HashSet<Hero> _moving = new();
    private HashSet<DungeonRoom> _occupied = new();
    private List<DungeonRoom> _rooms = new();
    private DungeonRoom _idleRoom = null!;
    private DungeonRoom _startRoom = null!;
    private Vector2 _startPoint;
    private readonly Dictionary<Hero, DungeonRoom> _lastVillage = new();
    private readonly Dictionary<Hero, DungeonRoom> _benched = new(); // hero -> the boss room whose fight they wait out
    private readonly Dictionary<Hero, float> _benchMessage = new();
    private readonly Dictionary<Hero, Door> _atBossDoor = new(); // held in the boss doorway, waiting for the team

    /// <summary>Where heroes come into a room: the spot, which way they face, and which way to line up side by side.</summary>
    private readonly record struct Arrival(DungeonRoom Room, Vector2 Point, Vector2 Facing, Vector2 Across)
    {
        public static Arrival Through(Door door) =>
            new(door.Room, door.EntryPoint, -door.Outward, new Vector2(-door.Outward.Y, door.Outward.X));
    }

    /// <summary>Shown when the dungeon starts.</summary>
    [Export] public string DungeonName { get; set; } = "Dungeon";

    /// <summary>The title the heroes earn for freeing the boss (the designer picks the real ones).</summary>
    [Export] public string TitleEarned { get; set; } = "Squire";

    [Export] public DungeonRoom? StartRoom { get; set; }

    /// <summary>Where the heroes first appear (defaults to the middle of the start room).</summary>
    [Export] public Node2D? StartPoint { get; set; }

    /// <summary>Where the exit portal takes the team after beating the boss (the dungeon's entrance in the overworld).</summary>
    [Export] public Door? ReturnDoor { get; set; }

    /// <summary>The last gathering marker anyone reached (a dungeon foyer or a teleport room): where a loaded game starts.</summary>
    public GatherMarker? LastGatherPoint { get; private set; }

    /// <summary>Keys the team is carrying. Any player can use them.</summary>
    public int Keys { get; set; }

    /// <summary>The lead hero's room (the lowest player number playing), or where the last one was.</summary>
    public DungeonRoom CurrentRoom => Heroes().OrderBy(h => h.PlayerIndex).FirstOrDefault() is { } lead ? RoomOf(lead) : _idleRoom;

    /// <summary>Where newly joined heroes appear when nobody's playing: where a hero last came into a room.</summary>
    public Vector2 RespawnPoint { get; private set; }

    /// <summary>A hero went through a door (the one they left by), before they arrive on the other side.</summary>
    public event Action<Hero, Door>? HeroLeftRoom;

    /// <summary>A knocked-out hero got back up and is being sent to the foyer, a village or the boss hall.</summary>
    public event Action<Hero>? HeroRevived;

    /// <summary>Knocked out fighting a boss: waiting in the boss hall until the fight's over.</summary>
    public bool IsBenched(Hero hero) => _benched.ContainsKey(hero);

    /// <summary>Held in the boss doorway, waiting for the rest of the team to go in together.</summary>
    public bool IsAtBossDoor(Hero hero) => _atBossDoor.ContainsKey(hero);

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
        if (room.IsVillage)
        {
            _lastVillage[hero] = room;
        }

        // Joined during a boss fight: waits in the boss hall until it's over.
        if (FightingBossRoom() is { } boss && boss != room && BossHallOf(boss) == room)
        {
            _benched[hero] = boss;
        }

        return room;
    }

    /// <summary>Is <paramref name="hero"/> in the same room as <paramref name="thing"/> (true for things outside any room)?</summary>
    public bool SharesRoom(Hero hero, Node thing) => DungeonRoom.Of(thing) is not { } room || RoomOf(hero) == room;

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
        // A loaded game starts at the last gathering marker anyone reached (a foyer or a teleport room).
        LastGatherPoint = SaveGame.Loaded?.GatherPoint is { Length: > 0 } path ? GetTree().CurrentScene.GetNodeOrNull<GatherMarker>(path) : null;
        var savedRoom = LastGatherPoint is not null ? DungeonRoom.Of(LastGatherPoint) : null;
        _idleRoom = DevStartRoom(_rooms) ?? savedRoom ?? StartRoom ?? _rooms.First();
        _occupied = new HashSet<DungeonRoom> { _idleRoom };
        foreach (var room in _rooms)
        {
            room.SetActive(room == _idleRoom);
            if (room.IsBossRoom)
            {
                room.Cleared += () => OnBossCleared(room);
            }
        }

        RespawnPoint = savedRoom == _idleRoom && LastGatherPoint is not null ? LastGatherPoint.JoinPoint
            : StartPoint is not null && DungeonRoom.Of(StartPoint) == _idleRoom ? StartPoint.GlobalPosition
            : _idleRoom.FloorRect.GetCenter();
        _startRoom = _idleRoom;
        _startPoint = RespawnPoint;
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
            _moving.Remove(gone);
            _lastVillage.Remove(gone);
            _benched.Remove(gone);
            _benchMessage.Remove(gone);
            _atBossDoor.Remove(gone);
        }

        foreach (var hero in _benchMessage.Keys.ToList())
        {
            _benchMessage[hero] -= (float)delta;
        }

        foreach (var hero in Heroes().Where(h => !_moving.Contains(h) && !_atBossDoor.ContainsKey(h)))
        {
            if (RoomOf(hero).Doors.FirstOrDefault(d => d.Partner is not null && d.IsLeaving(hero)) is { } door)
            {
                if (door.LeadsToBoss && !IsBenched(hero))
                {
                    // Into the boss doorway: wait there for the rest of the team.
                    _atBossDoor[hero] = door;
                    hero.IsHeld = true;
                    _benchMessage[hero] = 0;
                    continue;
                }

                if (door.LeadsToBoss)
                {
                    // Knocked out of the boss fight: no going back in until it's over.
                    hero.Teleport(door.EntryPoint);
                    if (_benchMessage.GetValueOrDefault(hero) <= 0)
                    {
                        _benchMessage[hero] = 3f;
                        FloatingText.Spawn(hero, hero.HurtCenter + new Vector2(0, -14), "You were knocked out.\nWait here until the fight is over!",
                            new Color(1f, 0.85f, 0.4f), 2.2f, 7f);
                    }

                    continue;
                }

                HeroLeftRoom?.Invoke(hero, door);
                MoveTo(new[] { hero }, Arrival.Through(door.Partner!));
            }
        }

        UpdateBossDoors();
        UpdateBossFights();
        UpdateRooms();
    }

    private void Begin()
    {
        SaveGame.Apply(this);
        CoopCamera.SnapAll(this);

        _idleRoom.Visited = true;
        Hud.ShowBanner(_idleRoom.Area != "" ? _idleRoom.Area : _idleRoom.Title, _idleRoom.Area != "" ? _idleRoom.Title : "", 3f);
    }

    /// <summary>Fade the heroes' views out, bring them in at <paramref name="target"/>, fade back in.</summary>
    private void MoveTo(Hero[] heroes, Arrival target)
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

    private void Arrive(Hero[] heroes, Arrival arrival)
    {
        heroes = heroes.Where(h => IsInstanceValid(h) && h.IsInGame).ToArray();
        var previousAreas = heroes.ToDictionary(h => h, h => RoomOf(h).Area);

        // Line the heroes up just inside the door (or by the marker), side by side.
        for (var i = 0; i < heroes.Length; i++)
        {
            var spread = (i - (heroes.Length - 1) / 2f) * 14f;
            heroes[i].ArriveAt(arrival.Point + arrival.Across * spread, arrival.Facing);
            _heroRooms[heroes[i]] = arrival.Room;
            if (arrival.Room.IsVillage)
            {
                _lastVillage[heroes[i]] = arrival.Room;
            }
        }

        var room = arrival.Room;
        RespawnPoint = arrival.Point;
        if (room.GetChildren().OfType<GatherMarker>().FirstOrDefault() is { } marker)
        {
            LastGatherPoint = marker;
        }

        UpdateRooms(); // the new room wakes up (and an empty overworld area gets its animals back)

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
    /// <paramref name="hero"/> was knocked out and is getting back up: in the boss hall if they were fighting a boss
    /// (waiting there until the fight's over), in the dungeon's foyer anywhere else in a dungeon, or in the last
    /// village they visited out in the overworld.
    /// </summary>
    public void Revive(Hero hero)
    {
        var room = RoomOf(hero);
        Arrival target;
        if (room is { IsBossRoom: true, IsCleared: false } && BossHallOf(room) is { } hall)
        {
            _benched[hero] = room;
            target = new Arrival(hall, hall.FloorRect.GetCenter(), Vector2.Up, Vector2.Right);
        }
        else if (room.Area != "" && FoyerOf(room.Area) is { } foyer)
        {
            target = new Arrival(foyer, foyer.GetChildren().OfType<GatherMarker>().First().JoinPoint, Vector2.Up, Vector2.Right);
        }
        else
        {
            var village = _lastVillage.GetValueOrDefault(hero) ?? HomeVillage;
            target = new Arrival(village, VillageSpot(village), Vector2.Down, Vector2.Right);
        }

        HeroRevived?.Invoke(hero);
        MoveTo(new[] { hero }, target);
    }

    /// <summary>
    /// Where a hero who joins the game appears: with the team if nobody's left the room where the game started (or
    /// nobody's playing yet); during a boss fight, in that boss hall; otherwise in Scalebrook Village.
    /// </summary>
    public Vector2 JoinPoint()
    {
        var heroes = Heroes();
        if (heroes.Length == 0)
        {
            return RespawnPoint;
        }

        if (heroes.All(h => RoomOf(h) == _startRoom))
        {
            return _startPoint;
        }

        if (FightingBossRoom() is { } boss && BossHallOf(boss) is { } hall)
        {
            return hall.FloorRect.GetCenter();
        }

        return VillageSpot(HomeVillage);
    }

    /// <summary>
    /// Heroes held in a boss doorway: pushing back (away from the door) steps out into the boss hall again; once the
    /// whole team is in the doorway, everyone goes in to the boss together.
    /// </summary>
    private void UpdateBossDoors()
    {
        foreach (var (hero, door) in _atBossDoor.ToList())
        {
            if (!door.LeadsToBoss || hero.IsDown)
            {
                Release(hero);
            }
            else if (hero.Controls.Move.Dot(door.Outward) < -0.5f)
            {
                Release(hero);
                hero.Teleport(door.EntryPoint);
            }
        }

        var heroes = Heroes();
        foreach (var door in _atBossDoor.Values.Distinct().ToList())
        {
            var waiting = _atBossDoor.Where(a => a.Value == door).Select(a => a.Key).ToArray();
            var missing = heroes.Except(waiting).ToList();
            if (missing.Count == 0)
            {
                foreach (var hero in waiting)
                {
                    Release(hero);
                }

                MoveTo(waiting, Arrival.Through(door.Partner!));
                continue;
            }

            foreach (var hero in waiting.Where(h => _benchMessage.GetValueOrDefault(h) <= 0))
            {
                _benchMessage[hero] = 3f;
                FloatingText.Spawn(hero, hero.HurtCenter + new Vector2(0, -14),
                    $"Everyone goes in together.\nWaiting for {string.Join(" and ", missing.OrderBy(h => h.PlayerIndex).Select(h => h.Name))}... (push back to step out)",
                    new Color(1f, 0.85f, 0.4f), 2.6f, 7f);
            }
        }
    }

    private void Release(Hero hero)
    {
        _atBossDoor.Remove(hero);
        hero.IsHeld = false;
    }

    /// <summary>
    /// The boss fights going on: when the boss is beaten, everyone waiting in the boss hall is free to go in; when
    /// nobody's left in the boss room (everyone was knocked out), the boss starts over and the team can try again.
    /// </summary>
    private void UpdateBossFights()
    {
        foreach (var boss in _benched.Values.Distinct().ToList())
        {
            if (boss.IsCleared)
            {
                Unbench(boss);
            }
            else if (!Heroes().Any(h => RoomOf(h) == boss))
            {
                foreach (var enemy in boss.Enemies)
                {
                    enemy.Respawn();
                }

                foreach (var hero in _benched.Where(b => b.Value == boss).Select(b => b.Key))
                {
                    Hud.ShowBanner("Everyone was knocked out!", "Gather up and try again", 3f, hero);
                }

                Unbench(boss);
            }
        }
    }

    private void Unbench(DungeonRoom boss)
    {
        foreach (var hero in _benched.Where(b => b.Value == boss).Select(b => b.Key).ToList())
        {
            _benched.Remove(hero);
        }
    }

    /// <summary>A boss room with someone in it, still to be beaten.</summary>
    private DungeonRoom? FightingBossRoom() => Heroes().Select(RoomOf).FirstOrDefault(r => r is { IsBossRoom: true, IsCleared: false });

    /// <summary>The small room in front of a boss room.</summary>
    private DungeonRoom? BossHallOf(DungeonRoom boss) => _rooms.FirstOrDefault(r => !r.IsBossRoom && r.Doors.Any(d => d.Partner?.Room == boss));

    /// <summary>A dungeon's foyer: its room with a gathering marker.</summary>
    private DungeonRoom? FoyerOf(string area) => _rooms.FirstOrDefault(r => r.Area == area && r.GetChildren().OfType<GatherMarker>().Any());

    /// <summary>Scalebrook Village, where the game starts.</summary>
    private DungeonRoom HomeVillage => (StartPoint is not null ? DungeonRoom.Of(StartPoint) : null) ?? _rooms.FirstOrDefault(r => r.IsVillage) ?? _startRoom;

    /// <summary>Where to put someone in a village: its start spot, or where its path in from the overworld arrives.</summary>
    private Vector2 VillageSpot(DungeonRoom village) =>
        StartPoint is not null && DungeonRoom.Of(StartPoint) == village ? StartPoint.GlobalPosition
        : village.Doors.FirstOrDefault(d => d.Path)?.EntryPoint ?? village.FloorRect.GetCenter();

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
    public void Travel(Door arrival, IEnumerable<Hero> heroes) => MoveTo(heroes.ToArray(), Arrival.Through(arrival));

    /// <summary>
    /// <paramref name="hero"/> picked "Join" in their menu: bring them to <paramref name="marker"/>, where a teammate is
    /// waiting. Only while someone's still waiting there.
    /// </summary>
    public void JoinAt(Hero hero, GatherMarker marker)
    {
        if (DungeonRoom.Of(marker) is { } room && marker.Waiting.Any(h => h != hero))
        {
            MoveTo(new[] { hero }, new Arrival(room, marker.JoinPoint, Vector2.Up, Vector2.Right));
        }
    }

    /// <summary>Heroes stood on the exit portal in <paramref name="room"/>: take them back out to the overworld.</summary>
    public void LeaveThroughPortal(DungeonRoom room, IEnumerable<Hero> heroes)
    {
        if ((room.ReturnDoor ?? ReturnDoor) is { } back)
        {
            MoveTo(heroes.ToArray(), Arrival.Through(back));
        }
    }

    private Hero[] Heroes() => GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).ToArray();
}
