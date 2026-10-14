using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;
using LizardWizard.Players;

namespace LizardWizard.Dungeons;

/// <summary>
/// TEST PAINTER (temporary, for trying things out): step onto the paint mat and you get painted into your
/// character's next class (e.g. lizard: Chameleon → Spitter → Tongue Master → Chameleon ...).
/// You have to stand on the mat for a moment, so walking across it in a fight doesn't repaint you.
/// Step off and on again for the next one. Using it also unlocks everything bosses would unlock.
/// </summary>
public partial class Painter : Node2D
{
    private static readonly Color[] Splotches =
    {
        new(0.9f, 0.27f, 0.22f), new(0.94f, 0.71f, 0.25f), new(0.39f, 0.67f, 0.25f), new(0f, 0.58f, 0.91f), new(0.6f, 0.35f, 0.8f),
    };

    /// <summary>How long each player (by number) has been on the mat.</summary>
    private readonly Dictionary<int, float> _onMatFor = new();

    /// <summary>Player numbers already painted this time on the mat (so standing still doesn't keep repainting).</summary>
    private readonly HashSet<int> _painted = new();

    /// <summary>How close (in pixels) a hero's feet must be to the middle of the mat.</summary>
    [Export] public float MatRadius { get; set; } = 10f;

    /// <summary>How long you stand on the mat before getting painted.</summary>
    [Export] public float StandSeconds { get; set; } = 0.6f;

    public override void _Ready()
    {
        AddChild(new Label
        {
            Text = "Painter: stand on\nthe mat to change class",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-40, -34),
            Size = new Vector2(80, 20),
            LabelSettings = new LabelSettings { FontSize = 8, OutlineSize = 3, OutlineColor = new Color(0.08f, 0.09f, 0.18f) },
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (PlayerManager.Find(this) is not { } manager)
        {
            return;
        }

        foreach (var hero in manager.Players.OfType<Hero>().Where(h => h.IsInGame).ToList())
        {
            var onMat = !hero.IsDown && hero.GlobalPosition.DistanceTo(GlobalPosition) <= MatRadius;
            if (!onMat)
            {
                _onMatFor.Remove(hero.PlayerIndex);
                _painted.Remove(hero.PlayerIndex);
                continue;
            }

            _onMatFor[hero.PlayerIndex] = _onMatFor.GetValueOrDefault(hero.PlayerIndex) + (float)delta;
            if (_onMatFor[hero.PlayerIndex] >= StandSeconds && _painted.Add(hero.PlayerIndex))
            {
                Unlocks.UnlockEverything();
                var classes = hero.ClassNames;
                if (classes.Count < 2)
                {
                    FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), $"No other {hero.DisplayName} classes yet", Colors.White, 1.5f, 8f);
                    continue;
                }

                var current = classes.ToList().IndexOf(hero.CurrentClass);
                hero.Paint(classes[(current + 1) % classes.Count]);
            }
        }
    }

    public override void _Draw()
    {
        // A rug with paint splotches.
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.55f));
        DrawCircle(Vector2.Zero, MatRadius + 4f, new Color(0.93f, 0.88f, 0.75f));
        DrawArc(Vector2.Zero, MatRadius + 4f, 0, Mathf.Tau, 24, new Color(0.08f, 0.09f, 0.18f), 1.5f);
        for (var i = 0; i < Splotches.Length; i++)
        {
            DrawCircle(Vector2.Right.Rotated(i * Mathf.Tau / Splotches.Length) * 8f, 3f, Splotches[i]);
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
