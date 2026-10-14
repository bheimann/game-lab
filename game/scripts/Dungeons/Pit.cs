using System.Linq;
using Godot;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>
/// A pit nobody can walk across (it's on physics layer 5, "pits"). Tongues reach over it, but walls stop them.
/// With <see cref="Switches"/> set, a bridge slides across once all of them are on.
/// Line several pits up side by side for a long gap: turn off the edge on the sides that touch.
/// </summary>
public partial class Pit : Node2D
{
    public const uint PitLayer = 16;

    private const string PitTexture = "res://assets/third_party/tiny_dungeon/Tilesets/Pit.png";
    private const string WaterTexture = "res://assets/third_party/tiny_dungeon/Tilesets/Water.png";
    private static readonly Rect2 Frame = new(16, 0, 48, 48);
    private const int Border = 6;

    private StaticBody2D _body = null!;
    private NinePatchRect _patch = null!;
    private Node2D _planks = null!;
    private float _bridge = -1f; // 0..1 while the bridge slides out

    /// <summary>Size in pixels; the node's position is the top-left corner.</summary>
    [Export] public Vector2 Size { get; set; } = new(32, 32);

    /// <summary>Draw it as water (a pond) instead of a pit. Works the same: no walking in, tongues reach over.</summary>
    [Export] public bool Water { get; set; }

    /// <summary>Draw the stone edge on the left / right (turn off where another pit continues).</summary>
    [Export] public bool EdgeLeft { get; set; } = true;

    [Export] public bool EdgeRight { get; set; } = true;

    /// <summary>When all of these are on, a bridge appears across this pit.</summary>
    [Export] public Godot.Collections.Array<NodePath> Switches { get; set; } = new();

    public bool HasBridge => _bridge >= 0;

    public override void _Ready()
    {
        ZIndex = -1; // part of the floor

        // Cut the edge off the sides that join another pit.
        var region = Frame;
        if (!EdgeLeft)
        {
            region = region.GrowIndividual(-Border, 0, 0, 0);
        }

        if (!EdgeRight)
        {
            region = region.GrowIndividual(0, 0, -Border, 0);
        }

        _patch = new NinePatchRect
        {
            Texture = GD.Load<Texture2D>(Water ? WaterTexture : PitTexture),
            RegionRect = region,
            PatchMarginLeft = EdgeLeft ? Border : 0,
            PatchMarginRight = EdgeRight ? Border : 0,
            PatchMarginTop = Border,
            PatchMarginBottom = Border,
            Size = Size,
            AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
            AxisStretchVertical = NinePatchRect.AxisStretchMode.Tile,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_patch);

        // The bridge is drawn by its own child, added after the pit picture so it shows on top of it.
        _planks = new Node2D();
        _planks.Draw += DrawBridge;
        AddChild(_planks);

        _body = new StaticBody2D { CollisionLayer = PitLayer, CollisionMask = 0 };
        _body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size - new Vector2(4, 4) }, Position = Size / 2 });
        AddChild(_body);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_bridge < 0 && Switches.Count > 0 && Switches.All(p => GetNodeOrNull(p) is ISwitch { IsOn: true }))
        {
            _bridge = 0;
            _body.GetChild<CollisionShape2D>(0).SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            FloatingText.Spawn(this, GlobalPosition + new Vector2(Size.X / 2, -6), "A bridge!", new Color(1f, 0.9f, 0.5f), 1.6f, 8f);
        }

        if (_bridge is >= 0 and < 1)
        {
            _bridge = Mathf.Min(1f, _bridge + (float)delta * 2f);
            _planks.QueueRedraw();
        }
    }

    private void DrawBridge()
    {
        if (_bridge < 0)
        {
            return;
        }

        // Wooden planks sliding across from the near (bottom) side.
        var length = Size.Y * _bridge;
        var top = Size.Y - length;
        var wood = new Color(0.62f, 0.4f, 0.27f);
        var dark = new Color(0.36f, 0.2f, 0.17f);
        _planks.DrawRect(new Rect2(2, top, Size.X - 4, length), wood);
        for (var y = Size.Y - 4; y > top; y -= 4)
        {
            _planks.DrawLine(new Vector2(2, y), new Vector2(Size.X - 2, y), dark);
        }

        _planks.DrawRect(new Rect2(1, top, 2, length), dark);
        _planks.DrawRect(new Rect2(Size.X - 3, top, 2, length), dark);
    }
}
