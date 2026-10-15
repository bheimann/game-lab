using Godot;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>A cracked rock that blocks the way. Only a bomb blast breaks it.</summary>
public partial class Boulder : StaticBody2D
{
    public const string Group = "bombable";

    private const string Texture = "res://assets/kenney/rock.png";

    private float _crumble = -1f;

    public override void _Ready()
    {
        AddToGroup(Group);
        CollisionLayer = 1;
        CollisionMask = 0;
        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(Texture), Offset = new Vector2(0, -7) });
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(14, 10) }, Position = new Vector2(0, -4) });
    }

    /// <summary>Blown up by a bomb.</summary>
    public void Break()
    {
        if (_crumble >= 0)
        {
            return;
        }

        _crumble = 0;
        Saving.SaveGame.MarkRemoved(this);
        GetChild<Sprite2D>(0).Hide();
        GetChild<CollisionShape2D>(1).SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_crumble < 0)
        {
            return;
        }

        _crumble += (float)delta;
        QueueRedraw();
        if (_crumble > 0.5f)
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        if (_crumble < 0)
        {
            // Cracks, so players can tell this rock can be broken.
            var crack = new Color(0.08f, 0.09f, 0.18f, 0.9f);
            DrawPolyline(new[] { new Vector2(-3, -12), new Vector2(-1, -9), new Vector2(-3, -6), new Vector2(0, -3) }, crack, 1f);
            DrawPolyline(new[] { new Vector2(2, -11), new Vector2(4, -8) }, crack, 1f);
            return;
        }

        // Bits of rock flying apart.
        var t = _crumble / 0.5f;
        var colour = new Color(0.6f, 0.62f, 0.75f, 1f - t);
        for (var i = 0; i < 6; i++)
        {
            var direction = Vector2.Right.Rotated(i * Mathf.Tau / 6 + 0.4f);
            DrawRect(new Rect2(direction * 12f * t + new Vector2(-1.5f, -8 - 6 * t * (1 - t) * 4), new Vector2(3, 3)), colour);
        }
    }
}
