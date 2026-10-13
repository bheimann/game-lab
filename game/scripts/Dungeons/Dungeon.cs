using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Players;
using LizardWizard.UI;

namespace LizardWizard.Dungeons;

/// <summary>
/// Runs a dungeon: which room the team is in, the keys they've found (shared by everyone), and moving
/// the whole team through doors. When the boss room is cleared the dungeon is complete and the team earns a title.
/// </summary>
public partial class Dungeon : Node
{
    public const string Group = "dungeon";

    private const float FadeSeconds = 0.18f;

    private ColorRect _fade = null!;
    private bool _moving;
    private bool _complete;

    /// <summary>Shown when the dungeon starts.</summary>
    [Export] public string DungeonName { get; set; } = "Dungeon";

    /// <summary>The title the heroes earn for freeing the boss (the designer picks the real ones).</summary>
    [Export] public string TitleEarned { get; set; } = "Squire";

    [Export] public DungeonRoom? StartRoom { get; set; }

    /// <summary>Keys the team is carrying. Any player can use them.</summary>
    public int Keys { get; set; }

    public DungeonRoom CurrentRoom { get; private set; } = null!;

    /// <summary>Where knocked-out or newly joined heroes appear: where the team came into this room.</summary>
    public Vector2 RespawnPoint { get; private set; }

    public bool IsComplete => _complete;

    public DungeonHud Hud { get; private set; } = null!;

    public static Dungeon? Find(Node anyNode) => anyNode.GetTree().GetFirstNodeInGroup(Group) as Dungeon;

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

        var rooms = GetTree().CurrentScene.FindChildren("*", "", true, false).OfType<DungeonRoom>().ToList();
        CurrentRoom = StartRoom ?? rooms.First();
        foreach (var room in rooms)
        {
            room.SetActive(room == CurrentRoom);
            if (room.IsBossRoom)
            {
                room.Cleared += () => OnBossCleared(room);
            }
        }

        RespawnPoint = CurrentRoom.FloorRect.GetCenter();
        CallDeferred(MethodName.Begin);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_moving)
        {
            return;
        }

        var heroes = Heroes();
        foreach (var door in CurrentRoom.Doors)
        {
            if (door.Partner is not null && heroes.Any(door.IsLeaving))
            {
                GoThrough(door);
                return;
            }
        }
    }

    /// <summary>Is this the room the team is in? (Things in other rooms wait.)</summary>
    public bool IsCurrent(Node node) => DungeonRoom.Of(node) == CurrentRoom;

    private void Begin()
    {
        if (CoopCamera.Find(this) is { } camera)
        {
            camera.Room = CurrentRoom.Layout;
            camera.Snap();
        }

        CurrentRoom.Visited = true;
        Hud.ShowBanner(DungeonName, CurrentRoom.Title, 3f);
    }

    private void GoThrough(Door door)
    {
        var target = door.Partner!;
        _moving = true;
        var tween = CreateTween();
        tween.TweenProperty(_fade, "color:a", 1f, FadeSeconds);
        tween.TweenCallback(Callable.From(() => Arrive(target)));
        tween.TweenProperty(_fade, "color:a", 0f, FadeSeconds * 1.3f);
        tween.TweenCallback(Callable.From(() => _moving = false));
    }

    private void Arrive(Door door)
    {
        CurrentRoom.SetActive(false);
        CurrentRoom = door.Room;
        CurrentRoom.SetActive(true);
        RespawnPoint = door.EntryPoint;

        // Bombs and smoke stay behind in the old room.
        var heroes = Heroes();
        var actors = heroes.FirstOrDefault()?.GetParent();
        foreach (var leftover in actors?.GetChildren().Where(c => c is Bomb or SmokeCloud) ?? Enumerable.Empty<Node>())
        {
            leftover.QueueFree();
        }

        // Line the team up just inside the door, side by side.
        var across = new Vector2(-door.Outward.Y, door.Outward.X);
        for (var i = 0; i < heroes.Length; i++)
        {
            var spread = (i - (heroes.Length - 1) / 2f) * 14f;
            heroes[i].ArriveAt(door.EntryPoint + across * spread, -door.Outward);
        }

        if (CoopCamera.Find(this) is { } camera)
        {
            camera.Room = CurrentRoom.Layout;
            camera.Snap();
        }

        if (!CurrentRoom.Visited)
        {
            CurrentRoom.Visited = true;
            if (CurrentRoom.Title != "")
            {
                Hud.ShowBanner(CurrentRoom.Title, CurrentRoom.IsBossRoom ? "Boss!" : "", 2.2f);
            }
        }
    }

    private async void OnBossCleared(DungeonRoom room)
    {
        // Let the boss wake up and say thanks first.
        await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);
        _complete = true;
        Hud.ShowBanner("Dungeon complete!", $"The heroes earned a title: {TitleEarned}", 6f);
    }

    private Hero[] Heroes() => GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).ToArray();
}
