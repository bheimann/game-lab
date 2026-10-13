using Godot;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper: run the game with <c>-- --screenshot=out.png</c> to save the screen after a
/// short delay and quit. Lets us check visuals from the command line.
/// </summary>
public partial class Screenshot : Node
{
    private const string Flag = "--screenshot=";

    private string _path = "";
    private int _framesLeft = 30;

    public static void AttachIfRequested(Node parent)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(Flag))
            {
                parent.AddChild(new Screenshot { _path = arg[Flag.Length..] });
            }
        }
    }

    public override async void _Process(double delta)
    {
        if (--_framesLeft != 0)
        {
            return;
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(_path);
        GD.Print(error == Error.Ok ? $"Saved screenshot to {_path}" : $"Screenshot failed: {error}");
        GetTree().Quit();
    }
}
