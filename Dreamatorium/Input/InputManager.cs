namespace Dreamatorium.Input;

[Flags]
public enum KeyEventType
{
    KeyUp = 1,
    KeyDown = 1 << 1,
    IsRepeat = 1 << 2,
}

public class InputManager
{
    private sealed class SnapshotBuffer
    {
        public readonly byte[] KeyEventFlags = new byte[256];
        public readonly bool[] KeyDownState = new bool[256];
        public readonly bool[] MouseButtonState = new bool[5];
        public float MouseX;
        public float MouseY;
        public float MouseWheelX;
        public float MouseWheelY;
    }

    private readonly SnapshotBuffer[] _snapshotBuffers =
    [
        new SnapshotBuffer(),
        new SnapshotBuffer(),
        new SnapshotBuffer()
    ];

    private int _writeIndex;

    private readonly Lock _lock = new();

    public void RecordKeyEvent(ushort keyCode, KeyEventType type, bool isRepeat)
    {
        lock (_lock)
        {
            SnapshotBuffer writeBuffer = _snapshotBuffers[_writeIndex];
            if (keyCode >= writeBuffer.KeyEventFlags.Length)
            {
                return;
            }
            writeBuffer.KeyEventFlags[keyCode] |= (byte)type;
            if (isRepeat)
            {
                writeBuffer.KeyEventFlags[keyCode] |= (byte)KeyEventType.IsRepeat;
            }
            if ((type & KeyEventType.KeyDown) != 0)
            {
                writeBuffer.KeyDownState[keyCode] = true;
            }
            if ((type & KeyEventType.KeyUp) != 0)
            {
                writeBuffer.KeyDownState[keyCode] = false;
            }
        }
    }

    public void RecordMouseMove(float x, float y)
    {
        lock (_lock)
        {
            SnapshotBuffer writeBuffer = _snapshotBuffers[_writeIndex];
            writeBuffer.MouseX = x;
            writeBuffer.MouseY = y;
        }
    }

    public void RecordMouseButton(int buttonIndex, bool isDown)
    {
        lock (_lock)
        {
            SnapshotBuffer writeBuffer = _snapshotBuffers[_writeIndex];
            if ((uint)buttonIndex >= writeBuffer.MouseButtonState.Length)
            {
                return;
            }

            writeBuffer.MouseButtonState[buttonIndex] = isDown;
        }
    }

    public void RecordMouseWheel(float deltaX, float deltaY)
    {
        lock (_lock)
        {
            SnapshotBuffer writeBuffer = _snapshotBuffers[_writeIndex];
            writeBuffer.MouseWheelX += deltaX;
            writeBuffer.MouseWheelY += deltaY;
        }
    }

    public InputSnapshot CaptureSnapshotAndSwap()
    {
        lock (_lock)
        {
            SnapshotBuffer current = _snapshotBuffers[_writeIndex];
            int nextWriteIndex = (_writeIndex + 1) % _snapshotBuffers.Length;
            SnapshotBuffer next = _snapshotBuffers[nextWriteIndex];

            Array.Copy(current.KeyDownState, next.KeyDownState, current.KeyDownState.Length);
            Array.Copy(current.MouseButtonState, next.MouseButtonState, current.MouseButtonState.Length);
            next.MouseX = current.MouseX;
            next.MouseY = current.MouseY;
            next.MouseWheelX = 0.0f;
            next.MouseWheelY = 0.0f;
            Array.Clear(next.KeyEventFlags);

            _writeIndex = nextWriteIndex;

            return new InputSnapshot(
                current.KeyEventFlags,
                current.KeyDownState,
                current.MouseButtonState,
                current.MouseX,
                current.MouseY,
                current.MouseWheelX,
                current.MouseWheelY);
        }
    }
}
