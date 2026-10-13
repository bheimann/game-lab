using Godot;
using LizardWizard.Rooms;
using LizardWizard.Util;

namespace LizardWizard;

/// <summary>
/// M0 art check: builds a room and drops in the art we plan to use.
/// Nothing here is playable yet; M1 replaces it with real player and enemy scenes.
/// </summary>
public partial class Main : Node2D
{
    private const string TinyDungeon = "res://assets/third_party/tiny_dungeon";
    private const string Deer = "res://assets/third_party/deer";

    private AnimatedSprite2D _deer = null!;
    private float _deerDirection = 1f;
    private float _deerMinX;
    private float _deerMaxX;

    public override void _Ready()
    {
        var room = GetNode<RoomBuilder>("Room");
        var size = room.SizeInPixels;
        GetNode<Camera2D>("Camera").Position = size / 2;

        // Stand-in chameleon: the Tiny Dungeon hero tinted green until we have a real lizard sprite.
        var frames = SpriteSheet.Frames("idle", $"{TinyDungeon}/Character/Char_one/Idle/Char_idle_down.png", 16, 8);
        SpriteSheet.AddStrip(frames, "walk", $"{TinyDungeon}/Character/Char_one/Walk/Char_walk_down.png", 16, 10);
        AddAnimated("Chameleon", frames, "idle", new Vector2(size.X / 2, size.Y / 2 + 8))
            .Modulate = new Color(0.55f, 1f, 0.45f);

        _deerMinX = 40;
        _deerMaxX = size.X - 40;
        _deer = AddAnimated(
            "Deer",
            SpriteSheet.Frames("walk", $"{Deer}/deer-walk-32x32.png", 40, 10),
            "walk",
            new Vector2(_deerMinX, size.Y / 2 + 20));

        foreach (var x in new[] { 40f, size.X - 40 })
        {
            AddAnimated("Torch", SpriteSheet.Frames("burn", $"{TinyDungeon}/Misc/Torch.png", 8, 8), "burn", new Vector2(x, 22));
        }

        AddAnimated("Chest", SpriteSheet.Frames("open", $"{TinyDungeon}/Misc/Chest.png", 16, 6, loop: false), "open", new Vector2(size.X / 2, 52))
            .Stop();

        Screenshot.AttachIfRequested(this);
        GD.Print("Lizard Wizard started.");
    }

    public override void _Process(double delta)
    {
        // Walk the deer back and forth so we can see the animation and the flip.
        var x = _deer.Position.X + _deerDirection * 30f * (float)delta;
        if (x > _deerMaxX || x < _deerMinX)
        {
            _deerDirection = -_deerDirection;
        }

        _deer.Position = new Vector2(Mathf.Clamp(x, _deerMinX, _deerMaxX), _deer.Position.Y);
        _deer.FlipH = _deerDirection < 0; // the art faces right
    }

    private AnimatedSprite2D AddAnimated(string name, SpriteFrames frames, string animation, Vector2 position)
    {
        var sprite = new AnimatedSprite2D { Name = name, SpriteFrames = frames, Position = position };
        AddChild(sprite);
        sprite.Play(animation);
        return sprite;
    }
}
