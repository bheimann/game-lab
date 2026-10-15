using System.Linq;
using Godot;
using LizardWizard.Actors;

namespace LizardWizard.Dungeons;

public enum PropKind
{
    Bush,
    Rock,
    Rock2,
    Sign,
    Box,
    Tree,
    TreeAutumn,
    SmallTree,
    Pine,
    SnowPine,
    DeadTree,
    Mushroom,
    Snowman,
    Fence,
    HouseBlue,
    HouseRed,
}

/// <summary>
/// Something solid that just stands there: a bush, a rock, a crate, a sign, a tree, a house. The node's position is
/// where it touches the ground (the middle of the bottom edge). Only the bottom is solid, so heroes can walk behind
/// tall things. A sign with <see cref="Text"/> shows it when a hero walks up to it.
/// </summary>
public partial class Prop : StaticBody2D
{
    /// <summary>Every prop is in this group (the grapple's aim assist looks for them).</summary>
    public const string Group = "props";

    private Label? _label;

    [Export] public PropKind Kind { get; set; } = PropKind.Bush;

    /// <summary>What a sign says (shown when a hero is close).</summary>
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";

    public override void _Ready()
    {
        AddToGroup(Group);
        CollisionLayer = 1;
        CollisionMask = 0;
        var texture = GD.Load<Texture2D>($"res://assets/kenney/{Kind.ToString().ToLowerInvariant()}.png");
        AddChild(new Sprite2D { Texture = texture, Offset = new Vector2(0, -texture.GetHeight() / 2f + 1) });
        var solid = SolidSize(Kind);
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = solid }, Position = new Vector2(0, -solid.Y / 2) });

        if (Text != "")
        {
            _label = new Label
            {
                Text = Text,
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(-60, -40),
                Size = new Vector2(120, 20),
                ZIndex = 10,
                LabelSettings = new LabelSettings { FontSize = 8, OutlineSize = 3, OutlineColor = new Color(0.08f, 0.09f, 0.18f) },
            };
            AddChild(_label);
        }
    }

    /// <summary>The solid part at the bottom of each kind.</summary>
    private static Vector2 SolidSize(PropKind kind) => kind switch
    {
        PropKind.Tree or PropKind.TreeAutumn or PropKind.Pine or PropKind.SnowPine or PropKind.DeadTree => new Vector2(8, 5),
        PropKind.SmallTree or PropKind.Mushroom => new Vector2(8, 4),
        PropKind.Snowman => new Vector2(10, 5),
        PropKind.Fence => new Vector2(40, 4),
        PropKind.HouseBlue or PropKind.HouseRed => new Vector2(62, 20),
        _ => new Vector2(12, 6),
    };

    public override void _PhysicsProcess(double delta)
    {
        if (_label is not null)
        {
            _label.Visible = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
                .Any(h => h.IsInGame && h.GlobalPosition.DistanceTo(GlobalPosition) < 28);
        }
    }
}
