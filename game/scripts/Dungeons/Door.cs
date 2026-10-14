using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Rooms;

namespace LizardWizard.Dungeons;

public enum DoorSide
{
    Top,
    Bottom,
    Left,
    Right,
}

public enum DoorState
{
    Open,

    /// <summary>Needs a key. Walk into it with a key to unlock it (the key is used up).</summary>
    Locked,

    /// <summary>Opens by itself when the room is cleared and / or its switches are on.</summary>
    Shut,
}

/// <summary>Something that can open a door: a lever, a pressure plate...</summary>
public interface ISwitch
{
    bool IsOn { get; }
}

/// <summary>
/// A doorway in one of the room's walls, leading to a door in another room. Cuts a gap in the wall
/// tiles, draws the door frame from the room's tileset, and blocks the way while it's locked or shut.
/// Walking through an open door takes the whole team to the next room.
/// </summary>
public partial class Door : Node2D, Saving.ISaveable
{
    // Pieces of the Tiny Dungeon room sheet, in pixels.
    private static readonly Rect2 ClosedFrame = new(106, 23, 28, 24);
    private static readonly Rect2 OpenFrame = new(154, 103, 28, 24);
    private static readonly Rect2 Doorway = new(192, 24, 16, 23);
    private static readonly Rect2 LeftPost = new(88, 58, 9, 28);
    private static readonly Rect2 RightPost = new(143, 58, 9, 28);
    private const string KeyIcon = "res://assets/third_party/tiny_dungeon/Misc/Golden_key.png";
    private const string StairsTexture = "res://assets/third_party/tiny_dungeon/Misc/Stairs_down_grass.png";

    private DungeonRoom _room = null!;
    private StaticBody2D _blocker = null!;
    private Sprite2D? _frame;
    private Sprite2D? _keyIcon;
    private Texture2D _sheet = null!;
    private float _messageCooldown;
    private DoorState _startState;

    [Export] public DoorSide Side { get; set; } = DoorSide.Top;

    [Export] public DoorState State { get; set; } = DoorState.Open;

    /// <summary>A shut door opens once every enemy in the room is beaten.</summary>
    [Export] public bool OpensWhenCleared { get; set; }

    /// <summary>A shut door opens once all of these switches are on.</summary>
    [Export] public Godot.Collections.Array<NodePath> Switches { get; set; } = new();

    /// <summary>
    /// Not in a wall: grassy stairs going down, standing where it's placed (the way into a dungeon from the
    /// overworld). Walk onto the stairs to go through. Always open.
    /// </summary>
    [Export] public bool Stairs { get; set; }

    /// <summary>The door on the other side.</summary>
    [Export] public NodePath? LeadsTo { get; set; }

    public DungeonRoom Room => _room;

    public bool IsOpen => State == DoorState.Open;

    public Door? Partner => LeadsTo is null || LeadsTo.IsEmpty ? null : GetNodeOrNull<Door>(LeadsTo);

    /// <summary>Where heroes appear when they come through this door from the other side (global).</summary>
    public Vector2 EntryPoint => GlobalPosition + (Stairs ? new Vector2(0, 16) : Side switch
    {
        DoorSide.Top => new Vector2(0, 46),
        DoorSide.Bottom => new Vector2(0, -12),
        DoorSide.Left => new Vector2(22, 4),
        _ => new Vector2(-22, 4),
    });

    /// <summary>Which way you walk to go through this door.</summary>
    public Vector2 Outward => Stairs ? Vector2.Up : Side switch
    {
        DoorSide.Top => Vector2.Up,
        DoorSide.Bottom => Vector2.Down,
        DoorSide.Left => Vector2.Left,
        _ => Vector2.Right,
    };

    /// <summary>Called by the room once its layout is built: place the door in the middle of its wall and cut the gap.</summary>
    public void Setup(DungeonRoom room)
    {
        _room = room;
        _startState = State;
        var layout = room.Layout;
        ZIndex = -1; // part of the wall (or the ground): behind everyone

        if (Stairs)
        {
            State = DoorState.Open;
            AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(StairsTexture), Position = new Vector2(0, -8) });
            Block(Vector2.Zero, Vector2.One);
            Refresh();
            return;
        }

        _sheet = layout.Sheet;

        var size = RoomBuilder.TileSize;
        var midColumn = layout.Columns / 2;
        var midRow = RoomBuilder.TopWallTiles + layout.InteriorHeight / 2;
        var bottomWall = RoomBuilder.TopWallTiles + layout.InteriorHeight;

        switch (Side)
        {
            case DoorSide.Top:
                Position = layout.Position + new Vector2(midColumn * size, 0);
                for (var y = 0; y < RoomBuilder.TopWallTiles; y++)
                {
                    layout.Carve(new Vector2I(midColumn - 1, y));
                    layout.Carve(new Vector2I(midColumn, y));
                }

                AddPiece(Doorway, new Vector2(-8, 8), flip: false);
                _frame = AddPiece(ClosedFrame, new Vector2(-14, 7), flip: false);
                Block(new Vector2(0, 20), new Vector2(16, 24));
                break;

            case DoorSide.Bottom:
                Position = layout.Position + new Vector2(midColumn * size, bottomWall * size);
                for (var y = bottomWall; y < layout.Rows; y++)
                {
                    layout.Carve(new Vector2I(midColumn - 1, y));
                    layout.Carve(new Vector2I(midColumn, y));
                }

                AddPiece(Doorway, new Vector2(-8, 1), flip: true);
                _frame = AddPiece(ClosedFrame, new Vector2(-14, 0), flip: true);
                Block(new Vector2(0, 12), new Vector2(16, 24));
                break;

            case DoorSide.Left:
                Position = layout.Position + new Vector2(0, midRow * size);
                layout.Carve(new Vector2I(0, midRow - 1));
                layout.Carve(new Vector2I(0, midRow));
                AddPiece(LeftPost, new Vector2(0, -14), flip: false);
                Block(new Vector2(4, 0), new Vector2(8, 16));
                break;

            default:
                Position = layout.Position + new Vector2(layout.Columns * size, midRow * size);
                layout.Carve(new Vector2I(layout.Columns - 1, midRow - 1));
                layout.Carve(new Vector2I(layout.Columns - 1, midRow));
                AddPiece(RightPost, new Vector2(-9, -14), flip: false);
                Block(new Vector2(-4, 0), new Vector2(8, 16));
                break;
        }

        _keyIcon = new Sprite2D { Texture = GD.Load<Texture2D>(KeyIcon), Position = KeyIconPosition(), ZIndex = 1 };
        AddChild(_keyIcon);
        Refresh();
    }

    public override void _PhysicsProcess(double delta)
    {
        _messageCooldown -= (float)delta;

        if (State == DoorState.Shut && ShouldOpen())
        {
            Open();
        }

        if (State == DoorState.Locked)
        {
            TryUnlock();
        }
    }

    public string? SaveState() => State == _startState ? null : State.ToString();

    public void LoadState(string state)
    {
        State = System.Enum.Parse<DoorState>(state);
        Refresh();
    }

    public void Open()
    {
        if (State == DoorState.Open)
        {
            return;
        }

        State = DoorState.Open;
        Refresh();
        FloatingText.Spawn(this, GlobalPosition + EntryOffset(10), "The door opens!", new Color(1f, 0.9f, 0.5f), 1.5f, 8f);
    }

    /// <summary>Is <paramref name="hero"/> walking out through this (open) door?</summary>
    public bool IsLeaving(Hero hero)
    {
        if (!IsOpen || hero.IsDown)
        {
            return false;
        }

        var offset = hero.GlobalPosition - GlobalPosition;
        if (Stairs)
        {
            return Mathf.Abs(offset.X) < 7 && offset.Y is > -14 and < 2;
        }

        return Side switch
        {
            DoorSide.Top => Mathf.Abs(offset.X) < 9 && offset.Y < 24,
            DoorSide.Bottom => Mathf.Abs(offset.X) < 9 && offset.Y > 8,
            DoorSide.Left => Mathf.Abs(offset.Y - 4) < 9 && offset.X < 5,
            _ => Mathf.Abs(offset.Y - 4) < 9 && offset.X > -5,
        };
    }

    public override void _Draw()
    {
        // Side doors have no closed-door art in the tileset: draw a plain wooden door.
        if (IsOpen || Side is DoorSide.Top or DoorSide.Bottom)
        {
            return;
        }

        var rect = new Rect2(Side == DoorSide.Left ? 0 : -8, -8, 8, 16);
        DrawRect(rect, new Color(0.55f, 0.3f, 0.28f));
        DrawRect(rect, new Color(0.08f, 0.09f, 0.18f), false, 1f);
        DrawLine(rect.Position + new Vector2(0, 5), rect.Position + new Vector2(8, 5), new Color(0.35f, 0.18f, 0.2f));
        DrawLine(rect.Position + new Vector2(0, 11), rect.Position + new Vector2(8, 11), new Color(0.35f, 0.18f, 0.2f));
    }

    private bool ShouldOpen()
    {
        var switchesOn = Switches.All(p => GetNodeOrNull(p) is not ISwitch s || s.IsOn);
        var cleared = !OpensWhenCleared || _room.IsCleared;
        return switchesOn && cleared;
    }

    private void TryUnlock()
    {
        var dungeon = Dungeon.Find(this);
        var hero = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .FirstOrDefault(h => !h.IsDown && h.GlobalPosition.DistanceTo(_blocker.GlobalPosition) < 20);
        if (hero is null || dungeon is null)
        {
            return;
        }

        if (dungeon.Keys > 0)
        {
            dungeon.Keys--;
            Open();
            if (Partner is { State: DoorState.Locked } partner)
            {
                partner.State = DoorState.Open;
                partner.Refresh();
            }
        }
        else if (_messageCooldown <= 0)
        {
            _messageCooldown = 2.5f;
            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), "Locked! Find a key", new Color(1f, 0.85f, 0.4f), 1.4f, 6f);
        }
    }

    private void Refresh()
    {
        if (_frame is not null)
        {
            ((AtlasTexture)_frame.Texture).Region = IsOpen ? OpenFrame : ClosedFrame;
        }

        _blocker.GetChild<CollisionShape2D>(0).SetDeferred(CollisionShape2D.PropertyName.Disabled, IsOpen);
        if (_keyIcon is not null)
        {
            _keyIcon.Visible = State == DoorState.Locked;
        }

        QueueRedraw();
    }

    private Sprite2D AddPiece(Rect2 region, Vector2 at, bool flip)
    {
        var sprite = new Sprite2D
        {
            Texture = new AtlasTexture { Atlas = _sheet, Region = region },
            Centered = false,
            Position = at,
            FlipV = flip,
        };
        AddChild(sprite);
        return sprite;
    }

    private void Block(Vector2 center, Vector2 size)
    {
        _blocker = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0, Position = center };
        _blocker.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        AddChild(_blocker);
    }

    private Vector2 KeyIconPosition() => Side switch
    {
        DoorSide.Top => new Vector2(0, 24),
        DoorSide.Bottom => new Vector2(0, 8),
        DoorSide.Left => new Vector2(14, 0),
        _ => new Vector2(-14, 0),
    };

    private Vector2 EntryOffset(float extra) => (EntryPoint - GlobalPosition) + new Vector2(0, -extra);
}
