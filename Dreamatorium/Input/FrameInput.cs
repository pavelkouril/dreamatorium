using Dreamatorium.Input;

namespace Dreamatorium;

public readonly struct FrameInput(int frame, float time, float deltaTime, InputSnapshot input)
{
    public readonly int Frame = frame;

    public readonly float Time = time;

    public readonly float DeltaTime = deltaTime;

    public readonly InputSnapshot Input = input;

    public bool HasKeyEvent(KeyCode keyCode, KeyEventType type) => (Input.KeyEventFlags[(int)keyCode] & (int)type) != 0;
}
