using Godot;

namespace LizardWizard.Util;

/// <summary>
/// An animated decoration (torch, chest, etc.) made from a horizontal strip of frames.
/// Drop one into a scene, pick the strip texture and frame width in the inspector, done.
/// </summary>
public partial class StripSprite : AnimatedSprite2D
{
    [Export] public Texture2D? Strip { get; set; }

    [Export] public int FrameWidth { get; set; } = 16;

    [Export] public double FramesPerSecond { get; set; } = 8;

    [Export] public bool Loop { get; set; } = true;

    /// <summary>Start animating as soon as the scene starts. Turn off for things like chests that wait to be opened.</summary>
    [Export] public bool PlayOnStart { get; set; } = true;

    public override void _Ready()
    {
        if (Strip is null)
        {
            return;
        }

        SpriteFrames = SpriteSheet.Frames("default", Strip, FrameWidth, FramesPerSecond, Loop);
        if (PlayOnStart)
        {
            Play("default");
        }
    }
}
