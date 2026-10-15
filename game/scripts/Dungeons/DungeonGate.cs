using Godot;
using LizardWizard.Util;

namespace LizardWizard.Dungeons;

/// <summary>
/// The way into a dungeon from the overworld: a stone temple front with battlements, two torches and a portcullis
/// archway. The whole front is solid except the archway, so heroes go in from the bottom. Put the dungeon's stairs
/// (a <see cref="Door"/> with Stairs) at the same spot: they show at the bottom of the arch. The node's position is
/// the middle of the bottom edge.
/// </summary>
public partial class DungeonGate : StaticBody2D
{
    private const string GateTexture = "res://assets/kenney/gate.png";
    private const string TorchStrip = "res://assets/kenney/torch.png";
    private const float Width = 96f;
    private const float Height = 48f;
    private const float Passage = 20f; // the opening in the arch
    private const float PassageDepth = 16f;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(GateTexture), Offset = new Vector2(0, -Height / 2) });

        foreach (var x in new[] { -24f, 24f })
        {
            var torch = new AnimatedSprite2D { SpriteFrames = SpriteSheet.Frames("burn", TorchStrip, 8, 8), Position = new Vector2(x, -26) };
            AddChild(torch);
            torch.Play("burn");
        }

        // Solid either side of the arch, and behind the bit of passage where the stairs are.
        var side = (Width - Passage) / 2;
        Solid(new Rect2(-Width / 2, -Height, side, Height));
        Solid(new Rect2(Passage / 2, -Height, side, Height));
        Solid(new Rect2(-Passage / 2, -Height, Passage, Height - PassageDepth));
    }

    private void Solid(Rect2 rect) =>
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size }, Position = rect.GetCenter() });
}
