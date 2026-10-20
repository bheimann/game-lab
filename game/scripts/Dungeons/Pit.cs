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

    private const string PitTexture = "res://assets/kenney/pit.png";
    private const string WaterTexture = "res://assets/kenney/water.png";
    private static readonly Rect2 Frame = new(0, 0, 48, 48);
    private const int Border = 6;
    private const int WaterTop = 12; // the pond's grassy bank is deeper along the top

    private StaticBody2D _body = null!;
    private NinePatchRect _patch = null!;
    private Node2D _planks = null!;
    private float _bridge = -1f; // 0..1 while the bridge slides out

    /// <summary>Size in pixels; the node's position is the top-left corner.</summary>
    [Export] public Vector2 Size { get; set; } = new(32, 32);

    /// <summary>Draw it as water (a pond) instead of a pit. Works the same: no walking in, tongues reach over.</summary>
    [Export] public bool Water { get; set; }

    /// <summary>Draw it as a frozen lake (thin ice: no walking on it either).</summary>
    [Export] public bool Ice { get; set; }

    /// <summary>Colour the water (or pit) with this: green for a swamp, pale for a frozen lake.</summary>
    [Export] public Color Tint { get; set; } = Colors.White;

    /// <summary>Draw the stone edge on the left / right (turn off where another pit continues).</summary>
    [Export] public bool EdgeLeft { get; set; } = true;

    [Export] public bool EdgeRight { get; set; } = true;

    /// <summary>When all of these are on, a bridge appears across this pit.</summary>
    [Export] public Godot.Collections.Array<NodePath> Switches { get; set; } = new();

    /// <summary>The bridge is a fallen tree (see <see cref="FallingTree"/>) instead of planks.</summary>
    [Export] public bool Log { get; set; }

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
            PatchMarginTop = Water ? WaterTop : Border,
            PatchMarginBottom = Border,
            Size = Size,
            AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
            AxisStretchVertical = NinePatchRect.AxisStretchMode.Tile,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Tint,
            Visible = !Ice,
        };
        AddChild(_patch);
        if (Ice)
        {
            var ice = new Node2D();
            ice.Draw += () => DrawIce(ice);
            AddChild(ice);
        }

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
            if (!Log)
            {
                FloatingText.Spawn(this, GlobalPosition + new Vector2(Size.X / 2, -6), "A bridge!", new Color(1f, 0.9f, 0.5f), 1.6f, 8f);
            }
        }

        if (_bridge is >= 0 and < 1)
        {
            _bridge = Mathf.Min(1f, _bridge + (float)delta * 2f);
            _planks.QueueRedraw();
        }
    }

    private void DrawIce(Node2D canvas)
    {
        // Pale blue ice with a snowy rim, a few cracks and a shine.
        var rect = new Rect2(Vector2.Zero, Size);
        canvas.DrawRect(rect, new Color(0.62f, 0.8f, 0.94f));
        canvas.DrawRect(rect.Grow(-3), new Color(0.74f, 0.88f, 0.98f));
        canvas.DrawRect(rect, new Color(0.9f, 0.95f, 1f), false, 2f);
        var rng = new RandomNumberGenerator { Seed = 99 };
        var crack = new Color(0.55f, 0.72f, 0.88f);
        for (var i = 0; i < Size.X * Size.Y / 1500; i++)
        {
            var at = new Vector2(rng.RandfRange(8, Size.X - 8), rng.RandfRange(8, Size.Y - 8));
            var mid = at + new Vector2(rng.RandfRange(-6, 6), rng.RandfRange(-4, 4));
            canvas.DrawPolyline(new[] { at, mid, mid + new Vector2(rng.RandfRange(-6, 6), rng.RandfRange(-4, 4)) }, crack, 1f);
        }

        canvas.DrawLine(new Vector2(8, 8), new Vector2(Size.X * 0.3f, 8), new Color(1f, 1f, 1f, 0.8f), 1f);
        canvas.DrawLine(new Vector2(8, 11), new Vector2(Size.X * 0.18f, 11), new Color(1f, 1f, 1f, 0.6f), 1f);
    }

    /// <summary>A grey, barky trunk lying across, falling over from the near side; its broken top on the far side.</summary>
    private void DrawLog(float top, float length)
    {
        var bark = new Color(0.45f, 0.4f, 0.38f);
        var dark = new Color(0.08f, 0.09f, 0.18f);
        var light = new Color(0.62f, 0.57f, 0.52f);
        var width = Mathf.Min(16f, Size.X - 4);
        var left = (Size.X - width) / 2;
        _planks.DrawRect(new Rect2(left - 1, top - 1, width + 2, length + 2), dark);
        _planks.DrawRect(new Rect2(left, top, width, length), bark);
        _planks.DrawRect(new Rect2(left + 2, top, 2, length), light);
        for (var y = Size.Y - 7; y > top + 2; y -= 9)
        {
            _planks.DrawLine(new Vector2(left + 5, y), new Vector2(left + width - 3, y - 2), dark);
        }

        if (_bridge >= 1)
        {
            // The snapped-off top, with a bare branch.
            _planks.DrawLine(new Vector2(left + width - 2, 5), new Vector2(left + width + 3, 1), bark, 2f);
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
        if (Log)
        {
            DrawLog(top, length);
            return;
        }

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
