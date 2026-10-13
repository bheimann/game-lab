using Godot;

namespace LizardWizard.Players;

/// <summary>
/// Reads one player's controls, polled once per physics frame with <see cref="Update"/>.
/// M1: the keyboard and every gamepad all drive player 1. M2 gives each device its own player.
/// </summary>
/// <remarks>
/// Keyboard: WASD / arrows to move, J or Space to punch, K to use the tongue.
/// Gamepad: left stick / d-pad to move, A (bottom) to punch, X (left) to use the tongue.
/// </remarks>
public sealed class PlayerInput
{
    private const float StickDeadzone = 0.25f;

    private bool _punchWasHeld;
    private bool _tongueWasHeld;

    /// <summary>Movement direction, length 0..1 (sticks can be partly pushed).</summary>
    public Vector2 Move { get; private set; }

    public bool PunchPressed { get; private set; }

    public bool TonguePressed { get; private set; }

    public void Update()
    {
        var move = KeyboardMove();
        var punchHeld = Input.IsPhysicalKeyPressed(Key.J) || Input.IsPhysicalKeyPressed(Key.Space);
        var tongueHeld = Input.IsPhysicalKeyPressed(Key.K);

        foreach (var device in Input.GetConnectedJoypads())
        {
            move += GamepadMove(device);
            punchHeld |= Input.IsJoyButtonPressed(device, JoyButton.A);
            tongueHeld |= Input.IsJoyButtonPressed(device, JoyButton.X);
        }

        Move = move.LimitLength(1f);
        PunchPressed = punchHeld && !_punchWasHeld;
        TonguePressed = tongueHeld && !_tongueWasHeld;
        _punchWasHeld = punchHeld;
        _tongueWasHeld = tongueHeld;
    }

    private static Vector2 KeyboardMove()
    {
        var x = Axis(Key.A, Key.Left, Key.D, Key.Right);
        var y = Axis(Key.W, Key.Up, Key.S, Key.Down);
        return new Vector2(x, y).Normalized();

        static float Axis(Key neg1, Key neg2, Key pos1, Key pos2) =>
            (Input.IsPhysicalKeyPressed(pos1) || Input.IsPhysicalKeyPressed(pos2) ? 1 : 0)
            - (Input.IsPhysicalKeyPressed(neg1) || Input.IsPhysicalKeyPressed(neg2) ? 1 : 0);
    }

    private static Vector2 GamepadMove(int device)
    {
        var stick = new Vector2(Input.GetJoyAxis(device, JoyAxis.LeftX), Input.GetJoyAxis(device, JoyAxis.LeftY));
        if (stick.Length() >= StickDeadzone)
        {
            return stick;
        }

        var dpad = new Vector2(
            (Input.IsJoyButtonPressed(device, JoyButton.DpadRight) ? 1 : 0) - (Input.IsJoyButtonPressed(device, JoyButton.DpadLeft) ? 1 : 0),
            (Input.IsJoyButtonPressed(device, JoyButton.DpadDown) ? 1 : 0) - (Input.IsJoyButtonPressed(device, JoyButton.DpadUp) ? 1 : 0));
        return dpad.Normalized();
    }
}
