using System.Linq;
using Godot;

namespace LizardWizard.Util;

/// <summary>Builds animations from horizontal sprite strips (all frames in one row, equal width).</summary>
public static class SpriteSheet
{
    public static SpriteFrames Frames(string animation, string texturePath, int frameWidth, double fps, bool loop = true) =>
        Frames(animation, GD.Load<Texture2D>(texturePath), frameWidth, fps, loop);

    public static SpriteFrames Frames(string animation, Texture2D texture, int frameWidth, double fps, bool loop = true)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddStrip(frames, animation, texture, frameWidth, fps, loop);
        return frames;
    }

    public static void AddStrip(SpriteFrames frames, string animation, Texture2D texture, int frameWidth, double fps, bool loop = true)
    {
        var count = texture.GetWidth() / frameWidth;
        AddFrames(frames, animation, texture, frameWidth, fps, Enumerable.Range(0, count).ToArray(), loop);
    }

    /// <summary>Adds an animation that plays the strip's frames in the given order, e.g. { 1, 0, 2, 0 } for a walk cycle.</summary>
    public static void AddFrames(SpriteFrames frames, string animation, Texture2D texture, int frameWidth, double fps, int[] order, bool loop = true)
    {
        var height = texture.GetHeight();

        frames.AddAnimation(animation);
        frames.SetAnimationSpeed(animation, fps);
        frames.SetAnimationLoopMode(animation, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
        foreach (var i in order)
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
