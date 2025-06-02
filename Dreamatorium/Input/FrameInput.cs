using Dreamatorium.Input;

namespace Dreamatorium;

public readonly struct FrameInput(int frame, float time, float deltaTime, byte[] keys)
{
    public readonly int Frame = frame;

    public readonly float Time = time;

    public readonly float DeltaTime = deltaTime;

    public readonly byte[] Keys = keys;

    public bool HasKeyEvent(KeyCode keyCode, KeyEventType type) => (Keys[(int)keyCode] & (int)type) != 0;
}
