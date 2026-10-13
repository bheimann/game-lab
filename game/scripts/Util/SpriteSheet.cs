using Godot;

namespace LizardWizard.Util;

/// <summary>Builds animations from horizontal sprite strips (all frames in one row, equal width).</summary>
public static class SpriteSheet
{
    public static SpriteFrames Frames(string animation, string texturePath, int frameWidth, double fps, bool loop = true)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddStrip(frames, animation, texturePath, frameWidth, fps, loop);
        return frames;
    }

    public static void AddStrip(SpriteFrames frames, string animation, string texturePath, int frameWidth, double fps, bool loop = true)
    {
        var texture = GD.Load<Texture2D>(texturePath);
        var height = texture.GetHeight();
        var count = texture.GetWidth() / frameWidth;

        frames.AddAnimation(animation);
        frames.SetAnimationSpeed(animation, fps);
        frames.SetAnimationLoopMode(animation, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
        for (var i = 0; i < count; i++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(i * frameWidth, 0, frameWidth, height),
            };
            frames.AddFrame(animation, atlas);
        }
    }
}
