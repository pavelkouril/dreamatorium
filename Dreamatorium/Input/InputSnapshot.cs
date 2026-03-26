namespace Dreamatorium.Input;

public readonly struct InputSnapshot
{
    public readonly byte[] KeyEventFlags;
    public readonly bool[] KeyDownState;
    public readonly bool[] MouseButtonState;
    public readonly float MouseX;
    public readonly float MouseY;
    public readonly float MouseWheelX;
    public readonly float MouseWheelY;

    public InputSnapshot(byte[] keyEventFlags, bool[] keyDownState, bool[] mouseButtonState, float mouseX, float mouseY, float mouseWheelX, float mouseWheelY)
    {
        KeyEventFlags = keyEventFlags;
        KeyDownState = keyDownState;
        MouseButtonState = mouseButtonState;
        MouseX = mouseX;
        MouseY = mouseY;
        MouseWheelX = mouseWheelX;
        MouseWheelY = mouseWheelY;
    }
}
