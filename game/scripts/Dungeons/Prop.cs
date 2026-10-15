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
}

/// <summary>
/// Something solid that just stands there: a bush, a rock, a crate, a sign. The node's position is where it
/// touches the ground. A sign with <see cref="Text"/> shows it when a hero walks up to it.
/// </summary>
public partial class Prop : StaticBody2D
{
    private Label? _label;

    [Export] public PropKind Kind { get; set; } = PropKind.Bush;

    /// <summary>What a sign says (shown when a hero is close).</summary>
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        var texture = GD.Load<Texture2D>($"res://assets/kenney/{Kind.ToString().ToLowerInvariant()}.png");
        AddChild(new Sprite2D { Texture = texture, Offset = new Vector2(0, -texture.GetHeight() / 2f + 1) });
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(12, 6) }, Position = new Vector2(0, -3) });

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

    public override void _PhysicsProcess(double delta)
    {
        if (_label is not null)
        {
            _label.Visible = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
                .Any(h => h.IsInGame && h.GlobalPosition.DistanceTo(GlobalPosition) < 28);
        }
    }
}
