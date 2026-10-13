using Godot;

namespace LizardWizard.Players;

public enum InputDeviceKind
{
    /// <summary>WASD to move, J / Space / F primary, K / G secondary, L / H special, I item, Q / E previous / next,
    /// Tab menu, hold Backspace to leave.</summary>
    KeyboardLeft,

    /// <summary>Arrows to move, Period / Numpad 1 primary, Slash / Numpad 2 secondary, Comma / Numpad 3 special,
    /// M / Numpad 0 item, ; and ' previous / next, Enter menu, hold Delete to leave.</summary>
    KeyboardRight,

    /// <summary>Left stick / d-pad to move, A primary, X secondary, Y special, B item, LB / RB previous / next,
    /// Start menu, hold Back to leave.</summary>
    Gamepad,
}

/// <summary>One thing a player can hold: half the keyboard or one controller.</summary>
public readonly record struct InputDevice(InputDeviceKind Kind, int Joypad = -1)
{
    public static InputDevice KeyboardLeft => new(InputDeviceKind.KeyboardLeft);

    public static InputDevice KeyboardRight => new(InputDeviceKind.KeyboardRight);

    public static InputDevice Gamepad(int joypad) => new(InputDeviceKind.Gamepad, joypad);

    public override string ToString() => Kind switch
    {
        InputDeviceKind.KeyboardLeft => "keyboard (WASD)",
        InputDeviceKind.KeyboardRight => "keyboard (arrows)",
        _ => $"controller {Joypad + 1}",
    };
}

/// <summary>
/// Reads one device's controls. <see cref="PlayerManager"/> calls <see cref="Update"/> once per physics
/// frame for every device, before the lizards move, so "pressed" means "pressed this frame".
/// </summary>
public sealed class PlayerInput
{
    private const float StickDeadzone = 0.25f;

    private bool _primaryWasHeld;
    private bool _secondaryWasHeld;
    private bool _specialWasHeld;
    private bool _startWasHeld;
    private bool _itemWasHeld;
    private bool _previousWasHeld;
    private bool _nextWasHeld;
    private int _navigateWas;

    public PlayerInput(InputDevice device)
    {
        Device = device;
    }

    public InputDevice Device { get; }

    /// <summary>Movement direction, length 0..1 (sticks can be partly pushed).</summary>
    public Vector2 Move { get; private set; }

    /// <summary>A / J: punch, bite, ...</summary>
    public bool PrimaryPressed { get; private set; }

    /// <summary>X / K: tongue, bomb, ...</summary>
    public bool SecondaryPressed { get; private set; }

    /// <summary>Y / L: colour change, ...</summary>
    public bool SpecialPressed { get; private set; }

    /// <summary>B / I / M: use the selected item (herb).</summary>
    public bool ItemPressed { get; private set; }

    /// <summary>LB / Q / ;: previous bomb, colour, ... (and turns the ring menu).</summary>
    public bool PreviousPressed { get; private set; }

    /// <summary>RB / E / ': next bomb, colour, ... (and turns the ring menu).</summary>
    public bool NextPressed { get; private set; }

    /// <summary>Start / Tab / Enter: open or close the ring menu.</summary>
    public bool MenuPressed { get; private set; }

    /// <summary>-1 or +1 on the frame the stick / keys are pushed left or right (for menus), otherwise 0.</summary>
    public int NavigateX { get; private set; }

    /// <summary>Any of the buttons that join the game: primary, secondary or Start.</summary>
    public bool JoinPressed { get; private set; }

    /// <summary>How long the leave button (Back / Backspace / Delete) has been held, in seconds.</summary>
    public double LeaveHeldSeconds { get; private set; }

    public void Update(double delta)
    {
        bool primary, secondary, special, start, leave, item, previous, next;
        Vector2 move;

        switch (Device.Kind)
        {
            case InputDeviceKind.KeyboardLeft:
                move = Keys(Key.W, Key.A, Key.S, Key.D);
                primary = Down(Key.J) || Down(Key.Space) || Down(Key.F);
                secondary = Down(Key.K) || Down(Key.G);
                special = Down(Key.L) || Down(Key.H);
                item = Down(Key.I);
                previous = Down(Key.Q);
                next = Down(Key.E);
                start = Down(Key.Tab);
                leave = Down(Key.Backspace);
                break;

            case InputDeviceKind.KeyboardRight:
                move = Keys(Key.Up, Key.Left, Key.Down, Key.Right);
                primary = Down(Key.Period) || Down(Key.Kp1);
                secondary = Down(Key.Slash) || Down(Key.Kp2);
                special = Down(Key.Comma) || Down(Key.Kp3);
                item = Down(Key.M) || Down(Key.Kp0);
                previous = Down(Key.Semicolon);
                next = Down(Key.Apostrophe);
                start = Down(Key.Enter) || Down(Key.KpEnter);
                leave = Down(Key.Delete);
                break;

            default:
                var pad = Device.Joypad;
                move = GamepadMove(pad);
                primary = Input.IsJoyButtonPressed(pad, JoyButton.A);
                secondary = Input.IsJoyButtonPressed(pad, JoyButton.X);
                special = Input.IsJoyButtonPressed(pad, JoyButton.Y);
                item = Input.IsJoyButtonPressed(pad, JoyButton.B);
                previous = Input.IsJoyButtonPressed(pad, JoyButton.LeftShoulder);
                next = Input.IsJoyButtonPressed(pad, JoyButton.RightShoulder);
                start = Input.IsJoyButtonPressed(pad, JoyButton.Start);
                leave = Input.IsJoyButtonPressed(pad, JoyButton.Back);
                break;
        }

        Move = move.LimitLength(1f);
        PrimaryPressed = primary && !_primaryWasHeld;
        SecondaryPressed = secondary && !_secondaryWasHeld;
        SpecialPressed = special && !_specialWasHeld;
        ItemPressed = item && !_itemWasHeld;
        PreviousPressed = previous && !_previousWasHeld;
        NextPressed = next && !_nextWasHeld;
        MenuPressed = start && !_startWasHeld;
        JoinPressed = PrimaryPressed || SecondaryPressed || MenuPressed;
        LeaveHeldSeconds = leave ? LeaveHeldSeconds + delta : 0;

        var navigate = Move.X > 0.5f ? 1 : Move.X < -0.5f ? -1 : 0;
        NavigateX = navigate != _navigateWas ? navigate : 0;

        _primaryWasHeld = primary;
        _secondaryWasHeld = secondary;
        _specialWasHeld = special;
        _startWasHeld = start;
        _itemWasHeld = item;
        _previousWasHeld = previous;
        _nextWasHeld = next;
        _navigateWas = navigate;
    }

    private static bool Down(Key key) => Input.IsPhysicalKeyPressed(key);

    private static Vector2 Keys(Key up, Key left, Key down, Key right) =>
        new Vector2((Down(right) ? 1 : 0) - (Down(left) ? 1 : 0), (Down(down) ? 1 : 0) - (Down(up) ? 1 : 0)).Normalized();

    private static Vector2 GamepadMove(int pad)
    {
        var stick = new Vector2(Input.GetJoyAxis(pad, JoyAxis.LeftX), Input.GetJoyAxis(pad, JoyAxis.LeftY));
        if (stick.Length() >= StickDeadzone)
        {
            return stick;
        }

        return new Vector2(
            (Input.IsJoyButtonPressed(pad, JoyButton.DpadRight) ? 1 : 0) - (Input.IsJoyButtonPressed(pad, JoyButton.DpadLeft) ? 1 : 0),
            (Input.IsJoyButtonPressed(pad, JoyButton.DpadDown) ? 1 : 0) - (Input.IsJoyButtonPressed(pad, JoyButton.DpadUp) ? 1 : 0)).Normalized();
    }
}
