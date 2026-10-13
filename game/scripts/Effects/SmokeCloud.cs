using System.Linq;
using Godot;

namespace LizardWizard.Effects;

/// <summary>A smoke bomb's cloud: heroes inside it can't be seen by enemies. Fades after a few seconds.</summary>
public partial class SmokeCloud : Node2D
{
    public const string Group = "smoke";

    private float _age;

    public float Radius { get; init; } = 36f;

    public float Seconds { get; init; } = 5f;

    /// <summary>Is this point inside any smoke cloud?</summary>
    public static bool Hides(SceneTree tree, Vector2 point) =>
        tree.GetNodesInGroup(Group).OfType<SmokeCloud>().Any(c => c._age < c.Seconds && c.GlobalPosition.DistanceTo(point) <= c.Radius);

    public override void _Ready()
    {
        AddToGroup(Group);
        ZIndex = 5; // over the actors, so the smoke covers them
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        if (_age >= Seconds + 0.5f)
        {
            QueueFree();
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        // Puffs that swell in, drift and fade out at the end.
        var grow = Mathf.Min(1f, _age / 0.3f);
        var fade = _age < Seconds ? 1f : 1f - (_age - Seconds) / 0.5f;
        for (var i = 0; i < 9; i++)
        {
            var angle = i * Mathf.Tau / 9 + _age * 0.3f;
            var distance = Radius * (i % 2 == 0 ? 0.55f : 0.3f) * grow;
            var puff = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * distance; // flattened: seen from above at an angle
            var size = Radius * 0.45f * grow * (1f + 0.08f * Mathf.Sin(_age * 3f + i));
            DrawCircle(puff + new Vector2(0, -8), size, new Color(0.78f, 0.8f, 0.86f, 0.5f * fade));
        }
    }
}
