using Dreamatorium.Input;

namespace Dreamatorium;

public readonly struct FrameInput(int frame, float time, float deltaTime, InputSnapshot input)
{
    public readonly int Frame = frame;

    public readonly float Time = time;

    public readonly float DeltaTime = deltaTime;

    public readonly InputSnapshot Input = input;

    public bool HasKeyEvent(KeyCode keyCode, KeyEventType type) => (Input.KeyEventFlags[(int)keyCode] & (int)type) != 0;

    public bool IsKeyDown(KeyCode keyCode) => Input.KeyDownState[(int)keyCode];

    public bool IsMouseDown(int buttonIndex) => (uint)buttonIndex < Input.MouseButtonState.Length && Input.MouseButtonState[buttonIndex];

    public FrameInput ConsumeCapturedInputs(in InputCaptureState captureState)
    {
        if (!captureState.ConsumeMouse && !captureState.ConsumeKeyboard)
        {
            return this;
        }

        byte[] keyEventFlags = Input.KeyEventFlags;
        bool[] keyDownState = Input.KeyDownState;
        bool[] mouseButtonState = Input.MouseButtonState;

        float mouseWheelX = Input.MouseWheelX;
        float mouseWheelY = Input.MouseWheelY;

        if (captureState.ConsumeKeyboard)
        {
            keyEventFlags = (byte[])Input.KeyEventFlags.Clone();
            keyDownState = (bool[])Input.KeyDownState.Clone();
            Array.Clear(keyEventFlags);
            Array.Clear(keyDownState);
        }

        if (captureState.ConsumeMouse)
        {
            mouseButtonState = (bool[])Input.MouseButtonState.Clone();
            Array.Clear(mouseButtonState);
            mouseWheelX = 0.0f;
            mouseWheelY = 0.0f;
        }

        var consumedSnapshot = new InputSnapshot(
            keyEventFlags,
            keyDownState,
            mouseButtonState,
            Input.MouseX,
            Input.MouseY,
            mouseWheelX,
            mouseWheelY);

        return new FrameInput(Frame, Time, DeltaTime, consumedSnapshot);
    }
}
