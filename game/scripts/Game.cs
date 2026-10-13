using Godot;
using LizardWizard.Rooms;
using LizardWizard.Util;

namespace LizardWizard;

/// <summary>Root of a playable room: points the camera at the room and hooks up dev helpers.</summary>
public partial class Game : Node2D
{
    public override void _Ready()
    {
        var room = GetNode<RoomBuilder>("Room");
        GetNode<Camera2D>("Camera").Position = room.Position + room.SizeInPixels / 2;

        Screenshot.AttachIfRequested(this);
        DevAutoplay.AttachIfRequested(this);
    }
}
