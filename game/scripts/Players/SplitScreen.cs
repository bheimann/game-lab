using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;

namespace LizardWizard.Players;

/// <summary>
/// Split screen: every hero gets their own part of the window, even when they're standing together.
/// 1 hero: the whole screen. 2: left / right halves. 3: P1 top left, P2 the right half, P3 bottom left.
/// 4: a quarter each. While everyone's in the same boss room fighting the boss they share one full-screen view (see
/// <see cref="Shared"/>); once it's beaten, the screen splits again.
/// Each view is a <see cref="SubViewport"/> looking at the same world through its own <see cref="CoopCamera"/>;
/// the main window only draws the views and the HUD on top. The views are numbered in player order, so a hero's
/// hearts (in the screen corners, see <see cref="UI.HeartBar"/>) always sit in their own view.
/// </summary>
public partial class SplitScreen : CanvasLayer
{
    private const string NodeName = "SplitScreen";

    /// <summary>A split view is zoomed so it shows at least this much of the world (the 1-player close-up).</summary>
    private static readonly Vector2 SmallestWorldView = new(320, 180);

    private static readonly Color Divider = new(0.08f, 0.09f, 0.18f);

    private readonly List<View> _views = new();
    private readonly List<ColorRect> _dividers = new();
    private readonly Dictionary<Hero, float> _fades = new();
    private string _layout = "";
    private float _sharedMinZoom;
    private float _sharedMaxZoom;

    /// <summary>One part of the screen: where it is on the screen and the camera looking through it.</summary>
    public sealed class View
    {
        public required SubViewportContainer Box { get; init; }
        public required SubViewport Port { get; init; }
        public required CoopCamera Camera { get; init; }

        /// <summary>Darkens the view while its hero goes through a door.</summary>
        public required ColorRect Fade { get; init; }

        public Rect2 ScreenRect => new(Box.Position, Box.Size);

        public bool IsShown => Box.Visible;

        /// <summary>Where <paramref name="world"/> appears on the screen (the main window's coordinates).</summary>
        public Vector2 ToScreen(Vector2 world) => Box.Position + Port.GetCanvasTransform() * world;

        /// <summary>Is this point of the world inside this view (with a little leeway past the edges), in the room it's
        /// looking at? (A small room leaves space round it, where another room may be.)</summary>
        public bool Shows(Vector2 world, float leeway = 8f) =>
            IsShown && ScreenRect.Grow(leeway).HasPoint(ToScreen(world))
            && (Camera.Room is not { } room || new Rect2(room.GlobalPosition, room.SizeInPixels).Grow(leeway).HasPoint(world));
    }

    /// <summary>Everyone shares one full-screen view (one hero, or the boss fight).</summary>
    public bool Shared { get; private set; } = true;

    /// <summary>The views on screen right now, in player order.</summary>
    public IEnumerable<View> Views => _views.Where(v => v.IsShown);

    /// <summary>How many views are on screen.</summary>
    public int ViewCount => Views.Count();

    public static SplitScreen? Find(Node anyNode) =>
        anyNode.GetTree().CurrentScene?.GetNodeOrNull<SplitScreen>(NodeName);

    /// <summary>The view <paramref name="hero"/> plays in (their own, or the shared one).</summary>
    public View? ViewOf(Hero hero) =>
        Views.FirstOrDefault(v => v.Camera.Follow == hero) ?? Views.FirstOrDefault(v => v.Camera.Follow is null);

    /// <summary>Set up split screen for a playable scene, taking over its "Camera" (a <see cref="CoopCamera"/>).</summary>
    public static void Attach(Node2D scene)
    {
        if (scene.GetNodeOrNull<CoopCamera>("Camera") is not { } camera)
        {
            return;
        }

        var split = new SplitScreen { Name = NodeName, Layer = -1 };
        scene.AddChild(split);
        split.Build(camera);
    }

    private void Build(CoopCamera firstCamera)
    {
        var root = GetViewport();
        _sharedMinZoom = firstCamera.MinZoom;
        _sharedMaxZoom = firstCamera.MaxZoom;

        for (var i = 0; i < PlayerManager.MaxPlayers; i++)
        {
            var box = new SubViewportContainer { Name = $"View{i + 1}", MouseFilter = Control.MouseFilterEnum.Ignore };
            var port = new SubViewport
            {
                World2D = root.World2D,
                CanvasItemDefaultTextureFilter = root.CanvasItemDefaultTextureFilter,
                Snap2DTransformsToPixel = root.Snap2DTransformsToPixel,
                Snap2DVerticesToPixel = root.Snap2DVerticesToPixel,
                HandleInputLocally = false,
            };
            box.AddChild(port);
            AddChild(box);

            CoopCamera camera;
            if (i == 0)
            {
                camera = firstCamera;
                camera.Reparent(port, false);
            }
            else
            {
                camera = new CoopCamera
                {
                    Name = "Camera",
                    Room = firstCamera.Room,
                    Margin = firstCamera.Margin,
                    FollowSpeed = firstCamera.FollowSpeed,
                };
                port.AddChild(camera);
            }

            var fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
            _views.Add(new View { Box = box, Port = port, Camera = camera, Fade = fade });
        }

        // The fades go over the views and the lines between them.
        foreach (var view in _views)
        {
            AddChild(view.Fade);
        }

        // The main window draws the world through the views only: point its own (camera-less) view far away.
        root.CanvasTransform = new Transform2D(0, new Vector2(-1e7f, -1e7f));

        Arrange();
    }

    public override void _PhysicsProcess(double delta) => Arrange();

    /// <summary>Darken <paramref name="hero"/>'s view (0 = clear, 1 = black), e.g. going through a door.
    /// The shared view darkens with whoever is fading most.</summary>
    public void SetFade(Hero hero, float alpha)
    {
        _fades[hero] = alpha;
        foreach (var gone in _fades.Keys.Where(h => !IsInstanceValid(h)).ToList())
        {
            _fades.Remove(gone);
        }

        UpdateFades();
    }

    private void UpdateFades()
    {
        foreach (var view in _views)
        {
            var alpha = !view.IsShown ? 0f
                : view.Camera.Follow is { } hero ? _fades.GetValueOrDefault(hero)
                : _fades.Values.DefaultIfEmpty(0f).Max();
            view.Fade.Color = view.Fade.Color with { A = alpha };
        }
    }

    /// <summary>Work out who gets which part of the screen; only changes anything when that changes.</summary>
    private void Arrange()
    {
        var heroes = Heroes();
        var shared = heroes.Count <= 1 || AllInBossRoom(heroes) || OneViewForPictures;
        var layout = shared ? "shared" : string.Join(",", heroes.Select(h => h.GetInstanceId()));
        if (layout == _layout)
        {
            return;
        }

        _layout = layout;
        Shared = shared;

        var screen = GetViewport().GetVisibleRect().Size;
        var half = screen / 2;
        var rects = shared ? new[] { new Rect2(Vector2.Zero, screen) } : heroes.Count switch
        {
            2 => new[] { new Rect2(0, 0, half.X, screen.Y), new Rect2(half.X, 0, half.X, screen.Y) },
            3 => new[] { new Rect2(Vector2.Zero, half), new Rect2(half.X, 0, half.X, screen.Y), new Rect2(0, half.Y, half.X, half.Y) },
            _ => new[] { new Rect2(Vector2.Zero, half), new Rect2(half.X, 0, half.X, half.Y), new Rect2(0, half.Y, half.X, half.Y), new Rect2(half, half) },
        };

        for (var i = 0; i < _views.Count; i++)
        {
            var view = _views[i];
            var shown = i < rects.Length;
            view.Box.Visible = shown;
            view.Fade.Visible = shown;
            view.Port.RenderTargetUpdateMode = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            view.Camera.ProcessMode = shown ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            if (!shown)
            {
                continue;
            }

            view.Port.Size = (Vector2I)rects[i].Size; // first: the box can't be smaller than its view
            view.Box.Position = rects[i].Position;
            view.Box.Size = rects[i].Size;
            view.Fade.Position = rects[i].Position;
            view.Fade.Size = rects[i].Size;
            view.Camera.Follow = shared ? null : heroes[i];
            if (shared)
            {
                view.Camera.MinZoom = _sharedMinZoom;
                view.Camera.MaxZoom = _sharedMaxZoom;
            }
            else
            {
                var zoom = Mathf.Max(1f, Mathf.Floor(Mathf.Min(rects[i].Size.X / SmallestWorldView.X, rects[i].Size.Y / SmallestWorldView.Y)));
                view.Camera.MinZoom = view.Camera.MaxZoom = zoom;
            }

            view.Camera.Snap();
        }

        PlaceDividers();
        UpdateFades();
    }

    /// <summary>The heroes playing, in player order (P1 first).</summary>
    private List<Hero> Heroes()
    {
        if (PlayerManager.Find(this) is { } players)
        {
            return players.Players.OfType<Hero>().Where(h => h.IsInGame).ToList();
        }

        return GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).OrderBy(h => h.PlayerIndex).ToList();
    }

    /// <summary>Dev helper: <c>-- --showcase-shared</c> keeps one shared view however many heroes there are (for pictures).</summary>
    private static readonly bool OneViewForPictures = OS.GetCmdlineUserArgs().Contains("--showcase-shared");

    /// <summary>Boss fights are on one shared screen, once everyone's in there (until the boss is beaten).</summary>
    private bool AllInBossRoom(List<Hero> heroes) =>
        Dungeon.Find(this) is { } dungeon && dungeon.RoomOf(heroes[0]) is { IsBossRoom: true, IsCleared: false } room
        && heroes.All(h => dungeon.RoomOf(h) == room);

    /// <summary>A thin dark line between the views (on the right and bottom edges that aren't the screen's).</summary>
    private void PlaceDividers()
    {
        foreach (var line in _dividers)
        {
            line.QueueFree();
        }

        _dividers.Clear();
        var screen = GetViewport().GetVisibleRect().Size;
        foreach (var rect in Views.Select(v => v.ScreenRect))
        {
            if (rect.End.X < screen.X)
            {
                AddDivider(new Rect2(rect.End.X - 1, rect.Position.Y, 2, rect.Size.Y));
            }

            if (rect.End.Y < screen.Y)
            {
                AddDivider(new Rect2(rect.Position.X, rect.End.Y - 1, rect.Size.X, 2));
            }
        }
    }

    private void AddDivider(Rect2 rect)
    {
        var line = new ColorRect { Color = Divider, Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(line);
        _dividers.Add(line);
    }
}
